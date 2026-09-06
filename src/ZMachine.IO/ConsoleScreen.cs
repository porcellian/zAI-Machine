namespace ZMachine.IO;

/// <summary>
/// Minimal console-based IScreen implementation using ANSI escape codes.
/// Serves as the initial output backend before the Avalonia GUI is built
/// (Phase 11), and as a fallback for headless/test use.
/// </summary>
public class ConsoleScreen : IScreen
{
    private int _currentWindow;
    private bool _bufferMode = true;

    // ZSpec S8.4 — upper window line count; 0 means unsplit
    private int _upperWindowLines;

    public void Print(string text)
    {
        Console.Write(text);
    }

    public void PrintChar(char c)
    {
        Console.Write(c);
    }

    public void NewLine()
    {
        Console.WriteLine();
    }

    /// <summary>
    /// Renders a V1–3 status line as a reverse-video bar across the top row.
    /// </summary>
    public void ShowStatusLine(string location, string scoreOrTime)
    {
        var (cols, _) = GetScreenSize();
        var savedLeft = Console.CursorLeft;
        var savedTop = Console.CursorTop;

        Console.SetCursorPosition(0, 0);

        // ANSI reverse video
        Console.Write("\x1b[7m");

        var padding = Math.Max(0, cols - location.Length - scoreOrTime.Length);
        var line = location + new string(' ', padding) + scoreOrTime;
        if (line.Length > cols)
            line = line[..cols];
        Console.Write(line);

        // Reset style
        Console.Write("\x1b[0m");

        Console.SetCursorPosition(savedLeft, savedTop);
    }

    public void SplitWindow(int lines)
    {
        _upperWindowLines = lines;
    }

    public void SetWindow(int window)
    {
        _currentWindow = window;
    }

    public void EraseLine()
    {
        // ANSI: clear from cursor to end of line
        Console.Write("\x1b[K");
    }

    public void EraseWindow(int window)
    {
        if (window == -1 || window == -2)
        {
            Console.Clear();
            if (window == -1)
                _upperWindowLines = 0;
        }
    }

    public void SetCursor(int line, int column)
    {
        // ZSpec uses 1-based; Console.SetCursorPosition is 0-based
        Console.SetCursorPosition(column - 1, line - 1);
    }

    public void SetTextStyle(int style)
    {
        // Reset first, then apply requested styles via ANSI codes
        Console.Write("\x1b[0m");

        if ((style & 1) != 0) Console.Write("\x1b[7m");  // Reverse
        if ((style & 2) != 0) Console.Write("\x1b[1m");  // Bold
        if ((style & 4) != 0) Console.Write("\x1b[3m");  // Italic
        // Fixed-pitch (8) has no ANSI equivalent in a terminal
    }

    public void BufferMode(bool enabled)
    {
        _bufferMode = enabled;
    }

    public (int Columns, int Rows) GetScreenSize()
    {
        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch (IOException)
        {
            // Headless environments may not have a console
            return (80, 25);
        }
    }
}
