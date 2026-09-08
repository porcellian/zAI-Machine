namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for Blorb 2.0.4 file parser and resource index.
/// Blorb "Overall Structure", "Contents of the Resource Index Chunk".
/// </summary>
public class BlorbTests
{
    #region Basic Loading

    /// <summary>
    /// Verifies that a minimal Blorb with one PNG picture loads correctly.
    /// </summary>
    [Fact]
    public void Load_SinglePngResource_Parsed()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47]; // fake PNG header
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", png)]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.Equal(png, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Equal("PNG ", blorb.GetResourceType(BlorbUsage.Picture, 1));
    }

    /// <summary>
    /// Verifies that a JPEG picture resource is parsed.
    /// </summary>
    [Fact]
    public void Load_JpegResource_Parsed()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 2, 0)],
            [new IffChunk("JPEG", jpeg)]);

        Assert.Equal("JPEG", blorb.GetResourceType(BlorbUsage.Picture, 2));
        Assert.Equal(jpeg, blorb.GetResource(BlorbUsage.Picture, 2));
    }

    /// <summary>
    /// Verifies that a Rect (placeholder rectangle) picture is parsed.
    /// Blorb "Placeholder Pictures" — chunk type 'Rect', 8 bytes.
    /// </summary>
    [Fact]
    public void Load_RectResource_Parsed()
    {
        byte[] rect = new byte[8];
        WriteInt32BE(rect, 0, 320); // width
        WriteInt32BE(rect, 4, 200); // height
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 3, 0)],
            [new IffChunk("Rect", rect)]);

        Assert.Equal("Rect", blorb.GetResourceType(BlorbUsage.Picture, 3));
        Assert.Equal(rect, blorb.GetResource(BlorbUsage.Picture, 3));
    }

    /// <summary>
    /// Verifies multiple resources of different types can coexist.
    /// </summary>
    [Fact]
    public void Load_MultipleResources_AllAccessible()
    {
        byte[] pngData = [0x89, 0x50];
        byte[] sndData = [0x01, 0x02, 0x03];
        byte[] codeData = [0x05, 0x06, 0x07, 0x08];

        var blorb = LoadBlorb(
            [
                (BlorbUsage.Picture, 1, 0),
                (BlorbUsage.Sound, 3, 1),
                (BlorbUsage.Executable, 0, 2)
            ],
            [
                new IffChunk("PNG ", pngData),
                new IffChunk("OGGV", sndData),
                new IffChunk("ZCOD", codeData)
            ]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.True(blorb.HasResource(BlorbUsage.Sound, 3));
        Assert.True(blorb.HasResource(BlorbUsage.Executable, 0));

        Assert.Equal(pngData, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Equal(sndData, blorb.GetResource(BlorbUsage.Sound, 3));
        Assert.Equal(codeData, blorb.GetResource(BlorbUsage.Executable, 0));

        Assert.Equal("PNG ", blorb.GetResourceType(BlorbUsage.Picture, 1));
        Assert.Equal("OGGV", blorb.GetResourceType(BlorbUsage.Sound, 3));
        Assert.Equal("ZCOD", blorb.GetResourceType(BlorbUsage.Executable, 0));
    }

    /// <summary>
    /// Verifies that multiple pictures with the same chunk type are all kept.
    /// </summary>
    [Fact]
    public void Load_MultiplePngs_AllAccessible()
    {
        byte[] png1 = [0x01, 0x02];
        byte[] png2 = [0x03, 0x04];
        byte[] png3 = [0x05, 0x06];

        var blorb = LoadBlorb(
            [
                (BlorbUsage.Picture, 1, 0),
                (BlorbUsage.Picture, 2, 1),
                (BlorbUsage.Picture, 3, 2)
            ],
            [
                new IffChunk("PNG ", png1),
                new IffChunk("PNG ", png2),
                new IffChunk("PNG ", png3)
            ]);

        Assert.Equal(png1, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Equal(png2, blorb.GetResource(BlorbUsage.Picture, 2));
        Assert.Equal(png3, blorb.GetResource(BlorbUsage.Picture, 3));
    }

    #endregion

    #region Sound Resources

    /// <summary>
    /// Verifies that an AIFF sound (nested FORM) is parsed and the
    /// reconstructed data is a valid AIFF file.
    /// Blorb "AIFF Sounds" — chunk type FORM with formtype AIFF.
    /// </summary>
    [Fact]
    public void Load_AiffSound_ReconstructedAsFullForm()
    {
        byte[] innerData = [0xAA, 0xBB, 0xCC, 0xDD];
        var aiffChunk = new IffChunk("FORM", innerData, "AIFF");

        var blorb = LoadBlorb(
            [(BlorbUsage.Sound, 3, 0)],
            [aiffChunk]);

        Assert.Equal("AIFF", blorb.GetResourceType(BlorbUsage.Sound, 3));

        byte[] resource = blorb.GetResource(BlorbUsage.Sound, 3);
        // Should be a complete AIFF file: FORM + length + AIFF + data
        Assert.Equal(16, resource.Length);
        Assert.Equal("FORM", System.Text.Encoding.ASCII.GetString(resource, 0, 4));
        Assert.Equal(8u, ReadUInt32BE(resource, 4)); // 4 (AIFF) + 4 (data)
        Assert.Equal("AIFF", System.Text.Encoding.ASCII.GetString(resource, 8, 4));
        Assert.Equal(innerData, resource[12..]);
    }

    /// <summary>
    /// Verifies that an Ogg Vorbis sound is parsed.
    /// Blorb "Ogg Sounds" — chunk type 'OGGV'.
    /// </summary>
    [Fact]
    public void Load_OggSound_Parsed()
    {
        byte[] ogg = [0x4F, 0x67, 0x67, 0x53]; // OggS header
        var blorb = LoadBlorb(
            [(BlorbUsage.Sound, 5, 0)],
            [new IffChunk("OGGV", ogg)]);

        Assert.Equal("OGGV", blorb.GetResourceType(BlorbUsage.Sound, 5));
        Assert.Equal(ogg, blorb.GetResource(BlorbUsage.Sound, 5));
    }

    /// <summary>
    /// Verifies that a MOD sound is parsed.
    /// Blorb "MOD Sounds" — chunk type 'MOD '.
    /// </summary>
    [Fact]
    public void Load_ModSound_Parsed()
    {
        byte[] mod = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];
        var blorb = LoadBlorb(
            [(BlorbUsage.Sound, 4, 0)],
            [new IffChunk("MOD ", mod)]);

        Assert.Equal("MOD ", blorb.GetResourceType(BlorbUsage.Sound, 4));
        Assert.Equal(mod, blorb.GetResource(BlorbUsage.Sound, 4));
    }

    #endregion

    #region Executable Resources

    /// <summary>
    /// Verifies that a Z-code executable resource is parsed.
    /// Blorb "Executable Resource Chunks" — 'ZCOD' chunk type, number 0.
    /// </summary>
    [Fact]
    public void Load_ZcodeExecutable_Parsed()
    {
        byte[] zcode = new byte[64];
        zcode[0] = 5; // Z-Machine version 5

        var blorb = LoadBlorb(
            [(BlorbUsage.Executable, 0, 0)],
            [new IffChunk("ZCOD", zcode)]);

        Assert.True(blorb.HasResource(BlorbUsage.Executable, 0));
        Assert.Equal("ZCOD", blorb.GetResourceType(BlorbUsage.Executable, 0));
        Assert.Equal(zcode, blorb.GetResource(BlorbUsage.Executable, 0));
    }

    #endregion

    #region Data Resources

    /// <summary>
    /// Verifies that TEXT data resources are parsed.
    /// Blorb "Data Resource Chunks" — chunk type 'TEXT'.
    /// </summary>
    [Fact]
    public void Load_TextDataResource_Parsed()
    {
        byte[] text = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
        var blorb = LoadBlorb(
            [(BlorbUsage.Data, 1, 0)],
            [new IffChunk("TEXT", text)]);

        Assert.Equal("TEXT", blorb.GetResourceType(BlorbUsage.Data, 1));
        Assert.Equal(text, blorb.GetResource(BlorbUsage.Data, 1));
    }

    /// <summary>
    /// Verifies that BINA data resources are parsed.
    /// Blorb "Data Resource Chunks" — chunk type 'BINA'.
    /// </summary>
    [Fact]
    public void Load_BinaryDataResource_Parsed()
    {
        byte[] bin = [0xDE, 0xAD, 0xBE, 0xEF];
        var blorb = LoadBlorb(
            [(BlorbUsage.Data, 2, 0)],
            [new IffChunk("BINA", bin)]);

        Assert.Equal("BINA", blorb.GetResourceType(BlorbUsage.Data, 2));
        Assert.Equal(bin, blorb.GetResource(BlorbUsage.Data, 2));
    }

    #endregion

    #region Shared Resource Chunks

    /// <summary>
    /// Verifies that multiple RIdx entries can point to the same chunk
    /// offset without duplication or error.
    /// Blorb "Contents of the Resource Index Chunk" — start field
    /// may refer to the same chunk for multiple entries.
    /// </summary>
    [Fact]
    public void Load_SharedChunk_BothEntriesResolve()
    {
        byte[] sharedPng = [0x89, 0x50, 0x4E, 0x47];
        // Both Pict 1 and Pict 2 point to the same chunk (index 0)
        var blorb = LoadBlorb(
            [
                (BlorbUsage.Picture, 1, 0),
                (BlorbUsage.Picture, 2, 0)
            ],
            [new IffChunk("PNG ", sharedPng)]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.True(blorb.HasResource(BlorbUsage.Picture, 2));
        Assert.Equal(sharedPng, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Equal(sharedPng, blorb.GetResource(BlorbUsage.Picture, 2));
    }

    #endregion

    #region Color Palette Chunk

    /// <summary>
    /// Verifies that a direct-color depth hint (16-bit) is parsed.
    /// Blorb "The Color Palette Chunk" — single byte value 16.
    /// </summary>
    [Fact]
    public void Load_PaletteDirectColor16_Parsed()
    {
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])],
            [new IffChunk("Plte", [16])]);

        Assert.NotNull(blorb.Palette);
        Assert.True(blorb.Palette!.IsDirectColor);
        Assert.Equal(16, blorb.Palette.DirectColorDepth);
    }

    /// <summary>
    /// Verifies that a direct-color depth hint (32-bit) is parsed.
    /// </summary>
    [Fact]
    public void Load_PaletteDirectColor32_Parsed()
    {
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])],
            [new IffChunk("Plte", [32])]);

        Assert.NotNull(blorb.Palette);
        Assert.True(blorb.Palette!.IsDirectColor);
        Assert.Equal(32, blorb.Palette.DirectColorDepth);
    }

    /// <summary>
    /// Verifies that an explicit RGB color list palette is parsed.
    /// </summary>
    [Fact]
    public void Load_PaletteRgbList_Parsed()
    {
        byte[] plteData =
        [
            255, 0, 0,       // red
            0, 255, 0,       // green
            0, 0, 255,       // blue
            128, 128, 128    // gray
        ];
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])],
            [new IffChunk("Plte", plteData)]);

        Assert.NotNull(blorb.Palette);
        Assert.False(blorb.Palette!.IsDirectColor);
        Assert.Equal(4, blorb.Palette.Colors!.Count);
        Assert.Equal((255, 0, 0), blorb.Palette.Colors[0]);
        Assert.Equal((0, 255, 0), blorb.Palette.Colors[1]);
        Assert.Equal((0, 0, 255), blorb.Palette.Colors[2]);
        Assert.Equal((128, 128, 128), blorb.Palette.Colors[3]);
    }

    /// <summary>
    /// Verifies that an illegal palette length produces a warning.
    /// Blorb "The Color Palette Chunk" — illegal if not 1 or multiple of 3.
    /// </summary>
    [Fact]
    public void Load_PaletteIllegalLength_WarnsAndSkips()
    {
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])],
            [new IffChunk("Plte", [0x01, 0x02])]);

        Assert.Null(blorb.Palette);
        Assert.Contains(blorb.Warnings, w => w.Contains("illegal length"));
    }

    /// <summary>
    /// Verifies that an invalid direct-color value produces a warning.
    /// </summary>
    [Fact]
    public void Load_PaletteInvalidDirectColor_WarnsAndSkips()
    {
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])],
            [new IffChunk("Plte", [24])]);

        Assert.Null(blorb.Palette);
        Assert.Contains(blorb.Warnings, w => w.Contains("not 16 or 32"));
    }

    /// <summary>
    /// Verifies that no Plte chunk means Palette is null.
    /// </summary>
    [Fact]
    public void Load_NoPalette_PaletteIsNull()
    {
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])]);

        Assert.Null(blorb.Palette);
    }

    #endregion

    #region Deprecated and Unknown Chunks

    /// <summary>
    /// Verifies that an SNam (deprecated story name) chunk is skipped gracefully.
    /// Blorb "Deprecated Chunks" — SNam is UTF-16 BE story name.
    /// </summary>
    [Fact]
    public void Load_SNameChunk_SkippedGracefully()
    {
        byte[] snamData = System.Text.Encoding.BigEndianUnicode.GetBytes("Zork I");
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Executable, 0, 0)],
            [new IffChunk("ZCOD", [0x05])],
            [new IffChunk("SNam", snamData)]);

        // Should not throw; resource should still be accessible
        Assert.True(blorb.HasResource(BlorbUsage.Executable, 0));
    }

    /// <summary>
    /// Verifies that unknown chunk types are ignored without error.
    /// </summary>
    [Fact]
    public void Load_UnknownChunks_SkippedGracefully()
    {
        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x89])],
            [
                new IffChunk("XYZW", [0x01, 0x02]),
                new IffChunk("AUTH", System.Text.Encoding.ASCII.GetBytes("Test Author")),
                new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("Note"))
            ]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
    }

    /// <summary>
    /// Verifies that optional IFhd, RelN, Fspc chunks do not break parsing.
    /// </summary>
    [Fact]
    public void Load_OptionalMetadataChunks_SkippedGracefully()
    {
        byte[] ifhdData = new byte[13];
        byte[] relnData = [0x00, 0x01]; // release 1
        byte[] fspcData = [0x00, 0x00, 0x00, 0x01]; // frontispiece = Pict 1

        var blorb = LoadBlorbWithExtra(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x89])],
            [
                new IffChunk("IFhd", ifhdData),
                new IffChunk("RelN", relnData),
                new IffChunk("Fspc", fspcData)
            ]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
    }

    #endregion

    #region Validation and Error Handling

    /// <summary>
    /// Verifies that a non-IFRS FORM type throws.
    /// </summary>
    [Fact]
    public void Load_WrongFormType_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFZS",
            [new IffChunk("IFhd", new byte[13])]);

        Assert.Throws<InvalidDataException>(() => BlorbReader.Load(data));
    }

    /// <summary>
    /// Verifies that a missing RIdx throws.
    /// Blorb "Overall Structure" — first chunk must be RIdx.
    /// </summary>
    [Fact]
    public void Load_MissingRIdx_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFRS",
            [new IffChunk("PNG ", [0x89])]);

        Assert.Throws<InvalidDataException>(() => BlorbReader.Load(data));
    }

    /// <summary>
    /// Verifies that RIdx not being the first chunk throws.
    /// </summary>
    [Fact]
    public void Load_RIdxNotFirst_Throws()
    {
        byte[] ridxData = new byte[4]; // count = 0
        byte[] data = IffWriter.WriteToArray("IFRS",
            [
                new IffChunk("PNG ", [0x89]),
                new IffChunk("RIdx", ridxData)
            ]);

        Assert.Throws<InvalidDataException>(() => BlorbReader.Load(data));
    }

    /// <summary>
    /// Verifies that an empty IFRS FORM (no chunks) throws.
    /// </summary>
    [Fact]
    public void Load_EmptyForm_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFRS", []);

        Assert.Throws<InvalidDataException>(() => BlorbReader.Load(data));
    }

    /// <summary>
    /// Verifies that a truncated RIdx throws.
    /// </summary>
    [Fact]
    public void Load_TruncatedRIdx_Throws()
    {
        byte[] ridxData = [0x00, 0x00, 0x00, 0x01]; // claims 1 entry, but no entry data
        byte[] data = IffWriter.WriteToArray("IFRS",
            [new IffChunk("RIdx", ridxData)]);

        Assert.Throws<InvalidDataException>(() => BlorbReader.Load(data));
    }

    /// <summary>
    /// Verifies that an RIdx entry pointing to an invalid offset produces a warning.
    /// </summary>
    [Fact]
    public void Load_RIdxBadOffset_Warns()
    {
        byte[] ridxData = new byte[4 + 12];
        WriteInt32BE(ridxData, 0, 1);
        WriteAscii(ridxData, 4, "Pict");
        WriteInt32BE(ridxData, 8, 1);
        WriteInt32BE(ridxData, 12, 99999); // invalid offset

        byte[] data = IffWriter.WriteToArray("IFRS",
            [new IffChunk("RIdx", ridxData)]);

        var blorb = BlorbReader.Load(data);
        Assert.False(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.Contains(blorb.Warnings,
            w => w.Contains("does not match any chunk"));
    }

    /// <summary>
    /// Verifies that duplicate RIdx entries produce a warning and first is kept.
    /// </summary>
    [Fact]
    public void Load_DuplicateRIdxEntry_FirstKeptWithWarning()
    {
        byte[] png1 = [0x01, 0x02];
        byte[] png2 = [0x03, 0x04];

        // Build manually with two entries for (Pict, 1) pointing to different chunks
        int ridxLen = 4 + 2 * 12;
        int chunk0Offset = 12 + 8 + ridxLen;
        int chunk1Offset = chunk0Offset + 8 + png1.Length; // png1 is even length

        byte[] ridxData = new byte[ridxLen];
        WriteInt32BE(ridxData, 0, 2);
        // Entry 0: Pict 1 → chunk 0
        WriteAscii(ridxData, 4, "Pict");
        WriteInt32BE(ridxData, 8, 1);
        WriteInt32BE(ridxData, 12, chunk0Offset);
        // Entry 1: Pict 1 → chunk 1 (duplicate)
        WriteAscii(ridxData, 16, "Pict");
        WriteInt32BE(ridxData, 20, 1);
        WriteInt32BE(ridxData, 24, chunk1Offset);

        byte[] data = IffWriter.WriteToArray("IFRS",
            [
                new IffChunk("RIdx", ridxData),
                new IffChunk("PNG ", png1),
                new IffChunk("PNG ", png2)
            ]);

        var blorb = BlorbReader.Load(data);
        Assert.Equal(png1, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Contains(blorb.Warnings, w => w.Contains("Duplicate RIdx"));
    }

    #endregion

    #region HasResource / Missing Resources

    /// <summary>
    /// Verifies that HasResource returns false for missing resources.
    /// </summary>
    [Fact]
    public void HasResource_Missing_ReturnsFalse()
    {
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x89])]);

        Assert.False(blorb.HasResource(BlorbUsage.Picture, 2));
        Assert.False(blorb.HasResource(BlorbUsage.Sound, 1));
        Assert.False(blorb.HasResource(BlorbUsage.Executable, 0));
    }

    /// <summary>
    /// Verifies that GetResource throws for missing resources.
    /// </summary>
    [Fact]
    public void GetResource_Missing_Throws()
    {
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x89])]);

        Assert.Throws<KeyNotFoundException>(
            () => blorb.GetResource(BlorbUsage.Picture, 99));
    }

    /// <summary>
    /// Verifies that GetResourceType throws for missing resources.
    /// </summary>
    [Fact]
    public void GetResourceType_Missing_Throws()
    {
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x89])]);

        Assert.Throws<KeyNotFoundException>(
            () => blorb.GetResourceType(BlorbUsage.Picture, 99));
    }

    #endregion

    #region Stream API

    /// <summary>
    /// Verifies that the stream-based Load API works.
    /// </summary>
    [Fact]
    public void Load_FromStream_Works()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        byte[] blorbData = BuildBlorbBytes(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", png)]);

        using var stream = new MemoryStream(blorbData);
        var blorb = BlorbReader.Load(stream);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.Equal(png, blorb.GetResource(BlorbUsage.Picture, 1));
    }

    #endregion

    #region Form Property

    /// <summary>
    /// Verifies that the Form property exposes the underlying IFF form.
    /// </summary>
    [Fact]
    public void Form_ExposesUnderlyingIffForm()
    {
        var blorb = LoadBlorb(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", [0x01])]);

        Assert.Equal("IFRS", blorb.Form.FormType);
        Assert.True(blorb.Form.Chunks.Count >= 2); // RIdx + PNG
    }

    #endregion

    #region Non-Contiguous Resource Numbers

    /// <summary>
    /// Verifies that resources don't need contiguous numbering.
    /// Blorb spec — pictures are not necessarily numbered contiguously.
    /// </summary>
    [Fact]
    public void Load_NonContiguousNumbers_AllAccessible()
    {
        byte[] png1 = [0x01];
        byte[] png2 = [0x02];
        byte[] png3 = [0x03];

        var blorb = LoadBlorb(
            [
                (BlorbUsage.Picture, 1, 0),
                (BlorbUsage.Picture, 5, 1),
                (BlorbUsage.Picture, 100, 2)
            ],
            [
                new IffChunk("PNG ", png1),
                new IffChunk("PNG ", png2),
                new IffChunk("PNG ", png3)
            ]);

        Assert.True(blorb.HasResource(BlorbUsage.Picture, 1));
        Assert.False(blorb.HasResource(BlorbUsage.Picture, 2));
        Assert.True(blorb.HasResource(BlorbUsage.Picture, 5));
        Assert.True(blorb.HasResource(BlorbUsage.Picture, 100));
    }

    #endregion

    #region Zero-Entry RIdx

    /// <summary>
    /// Verifies that a Blorb with zero resources loads without error.
    /// </summary>
    [Fact]
    public void Load_ZeroResources_LoadsSuccessfully()
    {
        byte[] ridxData = [0x00, 0x00, 0x00, 0x00]; // count = 0
        byte[] data = IffWriter.WriteToArray("IFRS",
            [new IffChunk("RIdx", ridxData)]);

        var blorb = BlorbReader.Load(data);
        Assert.False(blorb.HasResource(BlorbUsage.Picture, 1));
    }

    #endregion

    #region Odd-Length Resource Chunks

    /// <summary>
    /// Verifies that odd-length resource chunks are handled correctly —
    /// IFF padding bytes don't affect offset calculations for subsequent chunks.
    /// </summary>
    [Fact]
    public void Load_OddLengthChunks_OffsetsCorrect()
    {
        byte[] png1 = [0x01, 0x02, 0x03]; // 3 bytes, odd
        byte[] png2 = [0x04, 0x05];       // 2 bytes, even

        var blorb = LoadBlorb(
            [
                (BlorbUsage.Picture, 1, 0),
                (BlorbUsage.Picture, 2, 1)
            ],
            [
                new IffChunk("PNG ", png1),
                new IffChunk("PNG ", png2)
            ]);

        Assert.Equal(png1, blorb.GetResource(BlorbUsage.Picture, 1));
        Assert.Equal(png2, blorb.GetResource(BlorbUsage.Picture, 2));
    }

    #endregion

    #region Mixed Sound Types

    /// <summary>
    /// Verifies that AIFF, Ogg, and MOD sounds coexist correctly.
    /// </summary>
    [Fact]
    public void Load_MixedSoundTypes_AllAccessible()
    {
        byte[] aiffInner = [0x01, 0x02];
        byte[] oggData = [0x03, 0x04];
        byte[] modData = [0x05, 0x06];

        var blorb = LoadBlorb(
            [
                (BlorbUsage.Sound, 3, 0),
                (BlorbUsage.Sound, 4, 1),
                (BlorbUsage.Sound, 5, 2)
            ],
            [
                new IffChunk("FORM", aiffInner, "AIFF"),
                new IffChunk("OGGV", oggData),
                new IffChunk("MOD ", modData)
            ]);

        Assert.Equal("AIFF", blorb.GetResourceType(BlorbUsage.Sound, 3));
        Assert.Equal("OGGV", blorb.GetResourceType(BlorbUsage.Sound, 4));
        Assert.Equal("MOD ", blorb.GetResourceType(BlorbUsage.Sound, 5));
    }

    #endregion

    #region BlorbUsage Constants

    /// <summary>
    /// Verifies that BlorbUsage constants match the Blorb spec values.
    /// </summary>
    [Fact]
    public void BlorbUsage_ConstantsMatchSpec()
    {
        Assert.Equal("Pict", BlorbUsage.Picture);
        Assert.Equal("Snd ", BlorbUsage.Sound);
        Assert.Equal("Data", BlorbUsage.Data);
        Assert.Equal("Exec", BlorbUsage.Executable);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds a Blorb byte array with the given resource entries and chunks.
    /// Computes RIdx offsets automatically.
    /// </summary>
    private static byte[] BuildBlorbBytes(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks,
        IffChunk[]? extraChunks = null)
    {
        // RIdx data length is always even: 4 + 12n
        int ridxDataLen = 4 + entries.Length * 12;

        // Compute offsets for resource chunks
        // File: FORM(4) + len(4) + IFRS(4) + RIdx_header(8) + RIdx_data
        int offset = 12 + 8 + ridxDataLen;

        int[] chunkOffsets = new int[resourceChunks.Length];
        for (int i = 0; i < resourceChunks.Length; i++)
        {
            chunkOffsets[i] = offset;
            uint chunkLen = resourceChunks[i].Length;
            offset += 8 + (int)chunkLen;
            if (chunkLen % 2 != 0)
                offset++;
        }

        // Build RIdx data
        byte[] ridxData = new byte[ridxDataLen];
        WriteInt32BE(ridxData, 0, entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            int entryOff = 4 + i * 12;
            WriteAscii(ridxData, entryOff, entries[i].Usage);
            WriteInt32BE(ridxData, entryOff + 4, entries[i].Number);
            WriteInt32BE(ridxData, entryOff + 8, chunkOffsets[entries[i].ChunkIndex]);
        }

        var allChunks = new List<IffChunk> { new("RIdx", ridxData) };
        allChunks.AddRange(resourceChunks);
        if (extraChunks != null)
            allChunks.AddRange(extraChunks);

        return IffWriter.WriteToArray("IFRS", allChunks);
    }

    /// <summary>
    /// Builds and loads a Blorb with the given entries and resource chunks.
    /// </summary>
    private static BlorbReader LoadBlorb(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks)
    {
        byte[] data = BuildBlorbBytes(entries, resourceChunks);
        return BlorbReader.Load(data);
    }

    /// <summary>
    /// Builds and loads a Blorb with resource chunks and extra non-resource chunks.
    /// </summary>
    private static BlorbReader LoadBlorbWithExtra(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks,
        IffChunk[] extraChunks)
    {
        byte[] data = BuildBlorbBytes(entries, resourceChunks, extraChunks);
        return BlorbReader.Load(data);
    }

    private static void WriteInt32BE(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteAscii(byte[] buf, int offset, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, 0, buf, offset, 4);
    }

    private static uint ReadUInt32BE(byte[] data, int offset)
        => (uint)(data[offset] << 24 | data[offset + 1] << 16 |
                  data[offset + 2] << 8 | data[offset + 3]);

    #endregion
}
