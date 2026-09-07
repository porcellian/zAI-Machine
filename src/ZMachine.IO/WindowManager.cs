namespace ZMachine.IO;

using ZMachine.Core;

/// <summary>
/// Coordinates Z-Machine window management across versions: V1-3 status
/// line display and V4-5 split/set/erase/cursor operations. Delegates
/// rendering to an IScreen backend.
/// </summary>
/// <remarks>
/// ZSpec S8.4 — V4+ screen model: upper window (fixed, no scroll) and
/// lower window (scrolling, word wrap in buffer mode).
/// ZSpec S8.7 — @split_window, @set_window, @erase_window, @set_cursor.
/// ZSpec11 "@set_cursor" — If the cursor is set below the split, the
/// upper window is implicitly expanded to include that line.
/// </remarks>
public class WindowManager
{
    private readonly IScreen _screen;
    private readonly int _version;
    private int _upperWindowLines;
    private int _currentWindow;

    public WindowManager(IScreen screen, int version)
    {
        _screen = screen;
        _version = version;
    }

    /// <summary>Current upper window size in lines.</summary>
    public int UpperWindowLines => _upperWindowLines;

    /// <summary>Currently selected window: 0 = lower, 1 = upper.</summary>
    public int CurrentWindow => _currentWindow;

    /// <summary>
    /// Splits the screen: the upper window gets the specified number of
    /// lines. Passing 0 unsplits (removes the upper window).
    /// </summary>
    /// <remarks>
    /// ZSpec S8.7 — @split_window lines: sets upper window to N lines.
    /// V3: clears the upper window. V4+: does not clear it.
    /// Cursor moves to (1,1) in V3.
    /// </remarks>
    public void SplitWindow(int lines)
    {
        _upperWindowLines = lines;
        _screen.SplitWindow(lines);

        if (lines == 0)
            _currentWindow = 0;
    }

    /// <summary>
    /// Selects the output window.
    /// </summary>
    /// <remarks>
    /// ZSpec S8.7 — @set_window: 0 selects lower, 1 selects upper.
    /// When selecting the upper window in V4+, the cursor moves to (1,1).
    /// </remarks>
    public void SetWindow(int window)
    {
        _currentWindow = window;
        _screen.SetWindow(window);
    }

    /// <summary>
    /// Sets the cursor position in the upper window (1-based coordinates).
    /// If the line exceeds the current split size, the upper window is
    /// implicitly expanded to include that line.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@set_cursor" — "If the window has not been split to be
    /// big enough to contain the cursor position, the window should be
    /// split to the appropriate size."
    /// </remarks>
    public void SetCursor(int line, int column)
    {
        if (_currentWindow == 1 && line > _upperWindowLines)
        {
            _upperWindowLines = line;
            _screen.SplitWindow(line);
        }

        _screen.SetCursor(line, column);
    }

    /// <summary>
    /// Erases a window or performs a special erase operation.
    /// </summary>
    /// <remarks>
    /// ZSpec S8.7.3 — @erase_window:
    ///   -1: clear all + unsplit (upper window size → 0)
    ///   -2: clear all without unsplitting
    ///    0: clear lower window
    ///    1: clear upper window
    /// After erase_window -1, the lower window is selected and the
    /// cursor moves to (1,1) of the lower window.
    /// </remarks>
    public void EraseWindow(int window)
    {
        _screen.EraseWindow(window);

        if (window == -1)
        {
            _upperWindowLines = 0;
            _currentWindow = 0;
        }
    }

    /// <summary>
    /// Shows the V1-3 status line. Should be called before each @read
    /// in V1-3 games.
    /// </summary>
    public void ShowStatusLine(StatusLineHandler statusHandler)
    {
        if (_version > 3)
            return;

        var (location, scoreOrTime) = statusHandler.BuildStatusLine();
        _screen.ShowStatusLine(location, scoreOrTime);
    }
}
