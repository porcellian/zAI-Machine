using System.Text;

namespace ZMachine.Core;

/// <summary>
/// Interactive debugger engine for the Z-Machine interpreter. Provides
/// step-through execution (step into, step over, continue), breakpoints
/// (address and conditional), state inspection (call stack, locals,
/// globals, eval stack, memory), execution trace, and watch expressions.
/// </summary>
/// <remarks>
/// ZSpec S4–S6 — Instruction encoding, routines, variables.
/// This class wraps an <see cref="Interpreter"/> and drives its
/// <see cref="Interpreter.Step"/> method with debugger logic.
/// </remarks>
public class Debugger
{
    private readonly Interpreter _machine;
    private readonly Disassembler _disassembler;
    private readonly List<Breakpoint> _breakpoints = [];
    private readonly List<TraceEntry> _traceLog = [];
    private readonly List<WatchExpression> _watches = [];
    private int _maxTraceEntries = 1000;

    /// <summary>
    /// Creates a debugger wrapping the given interpreter.
    /// The interpreter must already be loaded with a story file.
    /// </summary>
    public Debugger(Interpreter machine)
    {
        _machine = machine;
        _disassembler = new Disassembler(machine.Memory);
    }

    /// <summary>Whether the debugger is currently paused.</summary>
    public bool IsPaused { get; private set; } = true;

    /// <summary>Whether the interpreter has stopped running.</summary>
    public bool IsStopped => !_machine.Running;

    /// <summary>The current program counter.</summary>
    public int PC => _machine.State.PC;

    /// <summary>Total instructions executed since last reset.</summary>
    public int InstructionsExecuted { get; private set; }

    /// <summary>The underlying interpreter.</summary>
    public Interpreter Machine => _machine;

    /// <summary>The disassembler for the loaded story.</summary>
    public Disassembler Disassembler => _disassembler;

    #region Execution Controls

    /// <summary>
    /// Executes a single instruction (Step Into). If the instruction is
    /// a call, enters the called routine.
    /// </summary>
    /// <returns>The stop reason after stepping.</returns>
    public StopReason StepInto()
    {
        if (IsStopped) return StopReason.Halted;

        IsPaused = false;
        ExecuteOneInstruction();
        IsPaused = true;

        return IsStopped ? StopReason.Halted : StopReason.Step;
    }

    /// <summary>
    /// Executes one instruction (Step Over). If the instruction is a call,
    /// runs until the call returns and breaks on the next instruction in
    /// the current routine.
    /// </summary>
    /// <returns>The stop reason after stepping.</returns>
    public StopReason StepOver(int instructionLimit = 10_000_000)
    {
        if (IsStopped) return StopReason.Halted;

        IsPaused = false;
        int depthBefore = _machine.State.CallStack.FrameCount;

        ExecuteOneInstruction();
        if (IsStopped) { IsPaused = true; return StopReason.Halted; }

        int depthAfter = _machine.State.CallStack.FrameCount;

        if (depthAfter > depthBefore)
        {
            // We entered a call — run until we return to original depth
            int executed = 0;
            while (!IsStopped && _machine.State.CallStack.FrameCount > depthBefore)
            {
                if (++executed > instructionLimit)
                {
                    IsPaused = true;
                    return StopReason.InstructionLimit;
                }

                var bp = CheckBreakpoints();
                if (bp != null)
                {
                    IsPaused = true;
                    return StopReason.Breakpoint;
                }

                ExecuteOneInstruction();
            }
        }

        IsPaused = true;
        return IsStopped ? StopReason.Halted : StopReason.Step;
    }

    /// <summary>
    /// Runs until a breakpoint is hit, the machine halts, or the
    /// instruction limit is reached.
    /// </summary>
    /// <returns>The stop reason.</returns>
    public StopReason Continue(int instructionLimit = 10_000_000)
    {
        if (IsStopped) return StopReason.Halted;

        IsPaused = false;
        int executed = 0;

        while (!IsStopped)
        {
            if (++executed > instructionLimit)
            {
                IsPaused = true;
                return StopReason.InstructionLimit;
            }

            var bp = CheckBreakpoints();
            if (bp != null)
            {
                IsPaused = true;
                return StopReason.Breakpoint;
            }

            ExecuteOneInstruction();
        }

        IsPaused = true;
        return StopReason.Halted;
    }

    /// <summary>
    /// Executes one instruction, records the trace, and updates watches.
    /// </summary>
    private void ExecuteOneInstruction()
    {
        int pcBefore = _machine.State.PC;
        DisassemblyLine? disLine = null;

        try
        {
            disLine = _disassembler.DisassembleAt(pcBefore);
        }
        catch { /* invalid instruction — will fail on step too */ }

        _machine.Step();
        InstructionsExecuted++;

        string mnemonic = disLine?.Mnemonic ?? "???";
        RecordTrace(pcBefore, mnemonic);
        UpdateWatches();
    }

    #endregion

    #region Breakpoints

    /// <summary>All currently set breakpoints.</summary>
    public IReadOnlyList<Breakpoint> Breakpoints => _breakpoints;

    /// <summary>
    /// Adds an address breakpoint. Execution pauses when PC equals
    /// the given address.
    /// </summary>
    public Breakpoint AddBreakpoint(int address)
    {
        var bp = new Breakpoint(address);
        _breakpoints.Add(bp);
        return bp;
    }

    /// <summary>
    /// Adds a conditional breakpoint that triggers when the given
    /// global variable equals the specified value.
    /// </summary>
    public Breakpoint AddConditionalBreakpoint(byte globalVar, ushort value)
    {
        var bp = new Breakpoint(globalVar, value);
        _breakpoints.Add(bp);
        return bp;
    }

    /// <summary>
    /// Adds an opcode breakpoint that triggers when the specified
    /// mnemonic is about to be executed.
    /// </summary>
    public Breakpoint AddOpcodeBreakpoint(string mnemonic)
    {
        var bp = new Breakpoint(mnemonic);
        _breakpoints.Add(bp);
        return bp;
    }

    /// <summary>Removes a breakpoint.</summary>
    public bool RemoveBreakpoint(Breakpoint bp) => _breakpoints.Remove(bp);

    /// <summary>Removes all breakpoints.</summary>
    public void ClearBreakpoints() => _breakpoints.Clear();

    /// <summary>
    /// Checks if any breakpoint is triggered at the current state.
    /// Returns the first triggered breakpoint, or null.
    /// </summary>
    private Breakpoint? CheckBreakpoints()
    {
        int pc = _machine.State.PC;

        foreach (var bp in _breakpoints)
        {
            if (!bp.Enabled) continue;

            switch (bp.Type)
            {
                case BreakpointType.Address when bp.Address == pc:
                    return bp;

                case BreakpointType.Conditional:
                {
                    ushort val = _machine.State.ReadVariable(
                        (byte)(bp.GlobalVariable + 16));
                    if (val == bp.ConditionValue)
                        return bp;
                    break;
                }

                case BreakpointType.Opcode:
                {
                    try
                    {
                        var line = _disassembler.DisassembleAt(pc);
                        if (string.Equals(line.Mnemonic, bp.Mnemonic,
                            StringComparison.OrdinalIgnoreCase))
                            return bp;
                    }
                    catch { /* skip if can't decode */ }
                    break;
                }
            }
        }

        return null;
    }

    #endregion

    #region State Inspection

    /// <summary>
    /// Returns the disassembled instruction at the current PC.
    /// </summary>
    public DisassemblyLine? GetCurrentInstruction()
    {
        try
        {
            return _disassembler.DisassembleAt(_machine.State.PC);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the current call stack as a list of frame snapshots,
    /// from the topmost (current) frame to the bottom.
    /// </summary>
    public List<FrameSnapshot> GetCallStack()
    {
        var frames = new List<FrameSnapshot>();
        foreach (var frame in _machine.State.CallStack.GetFramesBottomUp())
        {
            var locals = new ushort[frame.LocalCount];
            for (int i = 0; i < frame.LocalCount; i++)
                locals[i] = frame.Locals[i + 1];

            var evalStack = frame.EvalStack.ToArray();

            frames.Add(new FrameSnapshot(
                frame.ReturnPC,
                frame.StoreVariable,
                frame.DiscardResult,
                frame.LocalCount,
                frame.ArgumentCount,
                locals,
                evalStack));
        }

        frames.Reverse();
        return frames;
    }

    /// <summary>
    /// Returns the local variables of the current call frame.
    /// </summary>
    public LocalsSnapshot GetLocals()
    {
        var frame = _machine.State.CallStack.CurrentFrame;
        if (frame == null)
            return new LocalsSnapshot(0, []);

        var values = new ushort[frame.LocalCount];
        for (int i = 0; i < frame.LocalCount; i++)
            values[i] = frame.Locals[i + 1];

        return new LocalsSnapshot(frame.LocalCount, values);
    }

    /// <summary>
    /// Returns the values of all 240 global variables (G00–GEF).
    /// </summary>
    public ushort[] GetGlobals()
    {
        var globals = new ushort[240];
        for (int i = 0; i < 240; i++)
            globals[i] = _machine.State.ReadVariable((byte)(i + 16));

        return globals;
    }

    /// <summary>
    /// Returns the current frame's evaluation stack contents.
    /// </summary>
    public ushort[] GetEvalStack()
    {
        var frame = _machine.State.CallStack.CurrentFrame;
        return frame?.EvalStack.ToArray() ?? [];
    }

    /// <summary>
    /// Returns a hex dump of memory at the given address and length,
    /// with ASCII sidebar.
    /// </summary>
    public MemoryDump GetMemoryDump(int address, int length)
    {
        var memory = _machine.Memory;
        int fileLen = memory.FileLength > 0
            ? memory.FileLength
            : memory.OriginalBytes.Length;

        int actualLen = Math.Min(length, fileLen - address);
        if (actualLen <= 0)
            return new MemoryDump(address, [], "", memory.StaticBase);

        var bytes = new byte[actualLen];
        for (int i = 0; i < actualLen; i++)
            bytes[i] = memory.ReadByte(address + i);

        var sb = new StringBuilder();
        for (int row = 0; row < actualLen; row += 16)
        {
            sb.Append($"${address + row:X5}  ");

            int rowLen = Math.Min(16, actualLen - row);
            for (int col = 0; col < 16; col++)
            {
                if (col < rowLen)
                    sb.Append($"{bytes[row + col]:X2} ");
                else
                    sb.Append("   ");
                if (col == 7) sb.Append(' ');
            }

            sb.Append(" |");
            for (int col = 0; col < rowLen; col++)
            {
                byte b = bytes[row + col];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }
            sb.AppendLine("|");
        }

        return new MemoryDump(address, bytes, sb.ToString(), memory.StaticBase);
    }

    #endregion

    #region Execution Trace

    /// <summary>The execution trace log.</summary>
    public IReadOnlyList<TraceEntry> TraceLog => _traceLog;

    /// <summary>Whether trace logging is enabled.</summary>
    public bool TraceEnabled { get; set; } = true;

    /// <summary>Maximum trace entries to keep (rolling log).</summary>
    public int MaxTraceEntries
    {
        get => _maxTraceEntries;
        set => _maxTraceEntries = Math.Max(1, value);
    }

    /// <summary>Clears the trace log.</summary>
    public void ClearTrace() => _traceLog.Clear();

    /// <summary>
    /// Exports the trace log as plain text.
    /// </summary>
    public string ExportTrace()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution trace ({_traceLog.Count} entries):");
        sb.AppendLine(new string('-', 40));

        foreach (var entry in _traceLog)
            sb.AppendLine($"  ${entry.Address:X5}  {entry.Mnemonic}");

        return sb.ToString();
    }

    private void RecordTrace(int address, string mnemonic)
    {
        if (!TraceEnabled) return;

        _traceLog.Add(new TraceEntry(address, mnemonic, InstructionsExecuted));

        while (_traceLog.Count > _maxTraceEntries)
            _traceLog.RemoveAt(0);
    }

    #endregion

    #region Watch Expressions

    /// <summary>All currently defined watch expressions.</summary>
    public IReadOnlyList<WatchExpression> Watches => _watches;

    /// <summary>
    /// Adds a watch expression. Supported formats:
    /// "G00"–"GEF" for globals, "L00"–"L0E" for locals,
    /// "SP" for stack top, "[$1234]" for memory byte.
    /// </summary>
    public WatchExpression AddWatch(string expression)
    {
        var watch = new WatchExpression(expression);
        EvaluateWatch(watch);
        _watches.Add(watch);
        return watch;
    }

    /// <summary>Removes a watch expression.</summary>
    public bool RemoveWatch(WatchExpression watch) => _watches.Remove(watch);

    /// <summary>Removes all watch expressions.</summary>
    public void ClearWatches() => _watches.Clear();

    private void UpdateWatches()
    {
        foreach (var watch in _watches)
            EvaluateWatch(watch);
    }

    private void EvaluateWatch(WatchExpression watch)
    {
        try
        {
            string expr = watch.Expression.Trim();

            if (expr.Equals("SP", StringComparison.OrdinalIgnoreCase))
            {
                var frame = _machine.State.CallStack.CurrentFrame;
                if (frame != null && frame.EvalStack.Count > 0)
                    watch.Value = frame.EvalStack.Peek();
                else
                    watch.Value = null;
                watch.Error = null;
                return;
            }

            if (expr.StartsWith('G') && expr.Length == 3 &&
                byte.TryParse(expr[1..], System.Globalization.NumberStyles.HexNumber,
                    null, out byte gIdx) && gIdx <= 239)
            {
                watch.Value = _machine.State.ReadVariable((byte)(gIdx + 16));
                watch.Error = null;
                return;
            }

            if (expr.StartsWith('L') && expr.Length == 3 &&
                byte.TryParse(expr[1..], System.Globalization.NumberStyles.HexNumber,
                    null, out byte lIdx) && lIdx <= 14)
            {
                watch.Value = _machine.State.ReadVariable((byte)(lIdx + 1));
                watch.Error = null;
                return;
            }

            if (expr.StartsWith("[$") && expr.EndsWith(']'))
            {
                string addrStr = expr[2..^1];
                if (int.TryParse(addrStr, System.Globalization.NumberStyles.HexNumber,
                    null, out int addr))
                {
                    watch.Value = _machine.Memory.ReadByte(addr);
                    watch.Error = null;
                    return;
                }
            }

            if (expr.StartsWith('[') && expr.EndsWith(']'))
            {
                string addrStr = expr[1..^1];
                if (int.TryParse(addrStr, out int addr))
                {
                    watch.Value = _machine.Memory.ReadByte(addr);
                    watch.Error = null;
                    return;
                }
            }

            watch.Value = null;
            watch.Error = "Unknown expression format";
        }
        catch (Exception ex)
        {
            watch.Value = null;
            watch.Error = ex.Message;
        }
    }

    #endregion
}

/// <summary>Why execution stopped.</summary>
public enum StopReason
{
    /// <summary>Single step completed.</summary>
    Step,
    /// <summary>A breakpoint was hit.</summary>
    Breakpoint,
    /// <summary>The machine executed a quit instruction.</summary>
    Halted,
    /// <summary>Instruction limit reached.</summary>
    InstructionLimit
}

/// <summary>Type of breakpoint.</summary>
public enum BreakpointType
{
    /// <summary>Break at a specific address.</summary>
    Address,
    /// <summary>Break when a global variable equals a value.</summary>
    Conditional,
    /// <summary>Break when a specific opcode is about to execute.</summary>
    Opcode
}

/// <summary>
/// A debugger breakpoint — address, conditional (global == value),
/// or opcode-based.
/// </summary>
public class Breakpoint
{
    /// <summary>The breakpoint type.</summary>
    public BreakpointType Type { get; }

    /// <summary>Target address (for Address breakpoints).</summary>
    public int Address { get; }

    /// <summary>Global variable index 0–239 (for Conditional breakpoints).</summary>
    public byte GlobalVariable { get; }

    /// <summary>Value to match (for Conditional breakpoints).</summary>
    public ushort ConditionValue { get; }

    /// <summary>Opcode mnemonic (for Opcode breakpoints).</summary>
    public string Mnemonic { get; }

    /// <summary>Whether this breakpoint is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Creates an address breakpoint.</summary>
    public Breakpoint(int address)
    {
        Type = BreakpointType.Address;
        Address = address;
        Mnemonic = "";
    }

    /// <summary>Creates a conditional breakpoint (global == value).</summary>
    public Breakpoint(byte globalVar, ushort value)
    {
        Type = BreakpointType.Conditional;
        GlobalVariable = globalVar;
        ConditionValue = value;
        Mnemonic = "";
    }

    /// <summary>Creates an opcode breakpoint.</summary>
    public Breakpoint(string mnemonic)
    {
        Type = BreakpointType.Opcode;
        Mnemonic = mnemonic;
    }
}

/// <summary>
/// A snapshot of a call frame for debugger display.
/// </summary>
public record FrameSnapshot(
    int ReturnPC,
    byte StoreVariable,
    bool DiscardResult,
    int LocalCount,
    int ArgumentCount,
    ushort[] Locals,
    ushort[] EvalStack);

/// <summary>
/// Snapshot of the current frame's local variables.
/// </summary>
public record LocalsSnapshot(int Count, ushort[] Values);

/// <summary>
/// A hex dump of a memory region with formatted output.
/// </summary>
public record MemoryDump(
    int StartAddress,
    byte[] Data,
    string Formatted,
    int StaticBase);

/// <summary>
/// A single entry in the execution trace log.
/// </summary>
public record TraceEntry(int Address, string Mnemonic, int SequenceNumber);

/// <summary>
/// A user-defined watch expression that evaluates on each step.
/// </summary>
public class WatchExpression
{
    /// <summary>The expression string (e.g. "G00", "L02", "[$1234]").</summary>
    public string Expression { get; }

    /// <summary>The last evaluated value, or null if unavailable.</summary>
    public ushort? Value { get; set; }

    /// <summary>Error message if evaluation failed, or null.</summary>
    public string? Error { get; set; }

    /// <summary>Creates a watch expression.</summary>
    public WatchExpression(string expression) => Expression = expression;
}
