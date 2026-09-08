namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for IFF container reader/writer — round-trip, padding,
/// nested FORMs, duplicate chunks, and error handling.
/// </summary>
public class IffTests
{
    #region Reader — Basic Parsing

    /// <summary>
    /// Verifies that a minimal FORM with no chunks parses correctly.
    /// </summary>
    [Fact]
    public void Parse_EmptyForm_ReturnsFormType()
    {
        byte[] data = BuildForm("TEST", []);
        var form = IffReader.Parse(data);

        Assert.Equal("TEST", form.FormType);
        Assert.Empty(form.Chunks);
    }

    /// <summary>
    /// Verifies that a FORM with a single chunk parses type, length, and data.
    /// </summary>
    [Fact]
    public void Parse_SingleChunk_ReturnsData()
    {
        byte[] chunkData = [0x01, 0x02, 0x03, 0x04];
        byte[] data = BuildForm("TEST", [new IffChunk("ABCD", chunkData)]);

        var form = IffReader.Parse(data);

        Assert.Single(form.Chunks);
        var chunk = form.Chunks[0];
        Assert.Equal("ABCD", chunk.TypeId);
        Assert.Equal(4u, chunk.Length);
        Assert.Equal(chunkData, chunk.Data);
    }

    /// <summary>
    /// Verifies that multiple chunks are parsed in file order.
    /// </summary>
    [Fact]
    public void Parse_MultipleChunks_PreservesOrder()
    {
        var chunks = new[]
        {
            new IffChunk("AAA ", [0x01, 0x02]),
            new IffChunk("BBB ", [0x03, 0x04]),
            new IffChunk("CCC ", [0x05, 0x06])
        };
        byte[] data = BuildForm("TEST", chunks);

        var form = IffReader.Parse(data);

        Assert.Equal(3, form.Chunks.Count);
        Assert.Equal("AAA ", form.Chunks[0].TypeId);
        Assert.Equal("BBB ", form.Chunks[1].TypeId);
        Assert.Equal("CCC ", form.Chunks[2].TypeId);
    }

    /// <summary>
    /// Verifies that an empty chunk (zero-length data) is handled.
    /// </summary>
    [Fact]
    public void Parse_EmptyChunk_HandledCorrectly()
    {
        byte[] data = BuildForm("TEST", [new IffChunk("EMTY", [])]);

        var form = IffReader.Parse(data);

        Assert.Single(form.Chunks);
        Assert.Empty(form.Chunks[0].Data);
        Assert.Equal(0u, form.Chunks[0].Length);
    }

    #endregion

    #region Reader — Padding (Quetzal S8.4.1)

    /// <summary>
    /// Verifies that odd-length chunks are properly padded during reading.
    /// The padding byte is not included in the chunk length.
    /// </summary>
    [Fact]
    public void Parse_OddLengthChunk_PaddingHandled()
    {
        // Build manually: FORM + length + type + chunk with 3 bytes + pad
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        WriteAscii(w, "FORM");
        WriteUInt32BE(w, 4 + 8 + 3 + 1 + 8 + 2); // formType + chunk1(8+3+1pad) + chunk2(8+2)
        WriteAscii(w, "TEST");

        // Chunk 1: 3 bytes (odd) + pad
        WriteAscii(w, "ODD ");
        WriteUInt32BE(w, 3);
        w.Write(new byte[] { 0xAA, 0xBB, 0xCC });
        w.Write((byte)0x00); // pad

        // Chunk 2: 2 bytes (even, no pad)
        WriteAscii(w, "EVEN");
        WriteUInt32BE(w, 2);
        w.Write(new byte[] { 0xDD, 0xEE });

        var form = IffReader.Parse(ms.ToArray());

        Assert.Equal(2, form.Chunks.Count);
        Assert.Equal("ODD ", form.Chunks[0].TypeId);
        Assert.Equal(3, form.Chunks[0].Data.Length);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, form.Chunks[0].Data);
        Assert.Equal("EVEN", form.Chunks[1].TypeId);
        Assert.Equal(new byte[] { 0xDD, 0xEE }, form.Chunks[1].Data);
    }

    /// <summary>
    /// Verifies that a single-byte chunk (odd) followed by another chunk
    /// doesn't lose the second chunk due to misaligned reads.
    /// </summary>
    [Fact]
    public void Parse_SingleByteChunk_PaddingCorrect()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        WriteAscii(w, "FORM");
        WriteUInt32BE(w, 4 + 8 + 1 + 1 + 8 + 4); // type + chunk1(8+1+1pad) + chunk2(8+4)
        WriteAscii(w, "TEST");

        WriteAscii(w, "ONE ");
        WriteUInt32BE(w, 1);
        w.Write((byte)0xFF);
        w.Write((byte)0x00); // pad

        WriteAscii(w, "FOUR");
        WriteUInt32BE(w, 4);
        w.Write(new byte[] { 1, 2, 3, 4 });

        var form = IffReader.Parse(ms.ToArray());

        Assert.Equal(2, form.Chunks.Count);
        Assert.Equal(new byte[] { 0xFF }, form.Chunks[0].Data);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, form.Chunks[1].Data);
    }

    #endregion

    #region Reader — Nested FORM (Blorb AIFF)

    /// <summary>
    /// Verifies that a nested FORM chunk exposes the inner form type.
    /// Blorb "AIFF Sounds" — AIFF is a nested FORM with formtype 'AIFF'.
    /// </summary>
    [Fact]
    public void Parse_NestedForm_ExposesInnerFormType()
    {
        byte[] innerData = [0x01, 0x02, 0x03, 0x04];
        var nestedChunk = new IffChunk("FORM", innerData, "AIFF");
        byte[] data = BuildForm("IFRS", [nestedChunk]);

        var form = IffReader.Parse(data);

        Assert.Single(form.Chunks);
        var chunk = form.Chunks[0];
        Assert.Equal("FORM", chunk.TypeId);
        Assert.Equal("AIFF", chunk.InnerFormType);
        Assert.Equal(innerData, chunk.Data);
        Assert.Equal(8u, chunk.Length); // 4 (inner type) + 4 (data)
    }

    /// <summary>
    /// Verifies that a nested FORM coexists with regular chunks.
    /// </summary>
    [Fact]
    public void Parse_NestedFormWithRegularChunks_BothParsed()
    {
        var chunks = new IffChunk[]
        {
            new("RIdx", [0x00, 0x00, 0x00, 0x01]),
            new("FORM", [0xAA, 0xBB], "AIFF"),
            new("ANNO", System.Text.Encoding.ASCII.GetBytes("test"))
        };
        byte[] data = BuildForm("IFRS", chunks);

        var form = IffReader.Parse(data);

        Assert.Equal(3, form.Chunks.Count);
        Assert.Equal("RIdx", form.Chunks[0].TypeId);
        Assert.Null(form.Chunks[0].InnerFormType);
        Assert.Equal("FORM", form.Chunks[1].TypeId);
        Assert.Equal("AIFF", form.Chunks[1].InnerFormType);
        Assert.Equal("ANNO", form.Chunks[2].TypeId);
    }

    #endregion

    #region Reader — Duplicate Chunks (Quetzal S8.8)

    /// <summary>
    /// Verifies that duplicate non-ANNO chunks produce a warning
    /// and only the first is kept.
    /// </summary>
    [Fact]
    public void Parse_DuplicateIFhd_FirstKeptWithWarning()
    {
        byte[] data1 = [0x01, 0x02, 0x03, 0x04];
        byte[] data2 = [0x05, 0x06, 0x07, 0x08];
        var chunks = new[] { new IffChunk("IFhd", data1), new IffChunk("IFhd", data2) };
        byte[] formData = BuildForm("IFZS", chunks);

        var form = IffReader.Parse(formData);

        Assert.Single(form.Chunks);
        Assert.Equal(data1, form.Chunks[0].Data);
        Assert.Single(form.Warnings);
        Assert.Contains("Duplicate", form.Warnings[0]);
        Assert.Contains("IFhd", form.Warnings[0]);
    }

    /// <summary>
    /// Verifies that multiple ANNO chunks are all kept (no warning).
    /// Quetzal S7.5 — multiple ANNO chunks are acceptable.
    /// </summary>
    [Fact]
    public void Parse_MultipleANNO_AllKept()
    {
        var chunks = new[]
        {
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("note 1")),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("note 2"))
        };
        byte[] data = BuildForm("IFZS", chunks);

        var form = IffReader.Parse(data);

        Assert.Equal(2, form.Chunks.Count);
        Assert.Empty(form.Warnings);
        Assert.Equal("note 1", form.Chunks[0].GetText());
        Assert.Equal("note 2", form.Chunks[1].GetText());
    }

    #endregion

    #region Reader — Unknown Chunks (Quetzal S8.9)

    /// <summary>
    /// Verifies that unknown chunk types are preserved without error.
    /// </summary>
    [Fact]
    public void Parse_UnknownChunkType_Preserved()
    {
        var chunks = new[]
        {
            new IffChunk("IFhd", [0x01]),
            new IffChunk("XYZW", [0x02, 0x03]),
            new IffChunk("CMem", [0x04])
        };
        byte[] data = BuildForm("IFZS", chunks);

        var form = IffReader.Parse(data);

        Assert.Equal(3, form.Chunks.Count);
        Assert.Equal("XYZW", form.Chunks[1].TypeId);
    }

    #endregion

    #region Reader — Error Handling

    /// <summary>
    /// Verifies that a non-FORM file throws InvalidDataException.
    /// </summary>
    [Fact]
    public void Parse_NotForm_Throws()
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes("LISTTEST");
        Assert.Throws<InvalidDataException>(() => IffReader.Parse(data));
    }

    /// <summary>
    /// Verifies that truncated data throws InvalidDataException.
    /// </summary>
    [Fact]
    public void Parse_TruncatedHeader_Throws()
    {
        byte[] data = [0x46, 0x4F, 0x52]; // "FOR" — incomplete
        Assert.Throws<InvalidDataException>(() => IffReader.Parse(data));
    }

    #endregion

    #region Reader — Text Chunks

    /// <summary>
    /// Verifies that AUTH, ANNO, (c) chunks decode as text.
    /// Quetzal S7.2 — text chunks contain simple ASCII.
    /// </summary>
    [Fact]
    public void Parse_TextChunks_DecodeCorrectly()
    {
        var chunks = new[]
        {
            new IffChunk("AUTH", System.Text.Encoding.ASCII.GetBytes("John Doe")),
            new IffChunk("(c) ", System.Text.Encoding.ASCII.GetBytes("2026 Test")),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("A note"))
        };
        byte[] data = BuildForm("IFZS", chunks);

        var form = IffReader.Parse(data);

        Assert.Equal("John Doe", form.GetChunk("AUTH")!.GetText());
        Assert.Equal("2026 Test", form.GetChunk("(c) ")!.GetText());
        Assert.Equal("A note", form.GetChunk("ANNO")!.GetText());
    }

    #endregion

    #region Reader — IffForm Helpers

    /// <summary>
    /// Verifies that GetChunk returns null for a missing type.
    /// </summary>
    [Fact]
    public void GetChunk_Missing_ReturnsNull()
    {
        byte[] data = BuildForm("TEST", [new IffChunk("AAAA", [0x01])]);
        var form = IffReader.Parse(data);

        Assert.Null(form.GetChunk("BBBB"));
    }

    /// <summary>
    /// Verifies that GetChunks returns all matching chunks.
    /// </summary>
    [Fact]
    public void GetChunks_MultipleANNO_ReturnsAll()
    {
        var chunks = new[]
        {
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("first")),
            new IffChunk("IFhd", [0x01]),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("second"))
        };
        byte[] data = BuildForm("IFZS", chunks);
        var form = IffReader.Parse(data);

        var annos = form.GetChunks("ANNO").ToList();
        Assert.Equal(2, annos.Count);
        Assert.Equal("first", annos[0].GetText());
        Assert.Equal("second", annos[1].GetText());
    }

    #endregion

    #region Writer — Basic Output

    /// <summary>
    /// Verifies that writing an empty FORM produces valid IFF.
    /// </summary>
    [Fact]
    public void Write_EmptyForm_ValidIff()
    {
        byte[] output = IffWriter.WriteToArray("TEST", []);

        // FORM(4) + length(4) + type(4) = 12 bytes
        Assert.Equal(12, output.Length);
        Assert.Equal("FORM", System.Text.Encoding.ASCII.GetString(output, 0, 4));
        Assert.Equal(4u, ReadUInt32BE(output, 4)); // just the type ID
        Assert.Equal("TEST", System.Text.Encoding.ASCII.GetString(output, 8, 4));
    }

    /// <summary>
    /// Verifies that writing a single chunk produces correct header and data.
    /// </summary>
    [Fact]
    public void Write_SingleChunk_CorrectFormat()
    {
        byte[] chunkData = [0xAA, 0xBB, 0xCC, 0xDD];
        byte[] output = IffWriter.WriteToArray("TEST", [new IffChunk("DATA", chunkData)]);

        // FORM header(12) + chunk header(8) + data(4) = 24
        Assert.Equal(24, output.Length);

        // FORM length = 4 (type) + 8 (chunk header) + 4 (data) = 16
        Assert.Equal(16u, ReadUInt32BE(output, 4));

        // Chunk at offset 12
        Assert.Equal("DATA", System.Text.Encoding.ASCII.GetString(output, 12, 4));
        Assert.Equal(4u, ReadUInt32BE(output, 16));
        Assert.Equal(chunkData, output[20..24]);
    }

    /// <summary>
    /// Verifies that the writer adds a padding byte for odd-length chunks.
    /// </summary>
    [Fact]
    public void Write_OddLengthChunk_AddsPadding()
    {
        byte[] chunkData = [0x01, 0x02, 0x03]; // 3 bytes, odd
        byte[] output = IffWriter.WriteToArray("TEST",
            [new IffChunk("ODD ", chunkData), new IffChunk("NEXT", [0xFF])]);

        // Parse it back to verify the padding didn't corrupt the next chunk
        var form = IffReader.Parse(output);

        Assert.Equal(2, form.Chunks.Count);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, form.Chunks[0].Data);
        Assert.Equal(new byte[] { 0xFF }, form.Chunks[1].Data);
    }

    /// <summary>
    /// Verifies that nested FORM chunks are written correctly with inner type.
    /// </summary>
    [Fact]
    public void Write_NestedForm_IncludesInnerType()
    {
        byte[] innerData = [0x01, 0x02];
        var nested = new IffChunk("FORM", innerData, "AIFF");
        byte[] output = IffWriter.WriteToArray("IFRS", [nested]);

        var form = IffReader.Parse(output);

        Assert.Single(form.Chunks);
        Assert.Equal("FORM", form.Chunks[0].TypeId);
        Assert.Equal("AIFF", form.Chunks[0].InnerFormType);
        Assert.Equal(innerData, form.Chunks[0].Data);
    }

    /// <summary>
    /// Verifies that invalid form type length throws.
    /// </summary>
    [Fact]
    public void Write_InvalidFormTypeLength_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IffWriter.WriteToArray("AB", []));
    }

    #endregion

    #region Round-Trip

    /// <summary>
    /// Verifies complete round-trip: write then parse recovers all data.
    /// </summary>
    [Fact]
    public void RoundTrip_MultipleChunks_DataPreserved()
    {
        var originalChunks = new[]
        {
            new IffChunk("IFhd", [0x01, 0x02, 0x03, 0x04, 0x05, 0x06,
                                   0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D]),
            new IffChunk("CMem", [0x10, 0x20, 0x30]),
            new IffChunk("Stks", [0xAA, 0xBB, 0xCC, 0xDD]),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("test annotation"))
        };

        byte[] written = IffWriter.WriteToArray("IFZS", originalChunks);
        var parsed = IffReader.Parse(written);

        Assert.Equal("IFZS", parsed.FormType);
        Assert.Equal(4, parsed.Chunks.Count);

        for (int i = 0; i < originalChunks.Length; i++)
        {
            Assert.Equal(originalChunks[i].TypeId, parsed.Chunks[i].TypeId);
            Assert.Equal(originalChunks[i].Data, parsed.Chunks[i].Data);
        }
    }

    /// <summary>
    /// Verifies round-trip with odd-length IFhd (13 bytes, per Quetzal S5.7).
    /// </summary>
    [Fact]
    public void RoundTrip_IFhd13Bytes_PaddingPreserved()
    {
        byte[] ifhdData = new byte[13];
        ifhdData[0] = 0x00; ifhdData[1] = 0x02; // release 2
        ifhdData[2] = (byte)'8'; ifhdData[3] = (byte)'4';
        ifhdData[4] = (byte)'0'; ifhdData[5] = (byte)'7';
        ifhdData[6] = (byte)'2'; ifhdData[7] = (byte)'6';
        ifhdData[8] = 0xAB; ifhdData[9] = 0xCD; // checksum
        ifhdData[10] = 0x00; ifhdData[11] = 0x50; ifhdData[12] = 0x00; // PC

        byte[] written = IffWriter.WriteToArray("IFZS",
            [new IffChunk("IFhd", ifhdData), new IffChunk("CMem", [0x00])]);
        var parsed = IffReader.Parse(written);

        Assert.Equal(2, parsed.Chunks.Count);
        Assert.Equal(ifhdData, parsed.Chunks[0].Data);
        Assert.Equal(13u, parsed.Chunks[0].Length);
    }

    /// <summary>
    /// Verifies round-trip with a nested FORM chunk.
    /// </summary>
    [Fact]
    public void RoundTrip_NestedForm_Preserved()
    {
        byte[] aiffData = [0x01, 0x02, 0x03];
        var chunks = new IffChunk[]
        {
            new("RIdx", [0x00, 0x00, 0x00, 0x01]),
            new("FORM", aiffData, "AIFF")
        };

        byte[] written = IffWriter.WriteToArray("IFRS", chunks);
        var parsed = IffReader.Parse(written);

        Assert.Equal("IFRS", parsed.FormType);
        Assert.Equal(2, parsed.Chunks.Count);
        Assert.Equal("FORM", parsed.Chunks[1].TypeId);
        Assert.Equal("AIFF", parsed.Chunks[1].InnerFormType);
        Assert.Equal(aiffData, parsed.Chunks[1].Data);
    }

    /// <summary>
    /// Verifies that a large data chunk round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_LargeChunk_DataPreserved()
    {
        byte[] largeData = new byte[10000];
        var rng = new Random(42);
        rng.NextBytes(largeData);

        byte[] written = IffWriter.WriteToArray("TEST",
            [new IffChunk("BIG ", largeData)]);
        var parsed = IffReader.Parse(written);

        Assert.Equal(largeData, parsed.Chunks[0].Data);
    }

    #endregion

    #region Stream-Based API

    /// <summary>
    /// Verifies that the stream-based Parse and Write APIs work.
    /// </summary>
    [Fact]
    public void StreamAPI_WriteAndParse_RoundTrips()
    {
        var chunks = new[] { new IffChunk("TDAT", [0x01, 0x02]) };

        using var ms = new MemoryStream();
        IffWriter.Write(ms, "TEST", chunks);

        ms.Position = 0;
        var form = IffReader.Parse(ms);

        Assert.Equal("TEST", form.FormType);
        Assert.Single(form.Chunks);
        Assert.Equal(new byte[] { 0x01, 0x02 }, form.Chunks[0].Data);
    }

    #endregion

    #region Quetzal-Specific

    /// <summary>
    /// Verifies that an IFZS form type is recognized.
    /// </summary>
    [Fact]
    public void Parse_QuetzalFormType_IFZS()
    {
        byte[] data = BuildForm("IFZS", [new IffChunk("IFhd", new byte[13])]);
        var form = IffReader.Parse(data);
        Assert.Equal("IFZS", form.FormType);
    }

    /// <summary>
    /// Verifies that an IFRS form type (Blorb) is recognized.
    /// </summary>
    [Fact]
    public void Parse_BlorbFormType_IFRS()
    {
        byte[] data = BuildForm("IFRS", [new IffChunk("RIdx", [0x00, 0x00, 0x00, 0x00])]);
        var form = IffReader.Parse(data);
        Assert.Equal("IFRS", form.FormType);
    }

    #endregion

    #region IffChunk Properties

    /// <summary>
    /// Verifies that Length for a regular chunk equals data length.
    /// </summary>
    [Fact]
    public void Chunk_Length_EqualsDataLength()
    {
        var chunk = new IffChunk("TEST", [0x01, 0x02, 0x03]);
        Assert.Equal(3u, chunk.Length);
    }

    /// <summary>
    /// Verifies that Length for a nested FORM includes the inner type.
    /// </summary>
    [Fact]
    public void Chunk_NestedForm_LengthIncludesInnerType()
    {
        var chunk = new IffChunk("FORM", [0x01, 0x02], "AIFF");
        Assert.Equal(6u, chunk.Length); // 4 (inner type) + 2 (data)
    }

    #endregion

    #region Helpers

    /// <summary>Builds a FORM byte array using IffWriter.</summary>
    private static byte[] BuildForm(string formType, IffChunk[] chunks)
        => IffWriter.WriteToArray(formType, chunks);

    private static void WriteAscii(BinaryWriter w, string s)
        => w.Write(System.Text.Encoding.ASCII.GetBytes(s));

    private static void WriteUInt32BE(BinaryWriter w, uint value)
    {
        w.Write((byte)(value >> 24));
        w.Write((byte)(value >> 16));
        w.Write((byte)(value >> 8));
        w.Write((byte)value);
    }

    private static uint ReadUInt32BE(byte[] data, int offset)
        => (uint)(data[offset] << 24 | data[offset + 1] << 16 |
                  data[offset + 2] << 8 | data[offset + 3]);

    #endregion
}
