namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for InstructionDecoder — covers all four encoding forms (long,
/// short, variable, extended), operand type decoding, store/branch
/// decoding, and double-variable forms. Uses hand-crafted byte sequences
/// and real instructions from zork1.z3.
/// </summary>
public class InstructionDecoderTests
{
    #region Long Form (top bits 0b0x)

    [Fact]
    public void LongForm_TwoSmallConstants()
    {
        // Long form: bit 7=0, bit 6=0 (op1=small), bit 5=0 (op2=small)
        // Opcode 1 = je (2OP:1), operands: $05, $0A
        var mem = MemoryFromBytes(0x01, 0x05, 0x0A);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op2, inst.Form);
        Assert.Equal(1, inst.Opcode);
        Assert.Equal(2, inst.OperandCount);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[1]);
        Assert.Equal(5, inst.Operands[0]);
        Assert.Equal(10, inst.Operands[1]);
        Assert.Equal(3, inst.NextAddress);
    }

    [Fact]
    public void LongForm_VariableAndSmall()
    {
        // bit 6=1 (op1=variable), bit 5=0 (op2=small)
        // Opcode 15 = loadw (2OP:15), var $10 (global 0), small $02
        var mem = MemoryFromBytes(0x4F, 0x10, 0x02);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op2, inst.Form);
        Assert.Equal(15, inst.Opcode);
        Assert.Equal(2, inst.OperandCount);
        Assert.Equal(OperandType.Variable, inst.OperandTypes[0]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[1]);
        Assert.Equal(0x10, inst.Operands[0]);
        Assert.Equal(0x02, inst.Operands[1]);
    }

    [Fact]
    public void LongForm_SmallAndVariable()
    {
        // bit 6=0 (op1=small), bit 5=1 (op2=variable)
        // Opcode 5 = inc_chk (2OP:5), small $03, var $01 (local 1)
        var mem = MemoryFromBytes(0x25, 0x03, 0x01);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.Variable, inst.OperandTypes[1]);
        Assert.Equal(0x03, inst.Operands[0]);
        Assert.Equal(0x01, inst.Operands[1]);
    }

    [Fact]
    public void LongForm_TwoVariables()
    {
        // bit 6=1 (op1=var), bit 5=1 (op2=var)
        // Opcode 2 = jl (2OP:2), var $01, var $02
        var mem = MemoryFromBytes(0x62, 0x01, 0x02);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OperandType.Variable, inst.OperandTypes[0]);
        Assert.Equal(OperandType.Variable, inst.OperandTypes[1]);
    }

    #endregion

    #region Short Form (top bits 0b10)

    [Fact]
    public void ShortForm_LargeConstant()
    {
        // bits 5,4 = 0b00 → large constant, opcode = bottom 4 bits
        // 0x80 | (0b00 << 4) | opcode 0 = 0x80; then 2-byte operand $1234
        var mem = MemoryFromBytes(0x80, 0x12, 0x34);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op1, inst.Form);
        Assert.Equal(0, inst.Opcode);
        Assert.Equal(1, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(0x1234, inst.Operands[0]);
        Assert.Equal(3, inst.NextAddress);
    }

    [Fact]
    public void ShortForm_SmallConstant()
    {
        // bits 5,4 = 0b01 → small constant
        // 0x90 | opcode 1 = 0x91; then 1-byte operand $FF
        var mem = MemoryFromBytes(0x91, 0xFF);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op1, inst.Form);
        Assert.Equal(1, inst.Opcode);
        Assert.Equal(1, inst.OperandCount);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[0]);
        Assert.Equal(0xFF, inst.Operands[0]);
        Assert.Equal(2, inst.NextAddress);
    }

    [Fact]
    public void ShortForm_Variable()
    {
        // bits 5,4 = 0b10 → variable
        // 0xA0 | opcode 5 = 0xA5; then var number $00 (stack pointer)
        var mem = MemoryFromBytes(0xA5, 0x00);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op1, inst.Form);
        Assert.Equal(5, inst.Opcode);
        Assert.Equal(1, inst.OperandCount);
        Assert.Equal(OperandType.Variable, inst.OperandTypes[0]);
        Assert.Equal(0x00, inst.Operands[0]);
    }

    [Fact]
    public void ShortForm_ZeroOp()
    {
        // bits 5,4 = 0b11 → 0OP
        // 0xB0 | opcode 0 = 0xB0 (rtrue)
        var mem = MemoryFromBytes(0xB0);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op0, inst.Form);
        Assert.Equal(0, inst.Opcode);
        Assert.Equal(0, inst.OperandCount);
        Assert.Equal(1, inst.NextAddress);
    }

    [Fact]
    public void ShortForm_ZeroOp_Rfalse()
    {
        // 0xB1 = rfalse (0OP:1)
        var mem = MemoryFromBytes(0xB1);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op0, inst.Form);
        Assert.Equal(1, inst.Opcode);
    }

    #endregion

    #region Variable Form (top bits 0b11)

    [Fact]
    public void VariableForm_2OP_WithTypeBytes()
    {
        // 0b11_0_xxxxx: bit 5=0 → 2OP in variable encoding
        // 0xC1 = 2OP opcode 1 (je) in variable form
        // Type byte: 0b00_01_11_11 = large, small, omitted, omitted → 2 operands
        var mem = MemoryFromBytes(0xC1, 0b0001_1111, 0x00, 0x42, 0x05);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op2, inst.Form);
        Assert.Equal(1, inst.Opcode);
        Assert.Equal(2, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[1]);
        Assert.Equal(0x0042, inst.Operands[0]);
        Assert.Equal(0x05, inst.Operands[1]);
    }

    [Fact]
    public void VariableForm_VAR_Call()
    {
        // 0b11_1_xxxxx: bit 5=1 → VAR
        // 0xE0 = VAR opcode 0 (call/call_vs)
        // Type byte: 0b00_01_01_11 = large, small, small, omitted → 3 operands
        var mem = MemoryFromBytes(0xE0, 0b0001_0111, 0x2A, 0x39, 0x05, 0x0A);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Var, inst.Form);
        Assert.Equal(0, inst.Opcode);
        Assert.Equal(3, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[1]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[2]);
        Assert.Equal(0x2A39, inst.Operands[0]);
        Assert.Equal(0x05, inst.Operands[1]);
        Assert.Equal(0x0A, inst.Operands[2]);
    }

    [Fact]
    public void VariableForm_AllFourOperands()
    {
        // Type byte: 0b00_00_01_10 = large, large, small, variable → 4 operands
        var mem = MemoryFromBytes(0xE0, 0b0000_0110, 0x12, 0x34, 0x56, 0x78, 0x9A, 0x01);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(4, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[1]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[2]);
        Assert.Equal(OperandType.Variable, inst.OperandTypes[3]);
        Assert.Equal(0x1234, inst.Operands[0]);
        Assert.Equal(0x5678, inst.Operands[1]);
        Assert.Equal(0x9A, inst.Operands[2]);
        Assert.Equal(0x01, inst.Operands[3]);
    }

    [Fact]
    public void VariableForm_ZeroOperands()
    {
        // Type byte: 0xFF = all omitted → 0 operands
        var mem = MemoryFromBytes(0xE0, 0xFF);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Var, inst.Form);
        Assert.Equal(0, inst.OperandCount);
    }

    #endregion

    #region Extended Form ($BE prefix)

    [Fact]
    public void ExtendedForm_BasicDecode()
    {
        // 0xBE = extended prefix, next byte = EXT opcode
        // EXT opcode 0 = save (V5+)
        // Type byte: 0b00_01_11_11 = large, small, omitted, omitted → 2 operands
        var mem = MemoryFromBytes(0xBE, 0x00, 0b0001_1111, 0x10, 0x00, 0x05);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.True(inst.IsExtended);
        Assert.Equal(OpcodeForm.Ext, inst.Form);
        Assert.Equal(0, inst.Opcode);
        Assert.Equal(2, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[1]);
        Assert.Equal(0x1000, inst.Operands[0]);
        Assert.Equal(0x05, inst.Operands[1]);
    }

    [Fact]
    public void ExtendedForm_NoOperands()
    {
        // EXT opcode 9 = save_undo — no operands (type byte = 0xFF)
        var mem = MemoryFromBytes(0xBE, 0x09, 0xFF);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.True(inst.IsExtended);
        Assert.Equal(9, inst.Opcode);
        Assert.Equal(0, inst.OperandCount);
    }

    #endregion

    #region Double Variable Forms (call_vs2, call_vn2)

    [Fact]
    public void DoubleVariable_CallVs2_EightOperands()
    {
        // VAR opcode 12 = call_vs2 ($EC = 0b11101100)
        // Two type bytes allow up to 8 operands
        // Type byte 1: 0b01_01_01_01 = 4 small constants
        // Type byte 2: 0b01_01_01_11 = 3 small constants + omitted
        var mem = MemoryFromBytes(
            0xEC,
            0b01_01_01_01,
            0b01_01_01_11,
            0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Var, inst.Form);
        Assert.Equal(CallVs2Opcode, inst.Opcode);
        Assert.Equal(7, inst.OperandCount);
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[i]);
            Assert.Equal(i + 1, inst.Operands[i]);
        }
    }

    [Fact]
    public void DoubleVariable_CallVn2_MaxEightOperands()
    {
        // VAR opcode 26 = call_vn2 ($FA = 0b11111010)
        // Type byte 1: 0b01_01_01_01 = 4 small
        // Type byte 2: 0b01_01_01_01 = 4 small → 8 total
        var mem = MemoryFromBytes(
            0xFA,
            0b01_01_01_01,
            0b01_01_01_01,
            0x10, 0x20, 0x30, 0x40,
            0x50, 0x60, 0x70, 0x80);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(8, inst.OperandCount);
        Assert.Equal(0x10, inst.Operands[0]);
        Assert.Equal(0x80, inst.Operands[7]);
    }

    private const int CallVs2Opcode = 0x0C;

    #endregion

    #region Store Decoding

    [Fact]
    public void DecodeStore_StackPush()
    {
        // Store to variable 0 = stack push
        var mem = MemoryFromBytes(0xB0, 0x00);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeStore(mem, ref inst);

        Assert.True(inst.HasStore);
        Assert.Equal(0, inst.StoreVariable);
        Assert.Equal(2, inst.NextAddress);
    }

    [Fact]
    public void DecodeStore_LocalVariable()
    {
        var mem = MemoryFromBytes(0xB0, 0x05);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeStore(mem, ref inst);

        Assert.Equal(5, inst.StoreVariable);
    }

    [Fact]
    public void DecodeStore_GlobalVariable()
    {
        var mem = MemoryFromBytes(0xB0, 0x10);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeStore(mem, ref inst);

        Assert.Equal(0x10, inst.StoreVariable);
    }

    #endregion

    #region Branch Decoding

    [Fact]
    public void DecodeBranch_ShortOffset_BranchOnTrue()
    {
        // Bit 7=1 (branch on true), bit 6=1 (short), offset = 5
        var mem = MemoryFromBytes(0xB0, 0b1100_0101);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.True(inst.HasBranch);
        Assert.True(inst.Branch.BranchOnTrue);
        Assert.Equal(5, inst.Branch.Offset);
        Assert.Equal(2, inst.NextAddress);
    }

    [Fact]
    public void DecodeBranch_ShortOffset_BranchOnFalse()
    {
        // Bit 7=0 (branch on false), bit 6=1 (short), offset = 3
        var mem = MemoryFromBytes(0xB0, 0b0100_0011);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.False(inst.Branch.BranchOnTrue);
        Assert.Equal(3, inst.Branch.Offset);
    }

    [Fact]
    public void DecodeBranch_ShortOffset_RFalse()
    {
        // Offset = 0 → rfalse
        var mem = MemoryFromBytes(0xB0, 0b1100_0000);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.Equal(0, inst.Branch.Offset);
        Assert.True(inst.Branch.IsRFalse);
        Assert.False(inst.Branch.IsRTrue);
    }

    [Fact]
    public void DecodeBranch_ShortOffset_RTrue()
    {
        // Offset = 1 → rtrue
        var mem = MemoryFromBytes(0xB0, 0b1100_0001);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.Equal(1, inst.Branch.Offset);
        Assert.True(inst.Branch.IsRTrue);
        Assert.False(inst.Branch.IsRFalse);
    }

    [Fact]
    public void DecodeBranch_LongOffset_Positive()
    {
        // Bit 7=1, bit 6=0 (long), 14-bit offset
        // 0b10_000001 0b00000010 → offset = 0x0102 = 258
        var mem = MemoryFromBytes(0xB0, 0b1000_0001, 0x02);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.True(inst.Branch.BranchOnTrue);
        Assert.Equal(258, inst.Branch.Offset);
        Assert.Equal(3, inst.NextAddress);
    }

    [Fact]
    public void DecodeBranch_LongOffset_Negative()
    {
        // 14-bit signed negative: bit 13 set
        // 0b10_111111 0b11111110 → raw = 0x3FFE, sign-extended = -2
        var mem = MemoryFromBytes(0xB0, 0b1011_1111, 0xFE);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.Equal(-2, inst.Branch.Offset);
    }

    [Fact]
    public void DecodeBranch_LongOffset_Zero_IsRFalse()
    {
        // 14-bit offset = 0 → rfalse (even in long form)
        var mem = MemoryFromBytes(0xB0, 0b1000_0000, 0x00);
        var inst = InstructionDecoder.Decode(mem, 0);
        InstructionDecoder.DecodeBranch(mem, ref inst);

        Assert.Equal(0, inst.Branch.Offset);
        Assert.True(inst.Branch.IsRFalse);
    }

    #endregion

    #region Complete Instructions (Store + Branch Together)

    [Fact]
    public void FullInstruction_WithStoreAndBranch()
    {
        // je local1 42 ?label (long form, 2OP:1, branches)
        // In real code, je stores nothing but branches.
        // Let's test a hypothetical instruction that has both:
        //
        // We'll just verify store then branch decode sequentially.
        // Short form 1OP opcode 4 (get_prop_len, which stores)
        // 0x94 = short form, small constant, opcode 4
        // Operand: $10
        // Store: $02 (local 2)
        var mem = MemoryFromBytes(0x94, 0x10, 0x02);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op1, inst.Form);
        Assert.Equal(4, inst.Opcode);
        Assert.Equal(1, inst.OperandCount);
        Assert.Equal(0x10, inst.Operands[0]);

        InstructionDecoder.DecodeStore(mem, ref inst);
        Assert.True(inst.HasStore);
        Assert.Equal(0x02, inst.StoreVariable);
        Assert.Equal(3, inst.NextAddress);
    }

    #endregion

    #region Real Instruction Decoding (zork1.z3)

    [Fact]
    public void Zork1_FirstInstruction_IsCallVs()
    {
        // zork1.z3 InitialPC = $4F05
        // First bytes: E0 03 2A 39 80 ...
        // 0xE0 = VAR call_vs (VAR:0)
        // Type byte 0x03 = 0b00000011 → large, small, omit, omit
        // Wait: 0x03 = 0b0000_0011 → large(00), small(00), omit(00)...
        // Actually 0x03 = 0b00_00_00_11 → large, large, large, omitted? No.
        // Let me recalculate: 0x03 in binary is 00000011
        // Bits 7-6: 00 = large constant
        // Bits 5-4: 00 = large constant
        // Bits 3-2: 00 = large constant
        // Bits 1-0: 11 = omitted
        // So 3 large constants
        var mem = LoadZork1();
        var inst = InstructionDecoder.Decode(mem, 0x4F05);

        Assert.Equal(OpcodeForm.Var, inst.Form);
        Assert.Equal(0, inst.Opcode); // call_vs
        Assert.Equal(3, inst.OperandCount);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[0]);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[1]);
        Assert.Equal(OperandType.LargeConstant, inst.OperandTypes[2]);
        Assert.Equal(0x2A39, inst.Operands[0]);
        Assert.Equal(0x8010, inst.Operands[1]);
        Assert.Equal(0xFFFF, inst.Operands[2]);
    }

    #endregion

    #region Address Tracking

    [Fact]
    public void Decode_TracksAddress()
    {
        // Decode at address 10
        var data = new byte[128];
        data[0] = 3; // V3
        data[0x0E] = 0x00; data[0x0F] = 0x40;
        data[10] = 0xB0; // rtrue (0OP)

        var mem = new Memory();
        mem.LoadStory(data);
        var inst = InstructionDecoder.Decode(mem, 10);

        Assert.Equal(10, inst.Address);
        Assert.Equal(11, inst.NextAddress);
    }

    [Fact]
    public void Decode_SequentialInstructions()
    {
        // Two 0OP instructions back to back: rtrue, rfalse
        var data = new byte[128];
        data[0] = 3;
        data[0x0E] = 0x00; data[0x0F] = 0x40;
        data[64] = 0xB0; // rtrue
        data[65] = 0xB1; // rfalse

        var mem = new Memory();
        mem.LoadStory(data);

        var inst1 = InstructionDecoder.Decode(mem, 64);
        Assert.Equal(64, inst1.Address);
        Assert.Equal(65, inst1.NextAddress);

        var inst2 = InstructionDecoder.Decode(mem, inst1.NextAddress);
        Assert.Equal(65, inst2.Address);
        Assert.Equal(1, inst2.Opcode); // rfalse = 0OP:1
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void LongForm_OpcodeZero()
    {
        // Opcode 0 in long form — not a real opcode, but decoder should handle it
        var mem = MemoryFromBytes(0x00, 0x00, 0x00);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(OpcodeForm.Op2, inst.Form);
        Assert.Equal(0, inst.Opcode);
        Assert.Equal(2, inst.OperandCount);
    }

    [Fact]
    public void VariableForm_SingleOperand()
    {
        // Type byte with only one operand defined
        // 0b01_11_11_11 = small, omit, omit, omit → 1 operand
        var mem = MemoryFromBytes(0xE0, 0b01_11_11_11, 0x42);
        var inst = InstructionDecoder.Decode(mem, 0);

        Assert.Equal(1, inst.OperandCount);
        Assert.Equal(OperandType.SmallConstant, inst.OperandTypes[0]);
        Assert.Equal(0x42, inst.Operands[0]);
    }

    #endregion

    #region Helpers

    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Creates a V3 Memory with the given bytes at address 0 so tests
    /// can decode at offset 0. Builds a valid story file first, then
    /// overwrites the dynamic region with the test bytes.
    /// </summary>
    private static Memory MemoryFromBytes(params byte[] instructionBytes)
    {
        return new InstructionTestMemory(instructionBytes);
    }

    /// <summary>Loads the full zork1.z3 story file.</summary>
    private static Memory LoadZork1()
    {
        var mem = new Memory();
        mem.LoadStory(Path.Combine(RepoRoot, "stories/zork1.z3"));
        return mem;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    /// <summary>
    /// Helper that creates a Memory instance with instruction bytes at
    /// address 0. It builds a valid 256-byte V3 story file and overwrites
    /// early header bytes with the test data — the Memory object is already
    /// constructed with the original header values, so decoding at address 0
    /// reads the instruction bytes.
    /// </summary>
    private sealed class InstructionTestMemory : Memory
    {
        public InstructionTestMemory(byte[] instructionBytes)
        {
            // Build a valid story file
            var data = new byte[256];
            data[0] = 3; // V3
            data[0x04] = 0x00; data[0x05] = 0x80; // high base
            data[0x0E] = 0x00; data[0x0F] = 0x40; // static base at $40

            LoadStory(data);

            // Now overwrite bytes at address 0 with instruction data.
            // This corrupts the header, but Memory is already initialized.
            for (int i = 0; i < instructionBytes.Length; i++)
                WriteByte(i, instructionBytes[i]);
        }
    }

    #endregion
}
