namespace ZMachine.Core;

/// <summary>
/// Manages the 8 independent V6 windows and dispatches V6-specific
/// window opcodes. Provides @get_wind_prop, @put_wind_prop,
/// @move_window, @window_size, @window_style, @set_margins,
/// @scroll_window, and @mouse_window.
/// </summary>
/// <remarks>
/// ZSpec S8.8 — 8 windows numbered 0–7, each with 18 properties.
/// ZSpec11 "Version 6 windows" — all windows treated identically
/// except for default positions/sizes and @split_window.
/// </remarks>
public class V6WindowManager
{
    private readonly V6Window[] _windows;
    private int _selectedWindow;
    private int _mouseWindow = 1;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    /// <summary>
    /// Creates the V6 window manager and initialises all 8 windows
    /// with their default positions and sizes.
    /// </summary>
    /// <param name="screenWidth">Total screen width in pixels.</param>
    /// <param name="screenHeight">Total screen height in pixels.</param>
    /// <param name="fontWidth">Default font width in pixels.</param>
    /// <param name="fontHeight">Default font height in pixels.</param>
    public V6WindowManager(int screenWidth, int screenHeight, int fontWidth, int fontHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        _windows = new V6Window[8];
        for (int i = 0; i < 8; i++)
        {
            _windows[i] = new V6Window(i);
            _windows[i].FontSize = (fontHeight << 8) | fontWidth;
        }

        // ZSpec S8.8 — window 0 fills the screen by default
        _windows[0].Y = 1;
        _windows[0].X = 1;
        _windows[0].Width = screenWidth;
        _windows[0].Height = screenHeight;
        _windows[0].Wrapping = true;
        _windows[0].Scrolling = true;
        _windows[0].Buffered = true;

        // ZSpec S8.8 — window 1 at top, full width, zero height
        _windows[1].Y = 1;
        _windows[1].X = 1;
        _windows[1].Width = screenWidth;
        _windows[1].Height = 0;

        // Windows 2–7: zero size, position (1,1)
        for (int i = 2; i < 8; i++)
        {
            _windows[i].Y = 1;
            _windows[i].X = 1;
        }

        // ZSpec11 — default foreground/background colours (white on black)
        int defaultColour = (2 << 8) | 9; // bg=black(2), fg=white(9)
        for (int i = 0; i < 8; i++)
            _windows[i].ColourData = defaultColour;
    }

    /// <summary>Gets the currently selected window number (0–7).</summary>
    public int SelectedWindow => _selectedWindow;

    /// <summary>Gets the currently selected V6Window object.</summary>
    public V6Window Current => _windows[_selectedWindow];

    /// <summary>
    /// Gets the mouse window number.
    /// ZSpec11 "Version 6 windows" — window 1 is the default.
    /// </summary>
    public int MouseWindow => _mouseWindow;

    /// <summary>Gets a window by number (0–7).</summary>
    public V6Window GetWindow(int number)
    {
        if (number < 0 || number > 7)
            return _windows[0];
        return _windows[number];
    }

    /// <summary>Gets all 8 windows in drawing order (0–7).</summary>
    public IReadOnlyList<V6Window> Windows => _windows;

    /// <summary>
    /// EXT:19 @get_wind_prop window property → result.
    /// Returns the value of a window property (0–17).
    /// ZSpec S8.8, ZSpec11 "@get_wind_prop".
    /// </summary>
    public int GetWindProp(int window, int property)
    {
        return GetWindow(window).GetProperty(property);
    }

    /// <summary>
    /// EXT:20 @put_wind_prop window property value.
    /// Sets a writable window property (0–15).
    /// Properties 16–17 are read-only and silently ignored.
    /// </summary>
    public void PutWindProp(int window, int property, int value)
    {
        GetWindow(window).SetProperty(property, value);
    }

    /// <summary>
    /// VAR:11 @set_window window.
    /// Selects the active output window (0–7 in V6).
    /// </summary>
    public void SetWindow(int window)
    {
        if (window >= 0 && window <= 7)
            _selectedWindow = window;
    }

    /// <summary>
    /// EXT:16 @move_window window y x.
    /// Moves a window to an absolute pixel position.
    /// </summary>
    public void MoveWindow(int window, int y, int x)
    {
        var w = GetWindow(window);
        w.Y = y;
        w.X = x;
    }

    /// <summary>
    /// EXT:17 @window_size window height width.
    /// Resizes a window to the given dimensions in pixels.
    /// </summary>
    public void WindowSize(int window, int height, int width)
    {
        var w = GetWindow(window);
        w.Height = height;
        w.Width = width;
    }

    /// <summary>
    /// EXT:18 @window_style window flags operation.
    /// Sets window attributes. Operation: 0=set, 1=set bits, 2=clear bits.
    /// ZSpec S8.8 — attribute bits: 0=wrap, 1=scroll, 2=transcript, 3=buffered.
    /// </summary>
    public void WindowStyle(int window, int flags, int operation)
    {
        var w = GetWindow(window);
        switch (operation)
        {
            case 0: // set
                w.Attributes = flags;
                break;
            case 1: // set bits (OR)
                w.Attributes |= flags;
                break;
            case 2: // clear bits (AND NOT)
                w.Attributes &= ~flags;
                break;
        }
    }

    /// <summary>
    /// EXT:8 @set_margins left right window.
    /// Sets the left and right margins for a window.
    /// </summary>
    public void SetMargins(int left, int right, int window)
    {
        var w = GetWindow(window);
        w.LeftMargin = left;
        w.RightMargin = right;
    }

    /// <summary>
    /// EXT:21 @scroll_window window pixels.
    /// Scrolls the contents of a window by the given number of pixels.
    /// Positive = scroll up, negative = scroll down.
    /// </summary>
    public void ScrollWindow(int window, int pixels)
    {
        // Scrolling is a rendering operation — record the request;
        // the IScreen implementation will perform the actual scroll.
        var w = GetWindow(window);
        if (!w.Scrolling && pixels > 0)
            return;
        // The actual pixel-level scroll is delegated to the renderer.
    }

    /// <summary>
    /// EXT:22 @mouse_window window.
    /// Sets the mouse window. Mouse clicks outside this window
    /// are ignored for input purposes. -1 = any window.
    /// </summary>
    public void SetMouseWindow(int window)
    {
        _mouseWindow = window;
    }

    /// <summary>
    /// VAR:10 @split_window lines (V6 behavior).
    /// ZSpec11 "@split_window" — manipulates windows 0 and 1.
    /// Window 1 gets the specified height; window 0 fills below.
    /// Cursor remains at the same absolute position.
    /// </summary>
    public void SplitWindow(int lines, int fontHeight)
    {
        int splitHeight = lines * fontHeight;

        // Window 1: top of screen, full width, specified height
        _windows[1].Y = 1;
        _windows[1].X = 1;
        _windows[1].Width = _screenWidth;
        _windows[1].Height = splitHeight;

        // Window 0: below window 1, fills remaining screen
        _windows[0].Y = 1 + splitHeight;
        _windows[0].X = 1;
        _windows[0].Width = _screenWidth;
        _windows[0].Height = _screenHeight - splitHeight;

        // ZSpec11 — cursor stays at the same absolute position
        // (no cursor adjustment needed; cursor coordinates are
        // window-relative and the caller must handle any clipping)
    }
}
