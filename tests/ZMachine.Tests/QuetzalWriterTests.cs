namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests for QuetzalWriter — verifies IFhd, CMem, UMem, and Stks chunks
/// produced from live interpreter state against Quetzal 1.4 spec.
/// </summary>
public class QuetzalWriterTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";

    #region Valid IFF Output

    /// <summary>
    /// Verifies that saving produces a valid IFF FORM of type IFZS.
    /// </summary>
    [Fact]
    public void Save_Zork1_ProducesValidIFZS()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look", "open mailbox", "read leaflet"]);
        int savePC = machine.State.PC;

        byte[] data = QuetzalWriter.SaveToArray(machine, savePC);
        var form = IffReader.Parse(data);

        Assert.Equal("IFZS", form.FormType);
        Assert.Empty(form.Warnings);
    }

    /// <summary>
    /// Verifies that the output starts with 'FORM' magic.
    /// </summary>
    [Fact]
    public void Save_StartsWithFORM()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);

        Assert.Equal("FORM", System.Text.Encoding.ASCII.GetString(data, 0, 4));
    }

    /// <summary>
    /// Verifies that the FORM length field is correct.
    /// </summary>
    [Fact]
    public void Save_FORMLengthCorrect()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);

        uint formLength = (uint)(data[4] << 24 | data[5] << 16 |
                                  data[6] << 8 | data[7]);
        Assert.Equal((uint)(data.Length - 8), formLength);
    }

    #endregion

    #region IFhd Chunk (Quetzal S5.4)

    /// <summary>
    /// Verifies that IFhd is present and comes before CMem and Stks.
    /// Quetzal S5.4 — IFhd must come first.
    /// </summary>
    [Fact]
    public void Save_IFhdComesFirst()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        Assert.Equal("IFhd", form.Chunks[0].TypeId);
    }

    /// <summary>
    /// Verifies that IFhd is exactly 13 bytes.
    /// Quetzal S5.4.2.
    /// </summary>
    [Fact]
    public void Save_IFhd_Is13Bytes()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);
        var ifhd = form.GetChunk("IFhd")!;

        Assert.Equal(13, ifhd.Data.Length);
    }

    /// <summary>
    /// Verifies that IFhd contains the correct release number from header $02.
    /// </summary>
    [Fact]
    public void Save_IFhd_ReleaseNumber()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var ifhd = IffReader.Parse(data).GetChunk("IFhd")!;

        ushort release = (ushort)(ifhd.Data[0] << 8 | ifhd.Data[1]);
        ushort expected = machine.Memory.ReadWord(0x02);
        Assert.Equal(expected, release);
    }

    /// <summary>
    /// Verifies that IFhd contains the correct serial number from header $12.
    /// </summary>
    [Fact]
    public void Save_IFhd_SerialNumber()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var ifhd = IffReader.Parse(data).GetChunk("IFhd")!;

        for (int i = 0; i < 6; i++)
            Assert.Equal(machine.Memory.OriginalBytes[0x12 + i], ifhd.Data[2 + i]);
    }

    /// <summary>
    /// Verifies that IFhd contains the correct checksum from header $1C.
    /// </summary>
    [Fact]
    public void Save_IFhd_Checksum()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var ifhd = IffReader.Parse(data).GetChunk("IFhd")!;

        ushort checksum = (ushort)(ifhd.Data[8] << 8 | ifhd.Data[9]);
        ushort expected = machine.Memory.ReadWord(0x1C);
        Assert.Equal(expected, checksum);
    }

    /// <summary>
    /// Verifies that IFhd contains the correct PC as 3-byte big-endian.
    /// </summary>
    [Fact]
    public void Save_IFhd_PC()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);
        int savePC = machine.State.PC;

        byte[] data = QuetzalWriter.SaveToArray(machine, savePC);
        var ifhd = IffReader.Parse(data).GetChunk("IFhd")!;

        int pc = (ifhd.Data[10] << 16) | (ifhd.Data[11] << 8) | ifhd.Data[12];
        Assert.Equal(savePC, pc);
    }

    /// <summary>
    /// Verifies that the IFhd 13-byte odd length is properly padded in IFF.
    /// Quetzal S5.7.
    /// </summary>
    [Fact]
    public void Save_IFhd_OddLengthPadded()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        // If it parsed without error, padding is correct
        Assert.True(form.Chunks.Count >= 3);
    }

    #endregion

    #region CMem Chunk (Quetzal S3.2–S3.7)

    /// <summary>
    /// Verifies that CMem chunk is present when using compression.
    /// </summary>
    [Fact]
    public void Save_CMem_Present()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        Assert.NotNull(form.GetChunk("CMem"));
        Assert.Null(form.GetChunk("UMem"));
    }

    /// <summary>
    /// Verifies that CMem is smaller than dynamic memory (compression works).
    /// </summary>
    [Fact]
    public void Save_CMem_SmallerThanDynamic()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var cmem = IffReader.Parse(data).GetChunk("CMem")!;

        Assert.True(cmem.Data.Length < machine.Memory.StaticBase);
    }

    /// <summary>
    /// Verifies that CMem data decodes back to correct dynamic memory
    /// when XOR'd with the original.
    /// </summary>
    [Fact]
    public void Save_CMem_DecodesCorrectly()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox", "read leaflet"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var cmem = IffReader.Parse(data).GetChunk("CMem")!;

        // Decode CMem: XOR-compressed data
        int dynamicLen = machine.Memory.StaticBase;
        byte[] decoded = DecodeCMem(cmem.Data, machine.Memory.OriginalBytes, dynamicLen);

        // Compare with current dynamic memory
        for (int i = 0; i < dynamicLen; i++)
            Assert.Equal(machine.Memory.ReadByte(i), decoded[i]);
    }

    /// <summary>
    /// Verifies that an untouched interpreter produces minimal CMem.
    /// </summary>
    [Fact]
    public void Save_CMem_UntouchedMemory_MinimalSize()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var cmem = IffReader.Parse(data).GetChunk("CMem")!;

        // XOR of identical memory = all zeros, which compress to near-nothing
        // But the interpreter modifies some header flags on load, so not zero
        Assert.True(cmem.Data.Length < 100);
    }

    #endregion

    #region UMem Chunk (Quetzal S3.8)

    /// <summary>
    /// Verifies that UMem is present when compression is disabled.
    /// </summary>
    [Fact]
    public void Save_UMem_PresentWhenUncompressed()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC,
            useCompression: false);
        var form = IffReader.Parse(data);

        Assert.NotNull(form.GetChunk("UMem"));
        Assert.Null(form.GetChunk("CMem"));
    }

    /// <summary>
    /// Verifies that UMem equals the exact dynamic memory contents.
    /// </summary>
    [Fact]
    public void Save_UMem_MatchesDynamicMemory()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC,
            useCompression: false);
        var umem = IffReader.Parse(data).GetChunk("UMem")!;

        Assert.Equal(machine.Memory.StaticBase, umem.Data.Length);
        for (int i = 0; i < umem.Data.Length; i++)
            Assert.Equal(machine.Memory.ReadByte(i), umem.Data[i]);
    }

    #endregion

    #region Stks Chunk (Quetzal S4)

    /// <summary>
    /// Verifies that Stks chunk is present.
    /// </summary>
    [Fact]
    public void Save_Stks_Present()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        Assert.NotNull(form.GetChunk("Stks"));
    }

    /// <summary>
    /// Verifies that Stks has non-zero data (at least a dummy frame).
    /// </summary>
    [Fact]
    public void Save_Stks_NonEmpty()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var stks = IffReader.Parse(data).GetChunk("Stks")!;

        Assert.True(stks.Data.Length > 0);
    }

    /// <summary>
    /// Verifies that a V3 save file starts with a dummy frame (all zeros
    /// except eval stack count). Quetzal S4.11.
    /// </summary>
    [Fact]
    public void Save_Stks_V3DummyFrame()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var stks = IffReader.Parse(data).GetChunk("Stks")!;

        // Quetzal S4.11 — first frame: return PC=0, flags=0, store=0, args=0
        Assert.Equal(0, stks.Data[0]); // Return PC high
        Assert.Equal(0, stks.Data[1]); // Return PC mid
        Assert.Equal(0, stks.Data[2]); // Return PC low
        Assert.Equal(0, stks.Data[3]); // Flags (no locals)
        Assert.Equal(0, stks.Data[4]); // Store variable
        Assert.Equal(0, stks.Data[5]); // Argument flags
        // Bytes 6-7: eval stack count (may be 0 or more)
    }

    /// <summary>
    /// Verifies that the Stks frame count matches the call stack depth.
    /// </summary>
    [Fact]
    public void Save_Stks_FrameCountMatchesCallStack()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var stks = IffReader.Parse(data).GetChunk("Stks")!;

        // Count frames by parsing the Stks data
        int frameCount = CountStksFrames(stks.Data);
        Assert.Equal(machine.State.CallStack.FrameCount, frameCount);
    }

    /// <summary>
    /// Verifies that a second frame (after dummy) has a valid return PC.
    /// </summary>
    [Fact]
    public void Save_Stks_SecondFrameHasReturnPC()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var stks = IffReader.Parse(data).GetChunk("Stks")!;

        // Skip dummy frame: 8 bytes header + eval stack words
        int dummyEvalCount = (stks.Data[6] << 8) | stks.Data[7];
        int secondFrameOffset = 8 + dummyEvalCount * 2;

        if (secondFrameOffset + 8 <= stks.Data.Length)
        {
            int returnPC = (stks.Data[secondFrameOffset] << 16) |
                           (stks.Data[secondFrameOffset + 1] << 8) |
                           stks.Data[secondFrameOffset + 2];
            Assert.True(returnPC > 0);
        }
    }

    #endregion

    #region Chunk Ordering

    /// <summary>
    /// Verifies that chunks appear in order: IFhd, CMem/UMem, Stks.
    /// </summary>
    [Fact]
    public void Save_ChunkOrder_IFhd_CMem_Stks()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        Assert.Equal("IFhd", form.Chunks[0].TypeId);
        Assert.Equal("CMem", form.Chunks[1].TypeId);
        Assert.Equal("Stks", form.Chunks[2].TypeId);
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies that a V5 story file saves correctly.
    /// </summary>
    [Fact]
    public void Save_Czech_V5_ValidIFZS()
    {
        if (!File.Exists(CzechPath)) return;
        var machine = CreateMachine(CzechPath);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var form = IffReader.Parse(data);

        Assert.Equal("IFZS", form.FormType);
        Assert.NotNull(form.GetChunk("IFhd"));
        Assert.NotNull(form.GetChunk("CMem"));
        Assert.NotNull(form.GetChunk("Stks"));
    }

    /// <summary>
    /// Verifies that V5 IFhd contains valid data.
    /// </summary>
    [Fact]
    public void Save_Czech_V5_IFhdValid()
    {
        if (!File.Exists(CzechPath)) return;
        var machine = CreateMachine(CzechPath);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var ifhd = IffReader.Parse(data).GetChunk("IFhd")!;

        Assert.Equal(13, ifhd.Data.Length);
        ushort release = (ushort)(ifhd.Data[0] << 8 | ifhd.Data[1]);
        Assert.True(release > 0);
    }

    #endregion

    #region Round-Trip Verification

    /// <summary>
    /// Verifies that CMem data can fully reconstruct the original dynamic memory
    /// after game modifications (3 moves of Zork I).
    /// </summary>
    [Fact]
    public void Save_Zork1_3Moves_CMemRoundTrip()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox", "read leaflet", "go north"]);

        byte[] data = QuetzalWriter.SaveToArray(machine, machine.State.PC);
        var cmem = IffReader.Parse(data).GetChunk("CMem")!;

        byte[] decoded = DecodeCMem(cmem.Data, machine.Memory.OriginalBytes,
            machine.Memory.StaticBase);

        int diffs = 0;
        for (int i = 0; i < machine.Memory.StaticBase; i++)
        {
            if (decoded[i] != machine.Memory.ReadByte(i))
                diffs++;
        }
        Assert.Equal(0, diffs);
    }

    /// <summary>
    /// Verifies that saving to stream and byte array produce identical output.
    /// </summary>
    [Fact]
    public void Save_StreamAndArray_Identical()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);
        int pc = machine.State.PC;

        byte[] fromArray = QuetzalWriter.SaveToArray(machine, pc);

        using var ms = new MemoryStream();
        QuetzalWriter.Save(ms, machine, pc);
        byte[] fromStream = ms.ToArray();

        Assert.Equal(fromArray, fromStream);
    }

    #endregion

    #region Helpers

    private static Interpreter CreateMachine(string path)
    {
        var machine = new Interpreter();
        var input = new ScriptedInputStream(["quit", "y"]);
        var screen = new CaptureScreen();
        machine.Load(path, input, screen);
        return machine;
    }

    private static Interpreter RunMoves(string path, string[] moves)
    {
        var allCommands = new List<string>(moves);
        allCommands.Add("quit");
        allCommands.Add("y");

        var machine = new Interpreter();
        var input = new ScriptedInputStream(allCommands.ToArray());
        var screen = new CaptureScreen();
        machine.Load(path, input, screen);

        // Run until the interpreter reads input past our moves
        // Use limited stepping so we stop after the game processes moves
        int limit = 10_000_000;
        while (machine.Running && input.LinesConsumed < moves.Length + 1 && --limit > 0)
            machine.Step();

        return machine;
    }

    /// <summary>Decodes CMem compressed data back to dynamic memory.</summary>
    private static byte[] DecodeCMem(byte[] compressed, byte[] original, int dynamicLen)
    {
        var result = new byte[dynamicLen];
        Array.Copy(original, result, dynamicLen);

        int srcIdx = 0;
        int dstIdx = 0;
        while (srcIdx < compressed.Length && dstIdx < dynamicLen)
        {
            byte b = compressed[srcIdx++];
            if (b != 0)
            {
                result[dstIdx] ^= b;
                dstIdx++;
            }
            else
            {
                if (srcIdx >= compressed.Length) break;
                int runLen = compressed[srcIdx++] + 1;
                dstIdx += runLen;
            }
        }

        return result;
    }

    /// <summary>Counts frames in a Stks chunk by walking the frame data.</summary>
    private static int CountStksFrames(byte[] stksData)
    {
        int offset = 0;
        int count = 0;

        while (offset + 8 <= stksData.Length)
        {
            byte flags = stksData[offset + 3];
            int localCount = flags & 0x0F;
            int evalCount = (stksData[offset + 6] << 8) | stksData[offset + 7];

            offset += 8 + localCount * 2 + evalCount * 2;
            count++;
        }

        return count;
    }

    #endregion
}
