namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for Disassembler — verifies instruction decoding, mnemonic
/// generation, operand formatting, store/branch targets, inline text,
/// routine detection, cross-references, and export against real story
/// files and synthetic memory images.
/// </summary>
public class DisassemblerTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";
    private const string MindPath = "stories/mind.z4";
    private const string SherlockPath = "stories/sherlock.z5";

    #region Zork I — Main Routine

    /// <summary>
    /// Verifies that the main routine of Zork I can be disassembled
    /// and produces at least one instruction.
    /// </summary>
    [Fact]
    public void Zork1_MainRoutine_Disassembles()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var routine = dis.DisassembleRoutine(initialPC - 1);

        Assert.True(routine.Instructions.Count > 0,
            "Main routine should have instructions");
    }

    /// <summary>
    /// Verifies that the main routine starts with a call instruction
    /// (Zork I's main routine calls the game setup).
    /// </summary>
    [Fact]
    public void Zork1_MainRoutine_StartsWithCall()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var routine = dis.DisassembleRoutine(initialPC - 1);

        Assert.NotEmpty(routine.Instructions);
        var firstMnemonic = routine.Instructions[0].Mnemonic;
        Assert.Contains("call", firstMnemonic);
    }

    /// <summary>
    /// Verifies that the main routine has a valid local count.
    /// </summary>
    [Fact]
    public void Zork1_MainRoutine_HasLocalCount()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var routine = dis.DisassembleRoutine(initialPC - 1);

        Assert.InRange(routine.LocalCount, 0, 15);
    }

    /// <summary>
    /// Verifies that opcodes in the main routine have valid mnemonics
    /// (no "UNKNOWN" or raw form:number patterns).
    /// </summary>
    [Fact]
    public void Zork1_MainRoutine_ValidMnemonics()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var routine = dis.DisassembleRoutine(initialPC - 1);

        Assert.All(routine.Instructions, line =>
        {
            Assert.DoesNotContain("UNKNOWN", line.Mnemonic);
            Assert.NotEmpty(line.Mnemonic);
        });
    }

    #endregion

    #region Single Instruction

    /// <summary>
    /// Verifies that DisassembleAt produces correct address and
    /// non-empty raw bytes.
    /// </summary>
    [Fact]
    public void Zork1_DisassembleAt_HasAddressAndBytes()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var line = dis.DisassembleAt(initialPC);

        Assert.Equal(initialPC, line.Address);
        Assert.NotEmpty(line.RawBytesHex);
        Assert.True(line.EndAddress > line.Address);
    }

    /// <summary>
    /// Verifies that sequential disassembly produces contiguous addresses.
    /// </summary>
    [Fact]
    public void Zork1_DisassembleRange_Contiguous()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 10);

        Assert.True(lines.Count >= 2);
        for (int i = 1; i < lines.Count; i++)
            Assert.Equal(lines[i - 1].EndAddress, lines[i].Address);
    }

    #endregion

    #region Operand Formatting

    /// <summary>
    /// Verifies that variable operands are formatted as SP, Lxx, or Gxx.
    /// </summary>
    [Fact]
    public void Zork1_Operands_ContainVariableRefs()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 50);

        bool hasVariable = lines.Any(l =>
            l.Operands.Contains("SP") ||
            l.Operands.Contains("L") ||
            l.Operands.Contains("G"));

        Assert.True(hasVariable,
            "Disassembly should contain variable references");
    }

    /// <summary>
    /// Verifies that constant operands are formatted with # prefix.
    /// </summary>
    [Fact]
    public void Zork1_Operands_ContainConstants()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 50);

        Assert.Contains(lines, l => l.Operands.Contains("#"));
    }

    #endregion

    #region Store and Branch

    /// <summary>
    /// Verifies that store targets appear with -> prefix.
    /// </summary>
    [Fact]
    public void Zork1_Store_HasArrow()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 50);

        Assert.Contains(lines, l => l.Store.StartsWith("->"));
    }

    /// <summary>
    /// Verifies that branch targets appear with ? prefix.
    /// </summary>
    [Fact]
    public void Zork1_Branch_HasQuestionMark()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 100);

        Assert.Contains(lines, l =>
            l.Branch.StartsWith("?") || l.Branch.StartsWith("?~"));
    }

    #endregion

    #region Inline Text

    /// <summary>
    /// Verifies that print instructions have inline text decoded.
    /// </summary>
    [Fact]
    public void Zork1_InlineText_PrintDecoded()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        // Scan from the start of high memory for print instructions
        int highBase = memory.HighBase;
        var lines = dis.DisassembleRange(highBase, 500);

        var printLines = lines.Where(l =>
            l.Mnemonic is "print" or "print_ret").ToList();

        if (printLines.Count > 0)
        {
            Assert.All(printLines, l =>
                Assert.NotEmpty(l.InlineText));
        }
    }

    #endregion

    #region Routine Detection

    /// <summary>
    /// Verifies that routine detection finds at least the main routine.
    /// </summary>
    [Fact]
    public void Zork1_DetectRoutines_FindsMain()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        var routines = dis.DetectRoutines();

        Assert.NotEmpty(routines);
        // The main routine header should be at initialPC - 1 (for V3)
        // or at the packed address itself
        Assert.Contains(routines, r =>
            r.Address == initialPC - 1 || r.Address == initialPC);
    }

    /// <summary>
    /// Verifies that all detected routines have valid local counts.
    /// </summary>
    [Fact]
    public void Zork1_DetectRoutines_ValidLocalCounts()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var routines = dis.DetectRoutines();

        Assert.All(routines, r => Assert.InRange(r.LocalCount, 0, 15));
    }

    #endregion

    #region Cross-References

    /// <summary>
    /// Verifies that cross-references can be built from detected routines.
    /// </summary>
    [Fact]
    public void Zork1_CrossReferences_Build()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var routines = dis.DetectRoutines();

        if (routines.Count == 0) return;

        // Build xrefs for a subset to keep test fast
        var subset = routines.Take(10).ToList();
        var xrefs = dis.BuildCrossReferences(subset);

        Assert.Equal(subset.Count, xrefs.Count);
    }

    #endregion

    #region Export

    /// <summary>
    /// Verifies that export produces readable disassembly text.
    /// </summary>
    [Fact]
    public void Zork1_Export_ContainsHeader()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        string export = dis.Export(initialPC, 20);

        Assert.Contains("Z-Machine V3 Disassembly", export);
        Assert.Contains("call", export);
    }

    /// <summary>
    /// Verifies that export includes hex addresses.
    /// </summary>
    [Fact]
    public void Zork1_Export_IncludesAddresses()
    {
        if (!File.Exists(Zork1Path)) return;
        var dis = CreateDisassembler(Zork1Path);
        var memory = LoadMemory(Zork1Path);

        int initialPC = memory.ReadWord(0x06);
        string export = dis.Export(initialPC, 5);

        Assert.Contains("$", export);
    }

    #endregion

    #region Mnemonics

    /// <summary>
    /// Verifies mnemonic generation for all 2OP opcodes.
    /// </summary>
    [Fact]
    public void Mnemonics_2OP_KnownOpcodes()
    {
        var memory = CreateMinimalMemory(3);
        var dis = new Disassembler(memory);

        Assert.Equal("je", dis.GetMnemonic(OpcodeForm.Op2, 1));
        Assert.Equal("add", dis.GetMnemonic(OpcodeForm.Op2, 20));
        Assert.Equal("loadw", dis.GetMnemonic(OpcodeForm.Op2, 15));
        Assert.Equal("call_2s", dis.GetMnemonic(OpcodeForm.Op2, 25));
    }

    /// <summary>
    /// Verifies mnemonic generation for 1OP opcodes including
    /// version-dependent ones.
    /// </summary>
    [Fact]
    public void Mnemonics_1OP_VersionDependent()
    {
        var mem3 = CreateMinimalMemory(3);
        var dis3 = new Disassembler(mem3);
        Assert.Equal("not", dis3.GetMnemonic(OpcodeForm.Op1, 15));

        var mem5 = CreateMinimalMemory(5);
        var dis5 = new Disassembler(mem5);
        Assert.Equal("call_1n", dis5.GetMnemonic(OpcodeForm.Op1, 15));
    }

    /// <summary>
    /// Verifies mnemonic generation for 0OP opcodes.
    /// </summary>
    [Fact]
    public void Mnemonics_0OP_KnownOpcodes()
    {
        var memory = CreateMinimalMemory(3);
        var dis = new Disassembler(memory);

        Assert.Equal("rtrue", dis.GetMnemonic(OpcodeForm.Op0, 0));
        Assert.Equal("rfalse", dis.GetMnemonic(OpcodeForm.Op0, 1));
        Assert.Equal("print", dis.GetMnemonic(OpcodeForm.Op0, 2));
        Assert.Equal("print_ret", dis.GetMnemonic(OpcodeForm.Op0, 3));
        Assert.Equal("quit", dis.GetMnemonic(OpcodeForm.Op0, 10));
    }

    /// <summary>
    /// Verifies mnemonic generation for VAR opcodes.
    /// </summary>
    [Fact]
    public void Mnemonics_VAR_KnownOpcodes()
    {
        var memory = CreateMinimalMemory(5);
        var dis = new Disassembler(memory);

        Assert.Equal("call_vs", dis.GetMnemonic(OpcodeForm.Var, 0));
        Assert.Equal("storew", dis.GetMnemonic(OpcodeForm.Var, 1));
        Assert.Equal("aread", dis.GetMnemonic(OpcodeForm.Var, 4));
        Assert.Equal("call_vn", dis.GetMnemonic(OpcodeForm.Var, 25));
    }

    /// <summary>
    /// Verifies mnemonic generation for EXT opcodes.
    /// </summary>
    [Fact]
    public void Mnemonics_EXT_KnownOpcodes()
    {
        var memory = CreateMinimalMemory(5);
        var dis = new Disassembler(memory);

        Assert.Equal("save", dis.GetMnemonic(OpcodeForm.Ext, 0));
        Assert.Equal("restore", dis.GetMnemonic(OpcodeForm.Ext, 1));
        Assert.Equal("log_shift", dis.GetMnemonic(OpcodeForm.Ext, 2));
        Assert.Equal("save_undo", dis.GetMnemonic(OpcodeForm.Ext, 9));
        Assert.Equal("print_unicode", dis.GetMnemonic(OpcodeForm.Ext, 11));
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies that a V4 story disassembles from initial PC.
    /// </summary>
    [Fact]
    public void Mind_V4_DisassemblesMainRoutine()
    {
        if (!File.Exists(MindPath)) return;
        var dis = CreateDisassembler(MindPath);
        var memory = LoadMemory(MindPath);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 10);

        Assert.NotEmpty(lines);
    }

    /// <summary>
    /// Verifies that a V5 story disassembles from initial PC.
    /// </summary>
    [Fact]
    public void Sherlock_V5_DisassemblesMainRoutine()
    {
        if (!File.Exists(SherlockPath)) return;
        var dis = CreateDisassembler(SherlockPath);
        var memory = LoadMemory(SherlockPath);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 10);

        Assert.NotEmpty(lines);
    }

    /// <summary>
    /// Verifies V5 version-dependent mnemonics appear in Czech.
    /// </summary>
    [Fact]
    public void Czech_V5_HasV5Mnemonics()
    {
        if (!File.Exists(CzechPath)) return;
        var dis = CreateDisassembler(CzechPath);
        var memory = LoadMemory(CzechPath);

        int initialPC = memory.ReadWord(0x06);
        var lines = dis.DisassembleRange(initialPC, 200);

        // V5 should have call_vs, not just "call"
        Assert.Contains(lines, l =>
            l.Mnemonic == "call_vs" || l.Mnemonic == "call_1n" ||
            l.Mnemonic == "call_vn");
    }

    #endregion

    #region Synthetic Disassembly

    /// <summary>
    /// Verifies disassembly of a synthetic rtrue (0OP:0, byte $B0).
    /// </summary>
    [Fact]
    public void Synthetic_Rtrue()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;
        data[0x04] = 0x01; data[0x05] = 0x00;
        data[0x0E] = 0x01; data[0x0F] = 0x00;
        data[0x18] = 0x00; data[0x19] = 0x40;
        data[0x0100] = 0xB0; // rtrue

        var memory = new Memory();
        memory.LoadStory(data);
        var dis = new Disassembler(memory);

        var line = dis.DisassembleAt(0x0100);
        Assert.Equal("rtrue", line.Mnemonic);
        Assert.Equal(0x0100, line.Address);
        Assert.Equal(0x0101, line.EndAddress);
    }

    /// <summary>
    /// Verifies disassembly of a synthetic add with store.
    /// Long form: byte = 0b0_1_0_10100 = 0x54 (add, var op1, small op2)
    /// </summary>
    [Fact]
    public void Synthetic_Add_WithStore()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;
        data[0x04] = 0x01; data[0x05] = 0x00;
        data[0x0E] = 0x01; data[0x0F] = 0x00;
        data[0x18] = 0x00; data[0x19] = 0x40;

        // Long form add: opcode 20 = $14
        // 0b0_1_0_10100 = 0x54 (bit6=var, bit5=small)
        data[0x0100] = 0x54;
        data[0x0101] = 0x01; // var 1 (L00)
        data[0x0102] = 0x05; // small constant 5
        data[0x0103] = 0x02; // store to var 2 (L01)

        var memory = new Memory();
        memory.LoadStory(data);
        var dis = new Disassembler(memory);

        var line = dis.DisassembleAt(0x0100);
        Assert.Equal("add", line.Mnemonic);
        Assert.Contains("L00", line.Operands);
        Assert.Contains("#05", line.Operands);
        Assert.Equal("-> L01", line.Store);
    }

    /// <summary>
    /// Verifies disassembly of a synthetic je with branch.
    /// </summary>
    [Fact]
    public void Synthetic_Je_WithBranch()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;
        data[0x04] = 0x01; data[0x05] = 0x00;
        data[0x0E] = 0x01; data[0x0F] = 0x00;
        data[0x18] = 0x00; data[0x19] = 0x40;

        // Long form je: opcode 1 = 0x01
        // 0b0_0_0_00001 = 0x01 (both operands small constant)
        data[0x0100] = 0x01;
        data[0x0101] = 0x03; // small 3
        data[0x0102] = 0x03; // small 3
        // Branch: branch-on-true, single byte, offset = rtrue (1)
        data[0x0103] = 0xC1; // bit7=true, bit6=single, offset=1

        var memory = new Memory();
        memory.LoadStory(data);
        var dis = new Disassembler(memory);

        var line = dis.DisassembleAt(0x0100);
        Assert.Equal("je", line.Mnemonic);
        Assert.Equal("?TRUE", line.Branch);
    }

    #endregion

    #region Helpers

    /// <summary>Creates a Disassembler from a story file path.</summary>
    private static Disassembler CreateDisassembler(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return new Disassembler(memory);
    }

    /// <summary>Loads a Memory from a story file path.</summary>
    private static Memory LoadMemory(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return memory;
    }

    /// <summary>Creates a minimal synthetic memory for mnemonic tests.</summary>
    private static Memory CreateMinimalMemory(byte version)
    {
        var data = new byte[0x10000];
        data[0x00] = version;
        data[0x04] = 0x80; data[0x05] = 0x00;
        data[0x0E] = 0x80; data[0x0F] = 0x00;
        data[0x18] = 0x00; data[0x19] = 0x40;
        var memory = new Memory();
        memory.LoadStory(data);
        return memory;
    }

    #endregion
}
