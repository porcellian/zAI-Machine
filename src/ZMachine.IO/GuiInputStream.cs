namespace ZMachine.IO;

using ZMachine.Core;

/// <summary>
/// GUI-based IInputStream that receives keyboard input from the Avalonia
/// window's key events via a thread-safe queue. The Z-Machine runs on
/// a background thread; GUI events post to the queue, and ReadLine/ReadChar
/// block until input arrives.
/// </summary>
/// <remarks>
/// ZSpec S10 — Stream 0 is keyboard input. Timed input waits up to
/// the specified duration; if no key arrives, returns 0.
/// ZSpec S10.5 — Cursor/function keys map to ZSCII 129-154.
/// </remarks>
public class GuiInputStream : IInputStream
{
    private readonly BlockingQueue<InputEvent> _queue = new();

    public bool HasMore => true;

    /// <summary>
    /// Called by the GUI thread when a character is typed.
    /// </summary>
    public void EnqueueChar(int zsciiCode)
    {
        _queue.Enqueue(new InputEvent(zsciiCode, null));
    }

    /// <summary>
    /// Called by the GUI thread when a line is submitted (Enter pressed).
    /// </summary>
    public void EnqueueLine(string text)
    {
        _queue.Enqueue(new InputEvent(13, text));
    }

    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        if (timeoutTenths > 0)
        {
            int millis = timeoutTenths * 100;
            if (_queue.TryDequeue(out var evt, millis))
            {
                if (evt.LineText != null)
                {
                    string text = evt.LineText;
                    if (text.Length > maxLength)
                        text = text[..maxLength];
                    return (text, 13);
                }
            }
            return ("", 0);
        }

        while (true)
        {
            var input = _queue.Dequeue();
            if (input.LineText != null)
            {
                string text = input.LineText;
                if (text.Length > maxLength)
                    text = text[..maxLength];
                return (text, 13);
            }
        }
    }

    public int ReadChar(int timeoutTenths = 0)
    {
        if (timeoutTenths > 0)
        {
            int millis = timeoutTenths * 100;
            if (_queue.TryDequeue(out var evt, millis))
                return evt.ZsciiCode;
            return 0;
        }

        var input = _queue.Dequeue();
        return input.ZsciiCode;
    }

    private readonly record struct InputEvent(int ZsciiCode, string? LineText);

    /// <summary>
    /// Simple thread-safe blocking queue using Monitor wait/pulse.
    /// </summary>
    private class BlockingQueue<T>
    {
        private readonly Queue<T> _queue = new();
        private readonly object _lock = new();

        public void Enqueue(T item)
        {
            lock (_lock)
            {
                _queue.Enqueue(item);
                Monitor.Pulse(_lock);
            }
        }

        public T Dequeue()
        {
            lock (_lock)
            {
                while (_queue.Count == 0)
                    Monitor.Wait(_lock);
                return _queue.Dequeue();
            }
        }

        public bool TryDequeue(out T item, int timeoutMillis)
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                    Monitor.Wait(_lock, timeoutMillis);

                if (_queue.Count > 0)
                {
                    item = _queue.Dequeue();
                    return true;
                }

                item = default!;
                return false;
            }
        }
    }
}
