namespace ZMachine.Core;

/// <summary>
/// The Z-Machine call stack — a stack of <see cref="CallFrame"/>s. Each
/// routine call pushes a new frame; each return pops one. The bottom
/// frame is the initial "main" routine's frame.
/// </summary>
/// <remarks>
/// ZSpec S5 — The call stack.
/// Quetzal S6.2 — Frame count needed for CATCH (returns the current
/// frame count) and THROW (unwinds to a given frame count).
/// </remarks>
public class CallStack
{
    private readonly Stack<CallFrame> _frames = new();

    /// <summary>
    /// The number of frames currently on the call stack.
    /// Used by CATCH/THROW (Quetzal S6.2).
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    /// The current (topmost) call frame, or null if the stack is empty.
    /// </summary>
    public CallFrame? CurrentFrame => _frames.Count > 0 ? _frames.Peek() : null;

    /// <summary>
    /// Pushes a new call frame onto the stack.
    /// </summary>
    public void PushFrame(CallFrame frame)
    {
        _frames.Push(frame);
    }

    /// <summary>
    /// Pops and returns the current call frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the call stack is empty.
    /// </exception>
    public CallFrame PopFrame()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("Call stack underflow: no frame to return from.");
        return _frames.Pop();
    }

    /// <summary>
    /// Removes all frames from the call stack.
    /// Used by Quetzal restore to replace the entire stack.
    /// </summary>
    public void Clear() => _frames.Clear();

    /// <summary>
    /// Returns all frames as an enumerable for serialization (Quetzal save).
    /// Bottom frame is enumerated first.
    /// </summary>
    public IEnumerable<CallFrame> GetFramesBottomUp()
    {
        return _frames.Reverse();
    }
}
