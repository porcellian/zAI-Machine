namespace ZMachine.Core;

/// <summary>
/// Implements Z-Machine control flow opcodes: routine calls, returns,
/// jumps, catch/throw, verify, and restart. These opcodes manipulate
/// the call stack and program counter.
/// </summary>
/// <remarks>
/// ZSpec S5 — Routines and the call stack.
/// ZSpec S15 — Instruction set (call variants, return variants, jump, etc.).
/// Quetzal S6.1–S6.2 — CATCH/THROW use frame count for stack unwinding.
/// </remarks>
public class ControlFlowOps
{
    private readonly Memory _memory;
    private readonly MachineState _state;
    private readonly int _version;
    private readonly ushort _routinesOffset;

    /// <summary>
    /// Creates control-flow opcode handlers bound to the given memory and
    /// machine state for a story of the given version.
    /// </summary>
    public ControlFlowOps(Memory memory, MachineState state, int version, ushort routinesOffset = 0)
    {
        _memory = memory;
        _state = state;
        _version = version;
        _routinesOffset = routinesOffset;
    }

    /// <summary>
    /// Calls a routine at the given packed address with arguments. Pushes
    /// a new call frame, reads the routine header, initialises locals, and
    /// sets PC to the first instruction.
    /// </summary>
    /// <remarks>
    /// ZSpec S5 — Routine header: byte 0 = local count (0–15). V1–4:
    /// followed by that many words giving initial local values. V5+:
    /// all locals initialise to 0. Arguments overwrite locals 1..n.
    ///
    /// ZSpec S15 — Calling address 0 is legal: the call does nothing
    /// and stores/returns 0 (false).
    /// </remarks>
    /// <returns>
    /// True if the call was executed (PC updated). False if packed address
    /// was 0 (caller should store 0 if this is a store-variant call).
    /// </returns>
    public bool Call(ushort packedAddress, ushort[] args, int argCount,
                     byte storeVariable, bool discardResult, int returnPC)
    {
        if (packedAddress == 0)
            return false;

        int routineAddr = AddressHelper.UnpackRoutineAddress(packedAddress, _version, _routinesOffset);
        int localCount = _memory.ReadByte(routineAddr);

        var frame = new CallFrame(returnPC, storeVariable, discardResult, localCount, argCount);

        // ZSpec S5 — V1–4: local initial values follow the count byte as words.
        int pc = routineAddr + 1;
        if (_version <= 4)
        {
            for (int i = 1; i <= localCount; i++)
            {
                frame.Locals[i] = _memory.ReadWord(pc);
                pc += 2;
            }
        }
        else
        {
            pc = routineAddr + 1;
        }

        // Arguments overwrite locals 1..argCount.
        for (int i = 0; i < argCount && i < localCount; i++)
            frame.Locals[i + 1] = args[i];

        _state.CallStack.PushFrame(frame);
        _state.PC = pc;
        return true;
    }

    /// <summary>
    /// Returns from the current routine with the given value. Pops the
    /// call frame, restores PC, and stores the result unless discarded.
    /// </summary>
    public void Return(ushort value)
    {
        var frame = _state.CallStack.PopFrame();

        _state.PC = frame.ReturnPC;

        if (!frame.DiscardResult)
            _state.StoreResult(frame.StoreVariable, value);
    }

    /// <summary>@rtrue — return 1 from current routine.</summary>
    public void ReturnTrue() => Return(1);

    /// <summary>@rfalse — return 0 from current routine.</summary>
    public void ReturnFalse() => Return(0);

    /// <summary>
    /// @ret_popped — pop the evaluation stack and return that value.
    /// </summary>
    public void ReturnPopped()
    {
        ushort value = _state.ReadVariable(0); // var 0 = pop
        Return(value);
    }

    /// <summary>
    /// @jump offset — unconditional jump. The offset is signed and
    /// relative to the address after the offset bytes.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@jump" — "The destination of the jump opcode is:
    /// Address after instruction + Offset - 2."
    /// </remarks>
    public void Jump(short offset, int addressAfterInstruction)
    {
        _state.PC = addressAfterInstruction + offset - 2;
    }

    /// <summary>@nop — no operation.</summary>
    public void Nop() { }

    /// <summary>
    /// @piracy — branch condition always true (interpreter is genuine).
    /// </summary>
    public bool Piracy() => true;

    /// <summary>
    /// @catch → frame count. Returns the current call stack depth for
    /// use with @throw.
    /// </summary>
    /// <remarks>Quetzal S6.1 — CATCH returns the current frame count.</remarks>
    public ushort Catch()
    {
        return (ushort)_state.CallStack.FrameCount;
    }

    /// <summary>
    /// @throw value frame-count — unwinds the call stack to the given
    /// frame count (as returned by @catch) and returns value from that
    /// frame.
    /// </summary>
    /// <remarks>
    /// Quetzal S6.2 — THROW unwinds to the frame at the given depth
    /// and performs a return with the specified value.
    /// </remarks>
    public void Throw(ushort value, ushort frameCount)
    {
        while (_state.CallStack.FrameCount > frameCount)
            _state.CallStack.PopFrame();

        Return(value);
    }

    /// <summary>
    /// @check_arg_count n — branches if the nth argument was supplied
    /// to the current routine (1-indexed).
    /// </summary>
    public bool CheckArgCount(ushort n)
    {
        var frame = _state.CallStack.CurrentFrame;
        if (frame == null)
            return false;
        return n <= frame.ArgumentCount;
    }

    /// <summary>
    /// @verify — computes the story file checksum from $40 to the
    /// header-declared file length and compares against the header
    /// checksum. Branches on match.
    /// </summary>
    public bool Verify()
    {
        ushort computed = _memory.ComputeChecksum();
        return computed == _memory.HeaderChecksum;
    }

    /// <summary>
    /// @restart — restores dynamic memory to its initial state and
    /// resets the call stack and PC. Returns the initial PC so the
    /// caller can begin execution from the start.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @restart: "Restart the game. (Any output streams
    /// which are selected remain selected.)"
    /// </remarks>
    public int Restart()
    {
        _memory.RestoreDynamicMemory();

        // Clear the call stack.
        while (_state.CallStack.FrameCount > 0)
            _state.CallStack.PopFrame();

        if (_version == 6)
        {
            // ZSpec S5 — V6: header $06 is a packed routine address.
            // Must enter the routine properly (read header, init locals,
            // push frame) rather than jumping to the raw address.
            ushort packed = _memory.ReadWord(0x06);
            Call(packed, [], 0, 0, true, 0);
            return _state.PC;
        }

        // V1–5/7/8: header $06 is a raw byte address of the first
        // instruction. Push a base frame so the eval stack and locals
        // are available immediately.
        int initialPC = _memory.ReadWord(0x06);
        _state.CallStack.PushFrame(new CallFrame(0, 0, false, 0, 0));
        _state.PC = initialPC;
        return initialPC;
    }

    /// <summary>@quit — signals the interpreter should halt.</summary>
    public void Quit()
    {
        // The actual halt is handled by the execution loop.
        // This method exists as a named opcode entry point.
    }
}
