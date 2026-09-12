namespace ZMachine.Core;

/// <summary>
/// Implements Z-Machine variable manipulation, memory read/write, and
/// table opcodes. Variable opcodes that take a variable number as an
/// operand use indirect semantics (stack pointer reads/writes in place).
/// </summary>
/// <remarks>
/// ZSpec S15 — Variable, memory, and table instructions.
/// ZSpec11 "Indirect variable references" — @load, @store, @inc, @dec,
/// @inc_chk, @dec_chk, and @pull use indirect references: variable 0
/// peeks/replaces the stack top instead of pushing/popping.
/// </remarks>
public static class VariableMemoryOps
{
    /// <summary>
    /// @load (indirect variable) — reads a variable by number.
    /// Variable 0 peeks the stack (does not pop).
    /// </summary>
    public static ushort Load(MachineState state, byte variable)
    {
        return state.ReadVariableIndirect(variable);
    }

    /// <summary>
    /// @store (indirect variable) — writes a value to a variable by number.
    /// Variable 0 replaces the stack top (does not push).
    /// </summary>
    public static void Store(MachineState state, byte variable, ushort value)
    {
        state.WriteVariableIndirect(variable, value);
    }

    /// <summary>
    /// @inc (indirect variable) — increments a variable as signed 16-bit.
    /// </summary>
    public static void Inc(MachineState state, byte variable)
    {
        ushort val = state.ReadVariableIndirect(variable);
        state.WriteVariableIndirect(variable, (ushort)((short)val + 1));
    }

    /// <summary>
    /// @dec (indirect variable) — decrements a variable as signed 16-bit.
    /// </summary>
    public static void Dec(MachineState state, byte variable)
    {
        ushort val = state.ReadVariableIndirect(variable);
        state.WriteVariableIndirect(variable, (ushort)((short)val - 1));
    }

    /// <summary>
    /// @inc_chk (indirect variable) — increments a variable and returns
    /// true if the new signed value is greater than the threshold.
    /// </summary>
    public static bool IncChk(MachineState state, byte variable, short threshold)
    {
        ushort val = state.ReadVariableIndirect(variable);
        short newVal = (short)((short)val + 1);
        state.WriteVariableIndirect(variable, (ushort)newVal);
        return newVal > threshold;
    }

    /// <summary>
    /// @dec_chk (indirect variable) — decrements a variable and returns
    /// true if the new signed value is less than the threshold.
    /// </summary>
    public static bool DecChk(MachineState state, byte variable, short threshold)
    {
        ushort val = state.ReadVariableIndirect(variable);
        short newVal = (short)((short)val - 1);
        state.WriteVariableIndirect(variable, (ushort)newVal);
        return newVal < threshold;
    }

    /// <summary>
    /// @push — pushes a value onto the evaluation stack.
    /// </summary>
    public static void Push(MachineState state, ushort value)
    {
        state.WriteVariable(0, value);
    }

    /// <summary>
    /// @pull (V1-5, indirect) — pops from the stack and writes to the
    /// given variable. Variable 0 replaces the stack top (indirect).
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Indirect variable references" — @pull uses indirect
    /// semantics. In V6, @pull stores to the result variable instead.
    /// </remarks>
    public static void Pull(MachineState state, byte variable)
    {
        ushort val = state.ReadVariable(0); // pop
        state.WriteVariableIndirect(variable, val);
    }

    /// <summary>
    /// @loadw — reads a 16-bit word from memory at array+2*index.
    /// </summary>
    /// <remarks>ZSpec S15 — @loadw array word-index → (result)</remarks>
    public static ushort LoadWord(Memory memory, ushort array, ushort wordIndex)
    {
        int address = array + 2 * (int)wordIndex;
        return memory.ReadWord(address);
    }

    /// <summary>
    /// @loadb — reads an 8-bit byte from memory at array+index.
    /// </summary>
    /// <remarks>ZSpec S15 — @loadb array byte-index → (result)</remarks>
    public static ushort LoadByte(Memory memory, ushort array, ushort byteIndex)
    {
        int address = array + (int)byteIndex;
        return memory.ReadByte(address);
    }

    /// <summary>
    /// @storew — writes a 16-bit word to memory at array+2*index.
    /// </summary>
    /// <remarks>ZSpec S15 — @storew array word-index value</remarks>
    public static void StoreWord(Memory memory, ushort array, ushort wordIndex, ushort value)
    {
        int address = array + 2 * (int)wordIndex;
        memory.WriteWord(address, value);
    }

    /// <summary>
    /// @storeb — writes an 8-bit byte to memory at array+index.
    /// </summary>
    /// <remarks>ZSpec S15 — @storeb array byte-index value</remarks>
    public static void StoreByte(Memory memory, ushort array, ushort byteIndex, byte value)
    {
        int address = array + (int)byteIndex;
        memory.WriteByte(address, value);
    }

    /// <summary>
    /// @scan_table — searches a table for a value. Returns the address
    /// of the matching entry, or 0 if not found. Sets the branch condition
    /// to whether the entry was found.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@scan_table" — form byte: bit 7 = 1 for word entries
    /// (default), 0 for byte entries. Bottom 7 bits = entry length
    /// (default 2 for words, 1 for bytes if form is omitted).
    /// The search compares the first word (or byte) of each entry.
    /// </remarks>
    public static (ushort Address, bool Found) ScanTable(
        Memory memory, ushort x, ushort table, ushort len, ushort form)
    {
        bool isWord = (form & 0x80) != 0;
        int entryLen = form & 0x7F;

        int addr = table;
        for (int i = 0; i < len; i++)
        {
            if (isWord)
            {
                ushort val = memory.ReadWord(addr);
                if (val == x)
                    return ((ushort)addr, true);
            }
            else
            {
                byte val = memory.ReadByte(addr);
                if (val == (byte)x)
                    return ((ushort)addr, true);
            }
            addr += entryLen;
        }

        return (0, false);
    }

    /// <summary>
    /// @copy_table — copies or zeroes a table in memory.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @copy_table first second size:
    ///   - second = 0: zero the first table (size bytes).
    ///   - size > 0: copy forwards or backwards to avoid corruption
    ///     when tables overlap (implementation copies backward if
    ///     second > first, forward otherwise).
    ///   - size &lt; 0: always copy forward (abs(size) bytes), which
    ///     allows deliberate overlapping copy for fill patterns.
    /// </remarks>
    public static void CopyTable(Memory memory, ushort first, ushort second, short size)
    {
        if (second == 0)
        {
            int absSize = Math.Abs(size);
            for (int i = 0; i < absSize; i++)
                memory.WriteByte(first + i, 0);
            return;
        }

        int byteCount;
        bool forceForward;

        if (size < 0)
        {
            byteCount = -size;
            forceForward = true;
        }
        else
        {
            byteCount = size;
            forceForward = false;
        }

        if (forceForward || second <= first)
        {
            for (int i = 0; i < byteCount; i++)
                memory.WriteByte(second + i, memory.ReadByte(first + i));
        }
        else
        {
            // Copy backward to avoid corruption when second > first and ranges overlap.
            for (int i = byteCount - 1; i >= 0; i--)
                memory.WriteByte(second + i, memory.ReadByte(first + i));
        }
    }
}
