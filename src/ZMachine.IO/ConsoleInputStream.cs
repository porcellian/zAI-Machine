namespace ZMachine.IO;

using ZMachine.Core;

/// <summary>
/// Keyboard-based IInputStream implementation using Console.ReadKey.
/// This is input stream 0 (the default keyboard input).
/// </summary>
/// <remarks>
/// ZSpec S10 — Stream 0 is keyboard input. Timed input waits up to
/// the specified duration for a keypress; if none arrives, returns 0.
/// ZSpec S10.5 — Cursor/function keys map to ZSCII 129-154.
/// </remarks>
public class ConsoleInputStream : IInputStream
{
    public bool HasMore => true;

    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        if (timeoutTenths > 0)
        {
            return ReadLineWithTimeout(maxLength, timeoutTenths);
        }

        string? line = Console.ReadLine();
        string text = line ?? "";
        if (text.Length > maxLength)
            text = text[..maxLength];

        return (text, 13);
    }

    public int ReadChar(int timeoutTenths = 0)
    {
        if (timeoutTenths > 0)
        {
            int millis = timeoutTenths * 100;
            var task = Task.Run(() =>
            {
                if (Console.KeyAvailable || SpinWait(millis))
                    return Console.ReadKey(true);
                return (ConsoleKeyInfo?)null;
            });

            if (task.Wait(millis))
            {
                var result = task.Result;
                if (result.HasValue)
                    return MapKeyToZscii(result.Value);
            }

            return 0;
        }

        var key = Console.ReadKey(true);
        return MapKeyToZscii(key);
    }

    private (string Text, int TerminatingChar) ReadLineWithTimeout(
        int maxLength, int timeoutTenths)
    {
        var chars = new List<char>();
        int remaining = timeoutTenths * 100;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            int elapsed = (int)stopwatch.ElapsedMilliseconds;
            if (elapsed >= remaining)
                return (new string(chars.ToArray()), 0);

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                    return (new string(chars.ToArray()), 13);

                if (key.Key == ConsoleKey.Backspace && chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    continue;
                }

                if (key.KeyChar >= 32 && key.KeyChar <= 126 && chars.Count < maxLength)
                {
                    chars.Add(key.KeyChar);
                }

                // ZSpec S10.7 — timer resets after each keypress.
                stopwatch.Restart();
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }

    private static bool SpinWait(int millis)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < millis)
        {
            if (Console.KeyAvailable)
                return true;
            Thread.Sleep(10);
        }
        return Console.KeyAvailable;
    }

    /// <summary>
    /// Maps a ConsoleKeyInfo to a ZSCII code. Printable ASCII maps
    /// directly; special keys map to ZSCII 129-154.
    /// </summary>
    /// <remarks>
    /// ZSpec S10.5 — Cursor and function key mappings:
    ///   129=cursor up, 130=cursor down, 131=cursor left, 132=cursor right
    ///   133-144=F1-F12, 145-154=numpad 0-9
    /// </remarks>
    public static int MapKeyToZscii(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter) return 13;
        if (key.Key == ConsoleKey.Backspace) return 8;
        if (key.Key == ConsoleKey.Escape) return 27;

        // Cursor keys — ZSpec S10.5
        if (key.Key == ConsoleKey.UpArrow) return 129;
        if (key.Key == ConsoleKey.DownArrow) return 130;
        if (key.Key == ConsoleKey.LeftArrow) return 131;
        if (key.Key == ConsoleKey.RightArrow) return 132;

        // Function keys — ZSCII 133-144
        if (key.Key >= ConsoleKey.F1 && key.Key <= ConsoleKey.F12)
            return 133 + (key.Key - ConsoleKey.F1);

        // Numpad 0-9 — ZSCII 145-154
        if (key.Key >= ConsoleKey.NumPad0 && key.Key <= ConsoleKey.NumPad9)
            return 145 + (key.Key - ConsoleKey.NumPad0);

        // Printable ASCII
        if (key.KeyChar >= 32 && key.KeyChar <= 126)
            return key.KeyChar;

        return 0;
    }
}
