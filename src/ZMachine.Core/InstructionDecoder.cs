namespace ZMachine.Core;

/// <summary>
/// Decodes Z-Machine instructions from memory at a given program counter.
/// Handles all four encoding forms (long, short, variable, extended) and
/// decodes operand types, store targets, and branch information.
/// </summary>
/// <remarks>
/// ZSpec S4 — Instruction encoding forms.
/// ZSpec S4.1 — Long form: top two bits 0b0x.
/// ZSpec S4.2 — Short form: top two bits 0b10.
/// ZSpec S4.3 — Variable form: top two bits 0b11.
/// ZSpec S4.4 — Extended form: first byte $BE (V5+).
/// ZSpec S4.5 — Store byte.
/// ZSpec S4.7 — Branch offset.
/// </remarks>
public static class InstructionDecoder
{
    /// <summary>
    /// Opcodes in the VAR range that use two operand-type bytes,
    /// allowing up to 8 operands.
    /// </summary>
    /// <remarks>
    /// ZSpec S4.4.3 — call_vs2 ($EC) and call_vn2 ($FA) are "double
    /// variable" forms with two type bytes.
    /// </remarks>
    private const byte CallVs2 = 0x0C; // VAR opcode 12
    private const byte CallVn2 = 0x1A; // VAR opcode 26

    /// <summary>
    /// Decodes the instruction at the given program counter address.
    /// Returns the decoded instruction with its NextAddress set to the
    /// byte immediately following the instruction.
    /// </summary>
    public static Instruction Decode(Memory memory, int pc)
    {
        var inst = new Instruction { Address = pc };
        int pos = pc;

        byte firstByte = memory.ReadByte(pos++);

        // ZSpec S4.4 — Extended form: first byte is $BE (V5+)
        if (firstByte == 0xBE)
        {
            inst.IsExtended = true;
            inst.Form = OpcodeForm.Ext;
            inst.Opcode = memory.ReadByte(pos++);

            // Extended opcodes always have a type byte (like VAR form)
            pos = DecodeVariableOperands(memory, pos, ref inst, isDouble: false);
        }
        // ZSpec S4.3 — Variable form: top two bits = 0b11
        else if ((firstByte & 0xC0) == 0xC0)
        {
            int opcodeNum = firstByte & 0x1F;

            // ZSpec S4.3 — Bit 5 determines 2OP vs VAR classification
            if ((firstByte & 0x20) == 0)
            {
                inst.Form = OpcodeForm.Op2;
                inst.Opcode = opcodeNum;
            }
            else
            {
                inst.Form = OpcodeForm.Var;
                inst.Opcode = opcodeNum;
            }

            // ZSpec S4.4.3 — Double-variable: call_vs2 and call_vn2
            bool isDouble = inst.Form == OpcodeForm.Var &&
                            (opcodeNum == CallVs2 || opcodeNum == CallVn2);

            pos = DecodeVariableOperands(memory, pos, ref inst, isDouble);
        }
        // ZSpec S4.2 — Short form: top two bits = 0b10
        else if ((firstByte & 0xC0) == 0x80)
        {
            int typeField = (firstByte >> 4) & 0x03;

            if (typeField == 0x03)
            {
                // ZSpec S4.2 — Bits 5,4 = 0b11 means 0OP
                inst.Form = OpcodeForm.Op0;
                inst.Opcode = firstByte & 0x0F;
                inst.OperandCount = 0;
                inst.OperandTypes = [];
                inst.Operands = [];
            }
            else
            {
                // 1OP with the type given by bits 5,4
                inst.Form = OpcodeForm.Op1;
                inst.Opcode = firstByte & 0x0F;
                inst.OperandCount = 1;

                var opType = (OperandType)typeField;
                inst.OperandTypes = [opType];
                inst.Operands = new ushort[1];
                pos = ReadOperandValue(memory, pos, opType, out inst.Operands[0]);
            }
        }
        // ZSpec S4.1 — Long form: top two bits = 0b0x (bit 7 clear)
        else
        {
            inst.Form = OpcodeForm.Op2;
            inst.Opcode = firstByte & 0x1F;
            inst.OperandCount = 2;
            inst.OperandTypes = new OperandType[2];
            inst.Operands = new ushort[2];

            // ZSpec S4.1 — Bit 6: 0=small constant, 1=variable for operand 1
            inst.OperandTypes[0] = (firstByte & 0x40) != 0
                ? OperandType.Variable
                : OperandType.SmallConstant;

            // ZSpec S4.1 — Bit 5: 0=small constant, 1=variable for operand 2
            inst.OperandTypes[1] = (firstByte & 0x20) != 0
                ? OperandType.Variable
                : OperandType.SmallConstant;

            pos = ReadOperandValue(memory, pos, inst.OperandTypes[0], out inst.Operands[0]);
            pos = ReadOperandValue(memory, pos, inst.OperandTypes[1], out inst.Operands[1]);
        }

        inst.NextAddress = pos;
        return inst;
    }

    /// <summary>
    /// Continues decoding after <see cref="Decode"/> by reading the store
    /// variable byte. Call this only for opcodes that store a result.
    /// Updates <paramref name="inst"/>.NextAddress.
    /// </summary>
    public static void DecodeStore(Memory memory, ref Instruction inst)
    {
        int pos = inst.NextAddress;
        inst.HasStore = true;
        inst.StoreVariable = memory.ReadByte(pos++);
        inst.NextAddress = pos;
    }

    /// <summary>
    /// Continues decoding after <see cref="Decode"/> (and optionally
    /// <see cref="DecodeStore"/>) by reading the branch offset. Call
    /// this only for opcodes that branch.
    /// Updates <paramref name="inst"/>.NextAddress.
    /// </summary>
    /// <remarks>
    /// ZSpec S4.7 — Branch offset encoding:
    /// Byte 1 bit 7 = branch on true (1) or false (0).
    /// Byte 1 bit 6 = 1: offset is bottom 6 bits (0–63), single byte.
    /// Byte 1 bit 6 = 0: offset is 14-bit signed from two bytes.
    /// Offset 0 = rfalse, 1 = rtrue.
    /// Otherwise target = address_after_branch + offset - 2.
    /// </remarks>
    public static void DecodeBranch(Memory memory, ref Instruction inst)
    {
        int pos = inst.NextAddress;
        inst.HasBranch = true;

        byte branchByte1 = memory.ReadByte(pos++);
        inst.Branch.BranchOnTrue = (branchByte1 & 0x80) != 0;

        if ((branchByte1 & 0x40) != 0)
        {
            // Single-byte offset: bottom 6 bits, unsigned
            inst.Branch.Offset = branchByte1 & 0x3F;
        }
        else
        {
            // Two-byte offset: 14-bit signed
            int highBits = branchByte1 & 0x3F;
            byte branchByte2 = memory.ReadByte(pos++);
            int raw = (highBits << 8) | branchByte2;

            // Sign-extend from 14 bits
            if ((raw & 0x2000) != 0)
                raw |= unchecked((int)0xFFFFC000);

            inst.Branch.Offset = raw;
        }

        inst.NextAddress = pos;
    }

    /// <summary>
    /// Decodes operands from one or two type bytes (variable/extended form).
    /// Each pair of bits in the type byte(s) specifies an operand type;
    /// 0b11 (Omitted) terminates the operand list.
    /// </summary>
    private static int DecodeVariableOperands(Memory memory, int pos, ref Instruction inst, bool isDouble)
    {
        byte typeByte1 = memory.ReadByte(pos++);
        byte typeByte2 = isDouble ? memory.ReadByte(pos++) : (byte)0xFF;

        // Combine type nibbles into an array (up to 4 or 8 operands)
        int maxOperands = isDouble ? 8 : 4;
        var types = new OperandType[maxOperands];
        int count = 0;

        count = ExtractOperandTypes(typeByte1, types, count, 0);
        if (isDouble && count == 4)
            count = ExtractOperandTypes(typeByte2, types, count, 4);

        inst.OperandCount = count;
        inst.OperandTypes = new OperandType[count];
        inst.Operands = new ushort[count];
        Array.Copy(types, inst.OperandTypes, count);

        for (int i = 0; i < count; i++)
            pos = ReadOperandValue(memory, pos, inst.OperandTypes[i], out inst.Operands[i]);

        return pos;
    }

    /// <summary>
    /// Extracts operand types from a single type byte, starting at the
    /// given index in the output array. Returns the new count.
    /// </summary>
    private static int ExtractOperandTypes(byte typeByte, OperandType[] types, int count, int startIndex)
    {
        for (int i = 0; i < 4; i++)
        {
            // Types are packed high-to-low: bits 7-6, 5-4, 3-2, 1-0
            var opType = (OperandType)((typeByte >> (6 - i * 2)) & 0x03);
            if (opType == OperandType.Omitted)
                break;
            types[startIndex + i] = opType;
            count++;
        }
        return count;
    }

    /// <summary>
    /// Reads a single operand value from memory according to its type.
    /// Returns the position after the operand.
    /// </summary>
    private static int ReadOperandValue(Memory memory, int pos, OperandType type, out ushort value)
    {
        switch (type)
        {
            case OperandType.LargeConstant:
                value = memory.ReadWord(pos);
                return pos + 2;

            case OperandType.SmallConstant:
            case OperandType.Variable:
                value = memory.ReadByte(pos);
                return pos + 1;

            default:
                value = 0;
                return pos;
        }
    }
}
