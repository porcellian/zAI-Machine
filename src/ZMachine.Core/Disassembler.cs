using System.Text;

namespace ZMachine.Core;

/// <summary>
/// Disassembles Z-Machine instructions from a loaded story file. Produces
/// decoded listings with mnemonics, operand types, store/branch targets,
/// inline text, routine detection, cross-references, and plain-text export.
/// </summary>
/// <remarks>
/// ZSpec S4 — Instruction encoding and operand types.
/// ZSpec S14/S15 — Opcode tables by form and version.
/// </remarks>
public class Disassembler
{
    private readonly Memory _memory;
    private readonly TextDecoder _textDecoder;
    private readonly int _version;

    /// <summary>
    /// Creates a disassembler for the given memory image.
    /// </summary>
    public Disassembler(Memory memory)
    {
        _memory = memory;
        _version = memory.Version;

        int abbrAddr = memory.ReadWord(0x18);
        int alphabetAddr = _version >= 5 ? memory.ReadWord(0x34) : 0;
        _textDecoder = new TextDecoder(memory, _version, abbrAddr, alphabetAddr);
    }

    /// <summary>
    /// Disassembles a single instruction at the given address and returns
    /// the fully decoded line with mnemonic, operands, store, branch, and
    /// inline text.
    /// </summary>
    public DisassemblyLine DisassembleAt(int address)
    {
        var inst = InstructionDecoder.Decode(_memory, address);
        string mnemonic = GetMnemonic(inst.Form, inst.Opcode);

        DecodeStoreAndBranch(ref inst);

        int rawEnd = inst.NextAddress;
        bool hasInlineText = IsInlineTextOpcode(inst.Form, inst.Opcode);
        string inlineText = "";

        if (hasInlineText)
        {
            var (text, byteLen) = _textDecoder.DecodeZString(inst.NextAddress);
            inlineText = text;
            rawEnd = inst.NextAddress + byteLen;
        }

        var rawBytes = ReadBytes(address, rawEnd);
        string operands = FormatOperands(inst);
        string store = FormatStore(inst);
        string branch = FormatBranch(inst);

        return new DisassemblyLine(
            address, rawEnd, rawBytes, mnemonic, operands,
            store, branch, inlineText, inst);
    }

    /// <summary>
    /// Disassembles a range of instructions starting at the given address,
    /// up to the specified count or until an invalid instruction is hit.
    /// </summary>
    public List<DisassemblyLine> DisassembleRange(int startAddress, int count)
    {
        var lines = new List<DisassemblyLine>();
        int addr = startAddress;
        int fileLen = _memory.FileLength > 0
            ? _memory.FileLength
            : _memory.OriginalBytes.Length;

        for (int i = 0; i < count && addr < fileLen; i++)
        {
            try
            {
                var line = DisassembleAt(addr);
                lines.Add(line);
                addr = line.EndAddress;
            }
            catch
            {
                break;
            }
        }

        return lines;
    }

    /// <summary>
    /// Disassembles an entire routine starting at the given byte address.
    /// Reads the local count header, then disassembles until a terminating
    /// instruction (rtrue, rfalse, ret, quit, jump with no fallthrough,
    /// or print_ret) is reached or the code runs into another routine.
    /// </summary>
    public RoutineDisassembly DisassembleRoutine(int routineAddress)
    {
        int pos = routineAddress;
        int localCount = _memory.ReadByte(pos++);
        if (localCount > 15)
            return new RoutineDisassembly(routineAddress, 0, [], []);

        var localDefaults = new ushort[localCount];
        if (_version <= 4)
        {
            for (int i = 0; i < localCount; i++)
            {
                localDefaults[i] = _memory.ReadWord(pos);
                pos += 2;
            }
        }

        int codeStart = pos;
        var lines = new List<DisassemblyLine>();
        int fileLen = _memory.FileLength > 0
            ? _memory.FileLength
            : _memory.OriginalBytes.Length;
        int maxInstructions = 10000;

        for (int i = 0; i < maxInstructions && pos < fileLen; i++)
        {
            try
            {
                var line = DisassembleAt(pos);
                lines.Add(line);
                pos = line.EndAddress;

                if (IsTerminator(line.Instruction))
                    break;
            }
            catch
            {
                break;
            }
        }

        return new RoutineDisassembly(routineAddress, localCount,
            localDefaults, lines);
    }

    /// <summary>
    /// Scans for routine boundaries in the code region by looking for
    /// valid routine headers (local count 0–15, optionally followed by
    /// initial values for V1–4).
    /// </summary>
    public List<RoutineInfo> DetectRoutines()
    {
        var routines = new List<RoutineInfo>();
        int highBase = _memory.HighBase;
        int fileLen = _memory.FileLength > 0
            ? _memory.FileLength
            : _memory.OriginalBytes.Length;

        // The initial PC marks the first routine
        int initialPC = _memory.ReadWord(0x06);
        if (_version == 6)
        {
            ushort routinesOffset = _memory.ReadWord(0x28);
            initialPC = AddressHelper.UnpackRoutineAddress(
                (ushort)initialPC, _version, routinesOffset);
        }

        var knownAddresses = new HashSet<int>();

        // Start with initial PC's routine header
        if (initialPC > 0)
        {
            int routineStart = _version == 6 ? initialPC : FindRoutineHeader(initialPC);
            if (routineStart > 0)
                knownAddresses.Add(routineStart);
        }

        // Scan from high memory for routine-like patterns
        for (int addr = highBase; addr < fileLen - 1; addr++)
        {
            byte localCount = _memory.ReadByte(addr);
            if (localCount > 15) continue;

            int headerSize = 1 + (_version <= 4 ? localCount * 2 : 0);
            if (addr + headerSize >= fileLen) continue;

            // Heuristic: the byte before a routine header is often 0x00
            // (padding), or this is right at a known boundary.
            if (addr > highBase && _memory.ReadByte(addr - 1) != 0x00
                && !knownAddresses.Contains(addr))
                continue;

            knownAddresses.Add(addr);
        }

        // Also find routines via call targets in disassembled code
        var callTargets = new HashSet<int>();
        foreach (int routineAddr in knownAddresses)
        {
            var dis = DisassembleRoutine(routineAddr);
            foreach (var line in dis.Instructions)
                CollectCallTargets(line.Instruction, callTargets);
        }

        foreach (int target in callTargets)
        {
            int rAddr = FindRoutineHeader(target);
            if (rAddr > 0)
                knownAddresses.Add(rAddr);
        }

        // Build routine info list
        foreach (int addr in knownAddresses.OrderBy(a => a))
        {
            byte lc = _memory.ReadByte(addr);
            if (lc > 15) continue;
            routines.Add(new RoutineInfo(addr, lc));
        }

        return routines;
    }

    /// <summary>
    /// Builds a cross-reference map: for each routine address, lists the
    /// addresses of call instructions that reference it.
    /// </summary>
    public Dictionary<int, List<int>> BuildCrossReferences(
        List<RoutineInfo> routines)
    {
        var xrefs = new Dictionary<int, List<int>>();
        foreach (var routine in routines)
            xrefs[routine.Address] = [];

        foreach (var routine in routines)
        {
            var dis = DisassembleRoutine(routine.Address);
            foreach (var line in dis.Instructions)
            {
                int target = GetCallTarget(line.Instruction);
                if (target > 0)
                {
                    int rAddr = FindRoutineHeader(target);
                    if (rAddr > 0 && xrefs.ContainsKey(rAddr))
                        xrefs[rAddr].Add(line.Address);
                }
            }
        }

        return xrefs;
    }

    /// <summary>
    /// Exports a full disassembly listing as plain text.
    /// </summary>
    public string Export(int startAddress, int instructionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Z-Machine V{_version} Disassembly");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        var lines = DisassembleRange(startAddress, instructionCount);
        foreach (var line in lines)
        {
            sb.Append($"  ${line.Address:X5}  ");
            sb.Append($"{line.RawBytesHex,-20} ");
            sb.Append($"{line.Mnemonic,-16} ");
            sb.Append(line.Operands);

            if (!string.IsNullOrEmpty(line.Store))
                sb.Append($" {line.Store}");
            if (!string.IsNullOrEmpty(line.Branch))
                sb.Append($" {line.Branch}");
            if (!string.IsNullOrEmpty(line.InlineText))
                sb.Append($" \"{line.InlineText}\"");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    #region Opcode Tables

    // ZSpec S14/S15 — Opcode mnemonic tables

    /// <summary>
    /// Returns the mnemonic for the given opcode form and number.
    /// </summary>
    public string GetMnemonic(OpcodeForm form, int opcode) => form switch
    {
        OpcodeForm.Op2 => Get2OPMnemonic(opcode),
        OpcodeForm.Op1 => Get1OPMnemonic(opcode),
        OpcodeForm.Op0 => Get0OPMnemonic(opcode),
        OpcodeForm.Var => GetVARMnemonic(opcode),
        OpcodeForm.Ext => GetEXTMnemonic(opcode),
        _ => $"UNKNOWN_{form}:{opcode}"
    };

    private string Get2OPMnemonic(int op) => op switch
    {
        1 => "je", 2 => "jl", 3 => "jg",
        4 => "dec_chk", 5 => "inc_chk",
        6 => "jin", 7 => "test",
        8 => "or", 9 => "and",
        10 => "test_attr", 11 => "set_attr", 12 => "clear_attr",
        13 => "store", 14 => "insert_obj",
        15 => "loadw", 16 => "loadb",
        17 => "get_prop", 18 => "get_prop_addr", 19 => "get_next_prop",
        20 => "add", 21 => "sub", 22 => "mul", 23 => "div", 24 => "mod",
        25 => "call_2s", 26 => "call_2n",
        27 => "set_colour", 28 => "throw",
        _ => $"2OP:{op}"
    };

    private string Get1OPMnemonic(int op) => op switch
    {
        0 => "jz",
        1 => "get_sibling", 2 => "get_child", 3 => "get_parent",
        4 => "get_prop_len",
        5 => "inc", 6 => "dec",
        7 => "print_addr",
        8 => _version >= 4 ? "call_1s" : $"1OP:{op}",
        9 => "remove_obj", 10 => "print_obj",
        11 => "ret", 12 => "jump",
        13 => "print_paddr",
        14 => "load",
        15 => _version >= 5 ? "call_1n" : "not",
        _ => $"1OP:{op}"
    };

    private string Get0OPMnemonic(int op) => op switch
    {
        0 => "rtrue", 1 => "rfalse",
        2 => "print", 3 => "print_ret",
        4 => "nop",
        5 => _version <= 4 ? "save" : $"0OP:{op}",
        6 => _version <= 4 ? "restore" : $"0OP:{op}",
        7 => "restart", 8 => "ret_popped",
        9 => _version >= 5 ? "catch" : "pop",
        10 => "quit", 11 => "new_line",
        12 => "show_status",
        13 => "verify",
        15 => "piracy",
        _ => $"0OP:{op}"
    };

    private string GetVARMnemonic(int op) => op switch
    {
        0 => "call_vs",
        1 => "storew", 2 => "storeb", 3 => "put_prop",
        4 => _version >= 5 ? "aread" : "read",
        5 => "print_char", 6 => "print_num",
        7 => "random", 8 => "push", 9 => "pull",
        10 => "split_window", 11 => "set_window",
        12 => "call_vs2",
        13 => "erase_window", 14 => "erase_line",
        15 => "set_cursor", 16 => "get_cursor",
        17 => "set_text_style", 18 => "buffer_mode",
        19 => "output_stream", 20 => "input_stream",
        21 => "sound_effect",
        22 => "read_char",
        23 => "scan_table",
        24 => _version >= 5 ? "not" : $"VAR:{op}",
        25 => "call_vn", 26 => "call_vn2",
        27 => "tokenise", 28 => "encode_text",
        29 => "copy_table", 30 => "print_table",
        31 => "check_arg_count",
        _ => $"VAR:{op}"
    };

    private static string GetEXTMnemonic(int op) => op switch
    {
        0 => "save", 1 => "restore",
        2 => "log_shift", 3 => "art_shift",
        4 => "set_font",
        5 => "draw_picture", 6 => "picture_data",
        7 => "erase_picture", 8 => "set_margins",
        9 => "save_undo", 10 => "restore_undo",
        11 => "print_unicode", 12 => "check_unicode",
        13 => "set_true_colour",
        16 => "move_window", 17 => "window_size",
        18 => "window_style", 19 => "get_wind_prop",
        20 => "put_wind_prop", 21 => "scroll_window",
        22 => "mouse_window",
        23 => "read_mouse",
        _ => $"EXT:{op}"
    };

    #endregion

    #region Store/Branch Tables

    /// <summary>
    /// Decodes store and/or branch bytes following the operands,
    /// based on which opcode is being disassembled.
    /// </summary>
    private void DecodeStoreAndBranch(ref Instruction inst)
    {
        switch (inst.Form)
        {
            case OpcodeForm.Op2:
                switch (inst.Opcode)
                {
                    case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 10:
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                    case 8: case 9: case 15: case 16: case 17: case 18: case 19:
                    case 20: case 21: case 22: case 23: case 24: case 25:
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                }
                break;

            case OpcodeForm.Op1:
                switch (inst.Opcode)
                {
                    case 0:
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                    case 1: case 2:
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                    case 3: case 4: case 8: case 14:
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 15:
                        if (_version <= 4)
                            InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                }
                break;

            case OpcodeForm.Op0:
                switch (inst.Opcode)
                {
                    case 5: case 6:
                        if (_version <= 3)
                            InstructionDecoder.DecodeBranch(_memory, ref inst);
                        else if (_version == 4)
                            InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 9:
                        if (_version >= 5)
                            InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 13: case 15:
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                }
                break;

            case OpcodeForm.Var:
                switch (inst.Opcode)
                {
                    case 0: case 7: case 12: case 22:
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 4:
                        if (_version >= 5)
                            InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 23:
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                    case 24:
                        if (_version >= 5)
                            InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                    case 31:
                        InstructionDecoder.DecodeBranch(_memory, ref inst);
                        break;
                }
                break;

            case OpcodeForm.Ext:
                switch (inst.Opcode)
                {
                    case 0: case 1: case 2: case 3: case 4:
                    case 9: case 10: case 12:
                    case 19: // get_wind_prop (store)
                        InstructionDecoder.DecodeStore(_memory, ref inst);
                        break;
                }
                break;
        }
    }

    #endregion

    #region Formatting

    /// <summary>Formats operands with type annotations.</summary>
    private static string FormatOperands(Instruction inst)
    {
        if (inst.OperandCount == 0) return "";

        var parts = new string[inst.OperandCount];
        for (int i = 0; i < inst.OperandCount; i++)
        {
            parts[i] = inst.OperandTypes[i] switch
            {
                OperandType.LargeConstant => $"#{inst.Operands[i]:X4}",
                OperandType.SmallConstant => $"#{inst.Operands[i]:X2}",
                OperandType.Variable => FormatVariable(inst.Operands[i]),
                _ => "?"
            };
        }

        return string.Join(", ", parts);
    }

    /// <summary>Formats a variable reference: SP, L00–L0E, G00–GEF.</summary>
    private static string FormatVariable(ushort varNum) => varNum switch
    {
        0 => "SP",
        <= 15 => $"L{varNum - 1:X2}",
        _ => $"G{varNum - 16:X2}"
    };

    /// <summary>Formats the store target.</summary>
    private static string FormatStore(Instruction inst)
    {
        if (!inst.HasStore) return "";
        return $"-> {FormatVariable(inst.StoreVariable)}";
    }

    /// <summary>Formats the branch target.</summary>
    private static string FormatBranch(Instruction inst)
    {
        if (!inst.HasBranch) return "";
        string cond = inst.Branch.BranchOnTrue ? "?" : "?~";
        if (inst.Branch.IsRFalse) return $"{cond}FALSE";
        if (inst.Branch.IsRTrue) return $"{cond}TRUE";
        int target = inst.NextAddress + inst.Branch.Offset - 2;
        return $"{cond}${target:X5}";
    }

    /// <summary>Reads raw bytes from memory as a hex string.</summary>
    private string ReadBytes(int start, int end)
    {
        int len = end - start;
        if (len > 16) len = 16;
        var sb = new StringBuilder(len * 3);
        for (int i = 0; i < len; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(_memory.ReadByte(start + i).ToString("X2"));
        }
        if (end - start > 16) sb.Append("..");
        return sb.ToString();
    }

    #endregion

    #region Instruction Classification

    /// <summary>Returns true if the opcode has inline text (print, print_ret).</summary>
    private static bool IsInlineTextOpcode(OpcodeForm form, int opcode)
        => form == OpcodeForm.Op0 && opcode is 2 or 3;

    /// <summary>
    /// Returns true if the instruction terminates a routine
    /// (rtrue, rfalse, ret, ret_popped, quit, print_ret).
    /// </summary>
    private static bool IsTerminator(Instruction inst)
    {
        if (inst.Form == OpcodeForm.Op0)
            return inst.Opcode is 0 or 1 or 3 or 7 or 8 or 10;
        if (inst.Form == OpcodeForm.Op1 && inst.Opcode == 11) // ret
            return true;
        if (inst.Form == OpcodeForm.Op1 && inst.Opcode == 12) // jump
            return true;
        return false;
    }

    /// <summary>
    /// Returns true if the opcode is a call instruction.
    /// </summary>
    private static bool IsCallOpcode(OpcodeForm form, int opcode) =>
        (form == OpcodeForm.Var && opcode is 0 or 12 or 25 or 26) ||
        (form == OpcodeForm.Op2 && opcode is 25 or 26) ||
        (form == OpcodeForm.Op1 && opcode is 8 or 15);

    /// <summary>
    /// Extracts the packed call target address from a call instruction,
    /// or returns 0.
    /// </summary>
    private int GetCallTarget(Instruction inst)
    {
        if (!IsCallOpcode(inst.Form, inst.Opcode)) return 0;
        if (inst.OperandCount == 0) return 0;
        if (inst.OperandTypes[0] != OperandType.LargeConstant
            && inst.OperandTypes[0] != OperandType.SmallConstant)
            return 0;

        ushort packed = inst.Operands[0];
        if (packed == 0) return 0;

        ushort routinesOffset = _version is 6 or 7
            ? _memory.ReadWord(0x28) : (ushort)0;
        return AddressHelper.UnpackRoutineAddress(packed, _version,
            routinesOffset);
    }

    /// <summary>
    /// Collects packed call targets from an instruction into a set.
    /// </summary>
    private void CollectCallTargets(Instruction inst, HashSet<int> targets)
    {
        int target = GetCallTarget(inst);
        if (target > 0)
            targets.Add(target);
    }

    /// <summary>
    /// Finds the routine header address for code that starts at the
    /// given byte address. The header is at address - 1 (V5+) or
    /// address - 1 - locals*2 (V1-4).
    /// </summary>
    private int FindRoutineHeader(int codeAddress)
    {
        if (codeAddress <= 0) return 0;

        // The routine header is at codeAddress - headerSize.
        // For V5+: header = 1 byte (local count), no defaults.
        // For V1-4: header = 1 + localCount * 2.
        // But we know the code address, not the header address.
        // For detected routines, the packed address points to the header.
        // Just return the address if the byte there is a valid local count.
        if (codeAddress < _memory.OriginalBytes.Length)
        {
            byte lc = _memory.ReadByte(codeAddress);
            if (lc <= 15)
                return codeAddress;
        }

        return 0;
    }

    #endregion
}

/// <summary>
/// A single disassembled instruction line with address, raw bytes,
/// mnemonic, formatted operands, store/branch targets, and inline text.
/// </summary>
public record DisassemblyLine(
    int Address,
    int EndAddress,
    string RawBytesHex,
    string Mnemonic,
    string Operands,
    string Store,
    string Branch,
    string InlineText,
    Instruction Instruction);

/// <summary>
/// A disassembled routine with its header address, local count,
/// local default values, and instruction listing.
/// </summary>
public record RoutineDisassembly(
    int Address,
    int LocalCount,
    ushort[] LocalDefaults,
    List<DisassemblyLine> Instructions);

/// <summary>
/// Metadata for a detected routine: its header address and local count.
/// </summary>
public record RoutineInfo(int Address, int LocalCount);
