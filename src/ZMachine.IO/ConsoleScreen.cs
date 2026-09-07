namespace ZMachine.IO;

/// <summary>
/// Console-based IScreen implementation using ANSI escape codes.
/// Serves as the initial output backend before the Avalonia GUI is built
/// (Phase 11), and as a fallback for headless/test use.
/// </summary>
/// <remarks>
/// ZSpec S8.1–S8.3 — The screen model: V1-3 have a status line and a
/// single scrolling window. V4+ add a split upper/lower window model.
/// ZSpec S8.4 — The upper window is a fixed-position text area that
/// does not scroll. The lower window scrolls and supports word wrapping
/// when buffer mode is enabled.
/// </remarks>
public class ConsoleScreen : IScreen
{
    private readonly TextWriter _output;
    private readonly int _columns;
    private readonly int _rows;
    private readonly bool _isTerminal;

    private int _currentWindow;
    private bool _bufferMode = true;

    // ZSpec S8.4 — upper window line count; 0 means unsplit.
    private int _upperWindowLines;

    // Word wrap buffer — accumulates text in the lower window when
    // buffer mode is on, flushed on newline or when wrapping is needed.
    private readonly List<char> _lineBuffer = new();
    private int _cursorColumn;

    // Upper window cursor position (1-based).
    private int _upperCursorLine = 1;
    private int _upperCursorColumn = 1;

    /// <summary>
    /// Creates a ConsoleScreen writing to the given TextWriter.
    /// </summary>
    /// <param name="output">
    /// The output sink. Pass <c>Console.Out</c> for real terminal use,
    /// or a <c>StringWriter</c> for testing.
    /// </param>
    /// <param name="columns">Screen width in character cells.</param>
    /// <param name="rows">Screen height in character cells.</param>
    /// <param name="isTerminal">
    /// Whether the output supports ANSI escape codes and cursor
    /// positioning. Set to false for test/capture mode.
    /// </param>
    public ConsoleScreen(TextWriter output, int columns = 80, int rows = 25,
        bool isTerminal = true)
    {
        _output = output;
        _columns = columns;
        _rows = rows;
        _isTerminal = isTerminal;
    }

    /// <summary>
    /// Creates a ConsoleScreen attached to the real terminal.
    /// </summary>
    public ConsoleScreen() : this(Console.Out,
        GetTerminalWidth(), GetTerminalHeight(), true)
    {
    }

    public void Print(string text)
    {
        foreach (char c in text)
            PrintChar(c);
    }

    public void PrintChar(char c)
    {
        if (_currentWindow == 1)
        {
            PrintToUpperWindow(c);
            return;
        }

        if (c == '\n')
        {
            NewLine();
            return;
        }

        if (_bufferMode)
        {
            _lineBuffer.Add(c);
            // Check if we need to wrap.
            if (_cursorColumn + _lineBuffer.Count > _columns)
                FlushWithWordWrap();
        }
        else
        {
            _output.Write(c);
            _cursorColumn++;
            if (_cursorColumn >= _columns)
            {
                _output.WriteLine();
                _cursorColumn = 0;
            }
        }
    }

    public void NewLine()
    {
        if (_currentWindow == 1)
        {
            // Upper window: advance cursor to next line, column 1.
            _upperCursorLine++;
            _upperCursorColumn = 1;
            if (_isTerminal)
                _output.Write($"\x1b[{_upperCursorLine};1H");
            return;
        }

        FlushLineBuffer();
        _output.WriteLine();
        _cursorColumn = 0;
    }

    /// <summary>
    /// Renders a V1-3 status line as a reverse-video bar across the top row.
    /// Location is left-aligned, score/time is right-aligned.
    /// </summary>
    /// <remarks>
    /// ZSpec S8.2 — The status line occupies the top line of the screen.
    /// In "score" games it shows "location   score/turns"; in "time" games
    /// it shows "location   hours:minutes".
    /// </remarks>
    public void ShowStatusLine(string location, string scoreOrTime)
    {
        if (_isTerminal)
        {
            // Save cursor, move to top-left.
            _output.Write("\x1b[s\x1b[1;1H");
        }

        // ANSI reverse video.
        if (_isTerminal)
            _output.Write("\x1b[7m");

        string line = FormatStatusLine(location, scoreOrTime, _columns);
        _output.Write(line);

        if (_isTerminal)
        {
            // Reset style and restore cursor.
            _output.Write("\x1b[0m\x1b[u");
        }
        else
        {
            _output.WriteLine();
        }
    }

    public void SplitWindow(int lines)
    {
        _upperWindowLines = lines;
        if (lines == 0)
        {
            // Unsplit: select lower window.
            _currentWindow = 0;
        }
    }

    public void SetWindow(int window)
    {
        if (_currentWindow == 0 && window == 1)
        {
            // Switching to upper window — flush any buffered lower text.
            FlushLineBuffer();
        }

        _currentWindow = window;

        if (window == 1)
        {
            // ZSpec S8.7.2 — When upper window is selected, cursor moves
            // to top-left of that window.
            _upperCursorLine = 1;
            _upperCursorColumn = 1;
            if (_isTerminal)
                _output.Write("\x1b[1;1H");
        }
    }

    public void EraseLine()
    {
        if (_isTerminal)
            _output.Write("\x1b[K");
    }

    /// <remarks>
    /// ZSpec S8.7.3 — @erase_window: -1 clears + unsplits,
    /// -2 clears without unsplitting, 0/1 clears that window.
    /// </remarks>
    public void EraseWindow(int window)
    {
        if (window == -1 || window == -2)
        {
            if (_isTerminal)
                _output.Write("\x1b[2J\x1b[H");
            if (window == -1)
                _upperWindowLines = 0;
            _cursorColumn = 0;
            _lineBuffer.Clear();
        }
        else if (window == 0 || window == 1)
        {
            if (_isTerminal)
            {
                int startLine = window == 1 ? 1 : _upperWindowLines + 1;
                int endLine = window == 1 ? _upperWindowLines : _rows;
                for (int i = startLine; i <= endLine; i++)
                {
                    _output.Write($"\x1b[{i};1H\x1b[K");
                }
            }
            if (window == 0)
            {
                _cursorColumn = 0;
                _lineBuffer.Clear();
            }
        }
    }

    /// <remarks>
    /// ZSpec S8.7.2 — Cursor coordinates are 1-based, relative to the
    /// current window's origin.
    /// </remarks>
    public void SetCursor(int line, int column)
    {
        if (_currentWindow == 1)
        {
            _upperCursorLine = line;
            _upperCursorColumn = column;
            if (_isTerminal)
                _output.Write($"\x1b[{line};{column}H");
        }
    }

    /// <remarks>
    /// ZSpec11 "@set_text_style" — 0=Roman, 1=Reverse, 2=Bold,
    /// 4=Italic, 8=Fixed-pitch. Style 0 resets to Roman.
    /// </remarks>
    public void SetTextStyle(int style)
    {
        if (!_isTerminal)
            return;

        _output.Write("\x1b[0m");

        if ((style & 1) != 0) _output.Write("\x1b[7m");  // Reverse
        if ((style & 2) != 0) _output.Write("\x1b[1m");  // Bold
        if ((style & 4) != 0) _output.Write("\x1b[3m");  // Italic
        // Fixed-pitch (8) has no ANSI equivalent in a terminal.
    }

    public void BufferMode(bool enabled)
    {
        if (_bufferMode && !enabled)
            FlushLineBuffer();
        _bufferMode = enabled;
    }

    public (int Columns, int Rows) GetScreenSize()
    {
        return (_columns, _rows);
    }

    #region Word Wrapping

    /// <summary>
    /// Flushes the line buffer, performing word wrapping if the content
    /// would exceed the screen width. Finds the last space in the buffer
    /// and breaks there, emitting the first part with a newline and
    /// keeping the remainder for the next line.
    /// </summary>
    /// <remarks>
    /// ZSpec S8.4 — In buffer mode the interpreter word-wraps output
    /// at the screen width. Breaking happens at the last space that
    /// fits within the available columns.
    /// </remarks>
    private void FlushWithWordWrap()
    {
        int available = _columns - _cursorColumn;

        // Find the last space within the available width.
        int breakAt = -1;
        int searchEnd = Math.Min(_lineBuffer.Count, available);
        for (int i = searchEnd - 1; i >= 0; i--)
        {
            if (_lineBuffer[i] == ' ')
            {
                breakAt = i;
                break;
            }
        }

        if (breakAt < 0)
        {
            if (_cursorColumn == 0)
            {
                // Word is longer than screen width — force break.
                breakAt = available;
                WriteChars(_lineBuffer, 0, breakAt);
                _output.WriteLine();
                _lineBuffer.RemoveRange(0, breakAt);
                _cursorColumn = 0;
            }
            else
            {
                // No space found but we're mid-line — wrap the entire
                // buffer to a new line.
                _output.WriteLine();
                _cursorColumn = 0;
                // Re-check if the buffer still exceeds width.
                if (_lineBuffer.Count > _columns)
                    FlushWithWordWrap();
            }
            return;
        }

        // Emit everything up to (not including) the space, then newline.
        WriteChars(_lineBuffer, 0, breakAt);
        _output.WriteLine();
        _cursorColumn = 0;

        // Remove the emitted text plus the space.
        _lineBuffer.RemoveRange(0, breakAt + 1);

        // If the remaining buffer still overflows, wrap again.
        if (_lineBuffer.Count > _columns)
            FlushWithWordWrap();
    }

    /// <summary>
    /// Flushes the line buffer to output without wrapping (used on
    /// explicit newlines and when buffer mode is disabled).
    /// </summary>
    private void FlushLineBuffer()
    {
        if (_lineBuffer.Count == 0)
            return;

        WriteChars(_lineBuffer, 0, _lineBuffer.Count);
        _cursorColumn += _lineBuffer.Count;
        _lineBuffer.Clear();
    }

    private void WriteChars(List<char> chars, int start, int count)
    {
        for (int i = start; i < start + count; i++)
            _output.Write(chars[i]);
    }

    #endregion

    #region Upper Window

    private void PrintToUpperWindow(char c)
    {
        if (_upperCursorLine > _upperWindowLines)
            return;

        if (_isTerminal)
            _output.Write($"\x1b[{_upperCursorLine};{_upperCursorColumn}H");
        _output.Write(c);
        _upperCursorColumn++;

        if (_upperCursorColumn > _columns)
        {
            _upperCursorColumn = 1;
            _upperCursorLine++;
        }
    }

    #endregion

    /// <summary>
    /// Formats a status line string: location left-aligned, score/time
    /// right-aligned, padded with spaces to fill the given width.
    /// </summary>
    public static string FormatStatusLine(string location, string scoreOrTime, int width)
    {
        int padding = Math.Max(1, width - location.Length - scoreOrTime.Length);
        string line = location + new string(' ', padding) + scoreOrTime;
        if (line.Length > width)
            line = line[..width];
        return line;
    }

    private static int GetTerminalWidth()
    {
        try { return Console.WindowWidth; }
        catch (IOException) { return 80; }
    }

    private static int GetTerminalHeight()
    {
        try { return Console.WindowHeight; }
        catch (IOException) { return 25; }
    }
}
