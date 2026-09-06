namespace ZMachine.Core;

/// <summary>
/// A single call frame on the Z-Machine call stack. Each routine
/// invocation creates a frame with its own local variables, evaluation
/// stack, return address, and store target.
/// </summary>
/// <remarks>
/// ZSpec S5 — Routines and the call stack.
/// ZSpec S6.1 — Each routine has 0–15 local variables.
/// ZSpec S6.3 — Each routine invocation has its own evaluation stack.
/// Quetzal S6.2 — Frame count needed for CATCH/THROW.
/// </remarks>
public class CallFrame
{
    /// <summary>
    /// The program counter to return to when this routine exits.
    /// For the initial (bottom) frame, this is 0.
    /// </summary>
    public int ReturnPC { get; }

    /// <summary>
    /// The variable to store the return value in when this routine exits.
    /// 0 = stack push, 1–15 = local of the calling frame, 16–255 = global.
    /// Only meaningful when <see cref="DiscardResult"/> is false.
    /// </summary>
    public byte StoreVariable { get; }

    /// <summary>
    /// If true, the return value is discarded (call_vn/call_vn2 opcodes).
    /// </summary>
    public bool DiscardResult { get; }

    /// <summary>
    /// Number of arguments passed to this routine (0–7). Needed for
    /// <c>@check_arg_count</c>.
    /// </summary>
    public int ArgumentCount { get; }

    /// <summary>
    /// Local variables for this routine (1-indexed, slots 1–15).
    /// Slot 0 is unused. In V1–4, initial values come from the routine
    /// header. In V5+, all locals start at 0.
    /// </summary>
    public ushort[] Locals { get; } = new ushort[16];

    /// <summary>Number of local variables (0–15).</summary>
    public int LocalCount { get; }

    /// <summary>
    /// The per-frame evaluation stack. Variable 0 reads (pop) and
    /// writes (push) operate on this stack.
    /// </summary>
    public Stack<ushort> EvalStack { get; } = new();

    /// <summary>
    /// Creates a new call frame.
    /// </summary>
    /// <param name="returnPC">PC to return to when this routine exits.</param>
    /// <param name="storeVariable">Variable to receive the return value.</param>
    /// <param name="discardResult">True if the return value is discarded.</param>
    /// <param name="localCount">Number of local variables (0–15).</param>
    /// <param name="argumentCount">Number of arguments passed (0–7).</param>
    public CallFrame(int returnPC, byte storeVariable, bool discardResult, int localCount, int argumentCount)
    {
        ReturnPC = returnPC;
        StoreVariable = storeVariable;
        DiscardResult = discardResult;
        LocalCount = localCount;
        ArgumentCount = argumentCount;
    }
}
