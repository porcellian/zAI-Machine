namespace ZMachine.Core;

/// <summary>
/// Runtime execution state of the Z-Machine: program counter, call stack
/// with per-frame evaluation stacks and local variables, and global
/// variable access through memory.
/// </summary>
/// <remarks>
/// ZSpec S5 — Call stack and routines.
/// ZSpec S6.3 — The stack: variable 0 refers to the top of the current
/// frame's evaluation stack. Reading pops, writing pushes, except in
/// indirect references.
/// ZSpec S6.4 — Global variables are stored in a 240-word table in memory.
/// </remarks>
public class MachineState
{
    private readonly Memory _memory;
    private readonly int _globalsAddress;

    /// <summary>The current program counter (byte address).</summary>
    public int PC { get; set; }

    /// <summary>The call stack of routine frames.</summary>
    public CallStack CallStack { get; } = new();

    /// <summary>
    /// Creates a new machine state bound to the given memory.
    /// </summary>
    /// <param name="memory">The story file memory.</param>
    /// <param name="globalsAddress">
    /// Byte address of the global variables table (header word $0C).
    /// Globals 16–255 are stored as consecutive words starting here.
    /// </param>
    public MachineState(Memory memory, int globalsAddress)
    {
        _memory = memory;
        _globalsAddress = globalsAddress;
    }

    /// <summary>
    /// The current call frame's evaluation stack. Convenience accessor.
    /// </summary>
    private Stack<ushort> EvalStack => CurrentFrameRequired.EvalStack;

    /// <summary>
    /// Returns the current frame, throwing if there is none.
    /// </summary>
    private CallFrame CurrentFrameRequired =>
        CallStack.CurrentFrame ??
        throw new InvalidOperationException("No active call frame — push a frame before executing instructions.");

    /// <summary>
    /// Reads the value of a variable.
    /// </summary>
    /// <remarks>
    /// ZSpec S6.3 — Variable 0 = stack (pop), 1–15 = locals, 16–255 = globals.
    /// </remarks>
    public ushort ReadVariable(byte variable)
    {
        if (variable == 0)
        {
            var stack = EvalStack;
            if (stack.Count == 0)
                throw new InvalidOperationException("Stack underflow: attempted to pop from an empty evaluation stack.");
            return stack.Pop();
        }

        if (variable <= 15)
        {
            var frame = CurrentFrameRequired;
            if (variable > frame.LocalCount)
                throw new InvalidOperationException(
                    $"Read from local variable {variable} but routine only has {frame.LocalCount} locals.");
            return frame.Locals[variable];
        }

        // ZSpec S6.4 — Global variable g (16–255) at globals_address + 2*(g-16)
        int globalIndex = variable - 16;
        return _memory.ReadWord(_globalsAddress + globalIndex * 2);
    }

    /// <summary>
    /// Writes a value to a variable.
    /// </summary>
    /// <remarks>
    /// ZSpec S6.3 — Variable 0 = stack (push), 1–15 = locals, 16–255 = globals.
    /// </remarks>
    public void WriteVariable(byte variable, ushort value)
    {
        if (variable == 0)
        {
            EvalStack.Push(value);
            return;
        }

        if (variable <= 15)
        {
            var frame = CurrentFrameRequired;
            if (variable > frame.LocalCount)
                throw new InvalidOperationException(
                    $"Write to local variable {variable} but routine only has {frame.LocalCount} locals.");
            frame.Locals[variable] = value;
            return;
        }

        int globalIndex = variable - 16;
        _memory.WriteWord(_globalsAddress + globalIndex * 2, value);
    }

    /// <summary>
    /// Reads a variable without side effects — variable 0 peeks at the
    /// stack top instead of popping. Used by the seven indirect-reference
    /// opcodes (inc, dec, inc_chk, dec_chk, load, store, pull).
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Indirect variable references" — An indirect reference to
    /// the stack pointer reads/writes in place rather than pushing/popping.
    /// </remarks>
    public ushort ReadVariableIndirect(byte variable)
    {
        if (variable == 0)
        {
            var stack = EvalStack;
            if (stack.Count == 0)
                throw new InvalidOperationException("Stack underflow: indirect read from an empty evaluation stack.");
            return stack.Peek();
        }

        return ReadVariable(variable);
    }

    /// <summary>
    /// Writes a variable without side effects — variable 0 replaces the
    /// stack top instead of pushing. Used by indirect-reference opcodes.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Indirect variable references" — An indirect reference to
    /// the stack pointer reads/writes in place rather than pushing/popping.
    /// </remarks>
    public void WriteVariableIndirect(byte variable, ushort value)
    {
        if (variable == 0)
        {
            var stack = EvalStack;
            if (stack.Count == 0)
                throw new InvalidOperationException("Stack underflow: indirect write to an empty evaluation stack.");
            stack.Pop();
            stack.Push(value);
            return;
        }

        WriteVariable(variable, value);
    }

    /// <summary>
    /// Stores the result of an instruction into the variable specified
    /// by the decoded store byte.
    /// </summary>
    /// <remarks>
    /// ZSpec S4.6 — Store variable: 0 = push to stack, 1–15 = local,
    /// 16–255 = global.
    /// </remarks>
    public void StoreResult(byte variable, ushort value)
    {
        WriteVariable(variable, value);
    }

    /// <summary>
    /// Evaluates a branch condition and returns the action the execution
    /// engine should take.
    /// </summary>
    /// <remarks>
    /// ZSpec S4.7 — Branch mechanics.
    /// ZSpec11 "@jump" — Target = address_after_branch + offset - 2.
    /// Offset 0 = rfalse, offset 1 = rtrue.
    /// </remarks>
    public static BranchResult ExecuteBranch(bool condition, BranchInfo branch, int addressAfterBranch)
    {
        bool takeBranch = condition == branch.BranchOnTrue;

        if (!takeBranch)
            return BranchResult.DontBranch;

        if (branch.IsRFalse)
            return BranchResult.ReturnFalse;

        if (branch.IsRTrue)
            return BranchResult.ReturnTrue;

        int target = addressAfterBranch + branch.Offset - 2;
        return BranchResult.Jump(target);
    }
}

/// <summary>
/// The result of evaluating a branch condition. Tells the execution
/// engine what to do next.
/// </summary>
public readonly struct BranchResult
{
    /// <summary>What kind of branch action to take.</summary>
    public BranchAction Action { get; }

    /// <summary>
    /// The target address to jump to. Only valid when Action is
    /// <see cref="BranchAction.Jump"/>.
    /// </summary>
    public int TargetAddress { get; }

    private BranchResult(BranchAction action, int target = 0)
    {
        Action = action;
        TargetAddress = target;
    }

    /// <summary>Branch condition not met — continue to next instruction.</summary>
    public static readonly BranchResult DontBranch = new(BranchAction.DontBranch);

    /// <summary>Branch to rfalse — return 0 from current routine.</summary>
    public static readonly BranchResult ReturnFalse = new(BranchAction.ReturnFalse);

    /// <summary>Branch to rtrue — return 1 from current routine.</summary>
    public static readonly BranchResult ReturnTrue = new(BranchAction.ReturnTrue);

    /// <summary>Branch to a specific address.</summary>
    public static BranchResult Jump(int address) => new(BranchAction.Jump, address);
}

/// <summary>
/// The type of action resulting from a branch evaluation.
/// </summary>
public enum BranchAction
{
    /// <summary>Condition not met, don't branch.</summary>
    DontBranch,
    /// <summary>Jump to a computed target address.</summary>
    Jump,
    /// <summary>Return false (0) from the current routine.</summary>
    ReturnFalse,
    /// <summary>Return true (1) from the current routine.</summary>
    ReturnTrue,
}
