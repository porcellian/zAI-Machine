namespace ZMachine.Core;

/// <summary>
/// A decoded Z-Machine instruction. Contains the opcode number, operand
/// count classification, operand types and values, and optional store
/// and branch information.
/// </summary>
/// <remarks>
/// ZSpec S4 — Instruction encoding.
/// ZSpec11 "Operand evaluation" — Operands evaluated left to right.
/// </remarks>
public struct Instruction
{
    /// <summary>The opcode number (0–255 for normal, 0–255 for EXT).</summary>
    public int Opcode;

    /// <summary>The operand count classification of this instruction.</summary>
    public OpcodeForm Form;

    /// <summary>Number of operands (0–8).</summary>
    public int OperandCount;

    /// <summary>Operand types, indexed 0 to OperandCount-1.</summary>
    public OperandType[] OperandTypes;

    /// <summary>
    /// Raw operand values, indexed 0 to OperandCount-1.
    /// LargeConstant = 16-bit, SmallConstant = 8-bit (zero-extended),
    /// Variable = variable number (0=SP, 1–15=local, 16–255=global).
    /// </summary>
    public ushort[] Operands;

    /// <summary>True if this instruction stores a result.</summary>
    public bool HasStore;

    /// <summary>
    /// The variable to store the result in (0=SP push, 1–15=local,
    /// 16–255=global). Only valid when HasStore is true.
    /// </summary>
    public byte StoreVariable;

    /// <summary>True if this instruction has a branch.</summary>
    public bool HasBranch;

    /// <summary>Branch information. Only valid when HasBranch is true.</summary>
    public BranchInfo Branch;

    /// <summary>Byte address of this instruction in memory.</summary>
    public int Address;

    /// <summary>Byte address immediately after this instruction (next PC).</summary>
    public int NextAddress;

    /// <summary>True if this is an extended (EXT) opcode ($BE prefix, V5+).</summary>
    public bool IsExtended;
}

/// <summary>
/// Operand count classification of a Z-Machine instruction.
/// </summary>
/// <remarks>
/// ZSpec S4.3 — The opcode "form" determines how operand types are encoded,
/// but the operand count category (2OP/1OP/0OP/VAR) determines which
/// opcode table to look up. These are related but distinct concepts.
/// </remarks>
public enum OpcodeForm
{
    /// <summary>Two-operand instruction (2OP). Opcodes 0–31.</summary>
    Op2,
    /// <summary>One-operand instruction (1OP). Opcodes 128–143.</summary>
    Op1,
    /// <summary>Zero-operand instruction (0OP). Opcodes 176–191.</summary>
    Op0,
    /// <summary>Variable-operand instruction (VAR). Opcodes 224–255.</summary>
    Var,
    /// <summary>Extended instruction (EXT, $BE prefix, V5+). Opcodes 0–29+.</summary>
    Ext,
}

/// <summary>
/// The type of a single operand in a Z-Machine instruction.
/// </summary>
/// <remarks>ZSpec S4.2 — Operand type encoding in type bytes.</remarks>
public enum OperandType : byte
{
    /// <summary>Large constant: 16-bit value (2 bytes).</summary>
    LargeConstant = 0b00,
    /// <summary>Small constant: 8-bit value (1 byte).</summary>
    SmallConstant = 0b01,
    /// <summary>Variable: 8-bit variable number (1 byte).</summary>
    Variable = 0b10,
    /// <summary>Omitted: marks the end of the operand list in type bytes.</summary>
    Omitted = 0b11,
}

/// <summary>
/// Branch information decoded from an instruction.
/// </summary>
/// <remarks>ZSpec S4.7 — Branch offset encoding.</remarks>
public struct BranchInfo
{
    /// <summary>
    /// If true, branch is taken when the condition is true.
    /// If false, branch is taken when the condition is false.
    /// </summary>
    public bool BranchOnTrue;

    /// <summary>
    /// The raw branch offset. Special values: 0 = rfalse, 1 = rtrue.
    /// Otherwise the target address is: address_after_branch + offset - 2.
    /// </summary>
    public int Offset;

    /// <summary>True if offset is 0 (return false from current routine).</summary>
    public bool IsRFalse => Offset == 0;

    /// <summary>True if offset is 1 (return true from current routine).</summary>
    public bool IsRTrue => Offset == 1;
}
