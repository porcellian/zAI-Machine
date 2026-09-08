namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests for QuetzalReader — verifies save/restore round-trips,
/// CMem/UMem decoding, Stks reconstruction, IFhd validation,
/// and error handling against Quetzal 1.4 spec.
/// </summary>
public class QuetzalReaderTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";

    #region Save/Restore Round-Trip

    /// <summary>
    /// Verifies that saving and restoring recovers the same PC.
    /// </summary>
    [Fact]
    public void RoundTrip_Zork1_PCRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox", "read leaflet"]);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        // Create a fresh interpreter and restore into it
        var restored = CreateMachine(Zork1Path);
        int restoredPC = QuetzalReader.Restore(saveData, restored);

        Assert.Equal(savedPC, restoredPC);
        Assert.Equal(savedPC, restored.State.PC);
    }

    /// <summary>
    /// Verifies that dynamic memory is fully restored after modifications.
    /// </summary>
    [Fact]
    public void RoundTrip_Zork1_DynamicMemoryRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox", "read leaflet"]);
        int savedPC = machine.State.PC;

        // Capture current dynamic memory state
        int dynamicLen = machine.Memory.StaticBase;
        byte[] expectedMemory = new byte[dynamicLen];
        for (int i = 0; i < dynamicLen; i++)
            expectedMemory[i] = machine.Memory.ReadByte(i);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        // Restore into a fresh interpreter
        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        for (int i = 0; i < dynamicLen; i++)
            Assert.Equal(expectedMemory[i], restored.Memory.ReadByte(i));
    }

    /// <summary>
    /// Verifies that call stack frame count is restored.
    /// </summary>
    [Fact]
    public void RoundTrip_Zork1_CallStackRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);
        int savedPC = machine.State.PC;
        int frameCount = machine.State.CallStack.FrameCount;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        Assert.Equal(frameCount, restored.State.CallStack.FrameCount);
    }

    /// <summary>
    /// Verifies that global variables are restored through memory.
    /// </summary>
    [Fact]
    public void RoundTrip_Zork1_GlobalsRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox", "read leaflet"]);
        int savedPC = machine.State.PC;

        // Read a few globals from the running machine
        ushort g0 = machine.State.ReadVariable(16);
        ushort g1 = machine.State.ReadVariable(17);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        Assert.Equal(g0, restored.State.ReadVariable(16));
        Assert.Equal(g1, restored.State.ReadVariable(17));
    }

    /// <summary>
    /// Verifies round-trip with save/modify/restore pattern.
    /// </summary>
    [Fact]
    public void RoundTrip_SaveModifyRestore()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        // Modify dynamic memory
        byte originalByte = machine.Memory.ReadByte(0x40);
        machine.Memory.WriteByte(0x40, (byte)(originalByte ^ 0xFF));
        Assert.NotEqual(originalByte, machine.Memory.ReadByte(0x40));

        // Restore should undo the modification
        QuetzalReader.Restore(saveData, machine);
        Assert.Equal(originalByte, machine.Memory.ReadByte(0x40));
    }

    #endregion

    #region CMem Compression Round-Trip

    /// <summary>
    /// Verifies CMem compression round-trip with multiple moves.
    /// </summary>
    [Fact]
    public void RoundTrip_CMem_3Moves()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path,
            ["open mailbox", "read leaflet", "go north"]);
        int savedPC = machine.State.PC;

        int dynamicLen = machine.Memory.StaticBase;
        byte[] expectedMemory = new byte[dynamicLen];
        for (int i = 0; i < dynamicLen; i++)
            expectedMemory[i] = machine.Memory.ReadByte(i);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        int diffs = 0;
        for (int i = 0; i < dynamicLen; i++)
        {
            if (expectedMemory[i] != restored.Memory.ReadByte(i))
                diffs++;
        }
        Assert.Equal(0, diffs);
    }

    #endregion

    #region UMem Round-Trip

    /// <summary>
    /// Verifies that UMem (uncompressed) round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_UMem_MemoryRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);
        int savedPC = machine.State.PC;

        int dynamicLen = machine.Memory.StaticBase;
        byte[] expectedMemory = new byte[dynamicLen];
        for (int i = 0; i < dynamicLen; i++)
            expectedMemory[i] = machine.Memory.ReadByte(i);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC,
            useCompression: false);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        for (int i = 0; i < dynamicLen; i++)
            Assert.Equal(expectedMemory[i], restored.Memory.ReadByte(i));
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies round-trip with a V5 story file.
    /// </summary>
    [Fact]
    public void RoundTrip_Czech_V5()
    {
        if (!File.Exists(CzechPath)) return;
        var machine = CreateMachine(CzechPath);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(CzechPath);
        int restoredPC = QuetzalReader.Restore(saveData, restored);

        Assert.Equal(savedPC, restoredPC);
        Assert.Equal(machine.State.CallStack.FrameCount,
            restored.State.CallStack.FrameCount);
    }

    #endregion

    #region IFhd Validation

    /// <summary>
    /// Verifies that a mismatched release number is rejected.
    /// </summary>
    [Fact]
    public void Restore_WrongRelease_Throws()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, machine.State.PC);

        // Corrupt the release number in IFhd (at offset 12: FORM(4)+len(4)+type(4))
        // IFhd header: type(4)+len(4)=8, then data starts at offset 20
        int ifhdDataOffset = FindChunkDataOffset(saveData, "IFhd");
        saveData[ifhdDataOffset] ^= 0xFF; // corrupt release high byte

        var restored = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(saveData, restored));
    }

    /// <summary>
    /// Verifies that a mismatched serial number is rejected.
    /// </summary>
    [Fact]
    public void Restore_WrongSerial_Throws()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, machine.State.PC);

        int ifhdDataOffset = FindChunkDataOffset(saveData, "IFhd");
        saveData[ifhdDataOffset + 2] ^= 0xFF; // corrupt serial byte 0

        var restored = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(saveData, restored));
    }

    /// <summary>
    /// Verifies that a mismatched checksum is rejected.
    /// </summary>
    [Fact]
    public void Restore_WrongChecksum_Throws()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        byte[] saveData = QuetzalWriter.SaveToArray(machine, machine.State.PC);

        int ifhdDataOffset = FindChunkDataOffset(saveData, "IFhd");
        saveData[ifhdDataOffset + 8] ^= 0xFF; // corrupt checksum high byte

        var restored = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(saveData, restored));
    }

    /// <summary>
    /// Verifies that a missing IFhd chunk is rejected.
    /// </summary>
    [Fact]
    public void Restore_MissingIFhd_Throws()
    {
        // Build a FORM IFZS with CMem and Stks but no IFhd
        byte[] data = IffWriter.WriteToArray("IFZS", [
            new IffChunk("CMem", []),
            new IffChunk("Stks", [0, 0, 0, 0, 0, 0, 0, 0])
        ]);

        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, machine));
    }

    /// <summary>
    /// Verifies that a missing CMem/UMem is rejected.
    /// </summary>
    [Fact]
    public void Restore_MissingMemoryChunk_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFZS", [
            new IffChunk("IFhd", new byte[13]),
            new IffChunk("Stks", [0, 0, 0, 0, 0, 0, 0, 0])
        ]);

        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, machine));
    }

    /// <summary>
    /// Verifies that a missing Stks is rejected.
    /// </summary>
    [Fact]
    public void Restore_MissingStks_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFZS", [
            new IffChunk("IFhd", new byte[13]),
            new IffChunk("CMem", [])
        ]);

        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, machine));
    }

    /// <summary>
    /// Verifies that a wrong FORM type is rejected.
    /// </summary>
    [Fact]
    public void Restore_WrongFormType_Throws()
    {
        byte[] data = IffWriter.WriteToArray("IFRS", [
            new IffChunk("IFhd", new byte[13])
        ]);

        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, machine));
    }

    #endregion

    #region CMem Error Handling (Quetzal S3.5)

    /// <summary>
    /// Verifies that CMem ending with incomplete run is rejected.
    /// </summary>
    [Fact]
    public void Restore_CMem_IncompleteRun_Throws()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        // Build valid IFhd for this story
        byte[] ifhdData = BuildValidIFhd(machine.Memory, machine.State.PC);

        // CMem with incomplete run: zero byte without length
        byte[] data = IffWriter.WriteToArray("IFZS", [
            new IffChunk("IFhd", ifhdData),
            new IffChunk("CMem", [0x01, 0x00]), // 0x01 is valid, then 0x00 with no length
            new IffChunk("Stks", BuildMinimalStks())
        ]);

        var restored = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, restored));
    }

    #endregion

    #region UMem Error Handling (Quetzal S3.6)

    /// <summary>
    /// Verifies that UMem with wrong length is rejected.
    /// </summary>
    [Fact]
    public void Restore_UMem_WrongLength_Throws()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = CreateMachine(Zork1Path);

        byte[] ifhdData = BuildValidIFhd(machine.Memory, machine.State.PC);

        // UMem with wrong length (too short)
        byte[] data = IffWriter.WriteToArray("IFZS", [
            new IffChunk("IFhd", ifhdData),
            new IffChunk("UMem", new byte[100]), // wrong length
            new IffChunk("Stks", BuildMinimalStks())
        ]);

        var restored = CreateMachine(Zork1Path);
        Assert.Throws<QuetzalException>(() =>
            QuetzalReader.Restore(data, restored));
    }

    #endregion

    #region IntD Chunk Handling (Quetzal S7.8)

    /// <summary>
    /// Verifies that IntD chunks from other interpreters don't cause rejection.
    /// </summary>
    [Fact]
    public void Restore_WithIntD_DoesNotReject()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        // Parse, add IntD, and re-serialize
        var form = IffReader.Parse(saveData);
        var chunks = form.Chunks.ToList();

        // IntD: OS="UNIX", flags=0, contentsID=0, reserved=0, interp="Frtz"
        byte[] intdData = [
            (byte)'U', (byte)'N', (byte)'I', (byte)'X', // OS ID
            0x00,                                         // flags
            0x00,                                         // contents ID
            0x00, 0x00,                                   // reserved
            (byte)'F', (byte)'r', (byte)'t', (byte)'z',  // interpreter ID
            0x01, 0x02                                    // arbitrary data
        ];
        chunks.Add(new IffChunk("IntD", intdData));

        byte[] withIntD = IffWriter.WriteToArray("IFZS", chunks);

        var restored = CreateMachine(Zork1Path);
        int restoredPC = QuetzalReader.Restore(withIntD, restored);

        Assert.Equal(savedPC, restoredPC);
    }

    #endregion

    #region Stks Restore

    /// <summary>
    /// Verifies that local variables are restored in stack frames.
    /// </summary>
    [Fact]
    public void RoundTrip_LocalsRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);
        int savedPC = machine.State.PC;

        // Capture current frame's locals
        var frame = machine.State.CallStack.CurrentFrame!;
        ushort[] locals = new ushort[frame.LocalCount];
        for (int i = 0; i < frame.LocalCount; i++)
            locals[i] = frame.Locals[i + 1];

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        var restoredFrame = restored.State.CallStack.CurrentFrame!;
        Assert.Equal(frame.LocalCount, restoredFrame.LocalCount);
        for (int i = 0; i < frame.LocalCount; i++)
            Assert.Equal(locals[i], restoredFrame.Locals[i + 1]);
    }

    /// <summary>
    /// Verifies that frame return PCs are restored.
    /// </summary>
    [Fact]
    public void RoundTrip_FrameReturnPCsRestored()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["open mailbox"]);
        int savedPC = machine.State.PC;

        var frames = machine.State.CallStack.GetFramesBottomUp().ToList();
        int[] returnPCs = frames.Skip(1).Select(f => f.ReturnPC).ToArray();

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        var restoredFrames = restored.State.CallStack.GetFramesBottomUp().ToList();
        int[] restoredPCs = restoredFrames.Skip(1).Select(f => f.ReturnPC).ToArray();

        Assert.Equal(returnPCs, restoredPCs);
    }

    /// <summary>
    /// Verifies the dummy frame is correctly handled on restore.
    /// </summary>
    [Fact]
    public void RoundTrip_DummyFrame_Preserved()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        QuetzalReader.Restore(saveData, restored);

        // The bottom frame should be the dummy with returnPC=0
        var bottomFrame = restored.State.CallStack.GetFramesBottomUp().First();
        Assert.Equal(0, bottomFrame.ReturnPC);
        Assert.Equal(0, bottomFrame.LocalCount);
    }

    #endregion

    #region Stream API

    /// <summary>
    /// Verifies that the stream-based restore API works.
    /// </summary>
    [Fact]
    public void Restore_FromStream_Works()
    {
        if (!File.Exists(Zork1Path)) return;
        var machine = RunMoves(Zork1Path, ["look"]);
        int savedPC = machine.State.PC;

        byte[] saveData = QuetzalWriter.SaveToArray(machine, savedPC);

        var restored = CreateMachine(Zork1Path);
        using var ms = new MemoryStream(saveData);
        int restoredPC = QuetzalReader.Restore(ms, restored);

        Assert.Equal(savedPC, restoredPC);
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
        var allCommands = new List<string>(moves) { "quit", "y" };
        var machine = new Interpreter();
        var input = new ScriptedInputStream(allCommands.ToArray());
        var screen = new CaptureScreen();
        machine.Load(path, input, screen);

        int limit = 10_000_000;
        while (machine.Running && input.LinesConsumed < moves.Length + 1 && --limit > 0)
            machine.Step();

        return machine;
    }

    /// <summary>Builds a valid IFhd for the given memory and PC.</summary>
    private static byte[] BuildValidIFhd(Memory memory, int pc)
    {
        var data = new byte[13];
        data[0] = memory.OriginalBytes[0x02];
        data[1] = memory.OriginalBytes[0x03];
        for (int i = 0; i < 6; i++)
            data[2 + i] = memory.OriginalBytes[0x12 + i];
        ushort checksum = (ushort)(memory.OriginalBytes[0x1C] << 8 |
                                   memory.OriginalBytes[0x1D]);
        if (checksum == 0)
        {
            uint sum = 0;
            for (int i = 0x40; i < memory.OriginalBytes.Length; i++)
                sum += memory.OriginalBytes[i];
            checksum = (ushort)(sum & 0xFFFF);
        }
        data[8] = (byte)(checksum >> 8);
        data[9] = (byte)(checksum & 0xFF);
        data[10] = (byte)((pc >> 16) & 0xFF);
        data[11] = (byte)((pc >> 8) & 0xFF);
        data[12] = (byte)(pc & 0xFF);
        return data;
    }

    /// <summary>Builds a minimal Stks with just a dummy frame.</summary>
    private static byte[] BuildMinimalStks()
    {
        // Dummy frame: 3-byte PC=0, flags=0, store=0, args=0, evalCount=0
        return [0, 0, 0, 0, 0, 0, 0, 0];
    }

    /// <summary>Finds the data offset of a chunk type in raw IFF bytes.</summary>
    private static int FindChunkDataOffset(byte[] iffData, string chunkType)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(chunkType);
        // Skip FORM header (12 bytes)
        int offset = 12;
        while (offset + 8 <= iffData.Length)
        {
            if (iffData[offset] == typeBytes[0] &&
                iffData[offset + 1] == typeBytes[1] &&
                iffData[offset + 2] == typeBytes[2] &&
                iffData[offset + 3] == typeBytes[3])
            {
                return offset + 8; // skip type(4) + length(4)
            }

            uint chunkLen = (uint)(iffData[offset + 4] << 24 |
                                    iffData[offset + 5] << 16 |
                                    iffData[offset + 6] << 8 |
                                    iffData[offset + 7]);
            offset += 8 + (int)chunkLen;
            if (chunkLen % 2 != 0) offset++;
        }

        throw new InvalidOperationException($"Chunk '{chunkType}' not found.");
    }

    #endregion
}
