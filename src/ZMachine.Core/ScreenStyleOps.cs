namespace ZMachine.Core;

/// <summary>
/// Implements V5+ screen and style opcodes: text style, font selection,
/// colour setting, cursor queries, line erasure, buffer mode, Unicode
/// capability checks, and single-level undo. Style and font state is
/// tracked here; rendering is delegated to an <see cref="IScreenStyle"/>
/// callback interface so this class stays in Core without depending on IO.
/// </summary>
/// <remarks>
/// ZSpec S8 — Screen model.
/// ZSpec11 "@set_text_style", "@set_font", "@set_colour", "Colour numbers".
/// </remarks>
public class ScreenStyleOps
{
    private readonly Memory _memory;
    private readonly int _version;

    private int _currentStyle;
    private int _currentFont = 1;
    private int _foregroundColor = 1; // 1 = default
    private int _backgroundColor = 1;

    // Single-level undo snapshot.
    private byte[]? _undoSnapshot;
    private bool _undoAvailable;

    /// <summary>Callback to apply text style to the screen backend.</summary>
    public Action<int>? OnSetTextStyle { get; set; }

    /// <summary>Callback to apply font to the screen backend. Returns previous font, or 0 if unsupported.</summary>
    public Func<int, int>? OnSetFont { get; set; }

    /// <summary>Callback to apply foreground/background colours.</summary>
    public Action<int, int>? OnSetColour { get; set; }

    /// <summary>Callback to get cursor position as (line, column), 1-based.</summary>
    public Func<(int Line, int Column)>? OnGetCursor { get; set; }

    /// <summary>Callback to erase the current line.</summary>
    public Action? OnEraseLine { get; set; }

    /// <summary>Callback to set buffer mode (word wrapping).</summary>
    public Action<bool>? OnBufferMode { get; set; }

    /// <summary>
    /// Creates screen/style opcode handlers for the given version.
    /// </summary>
    public ScreenStyleOps(Memory memory, int version)
    {
        _memory = memory;
        _version = version;
    }

    /// <summary>Current text style bitmask.</summary>
    public int CurrentStyle => _currentStyle;

    /// <summary>Current font number.</summary>
    public int CurrentFont => _currentFont;

    /// <summary>Current foreground colour number.</summary>
    public int ForegroundColor => _foregroundColor;

    /// <summary>Current background colour number.</summary>
    public int BackgroundColor => _backgroundColor;

    /// <summary>
    /// @set_text_style style — sets the text style. Style 0 (Roman)
    /// clears all styles. Non-zero styles are combined via addition.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@set_text_style" — "Setting a style of 0 means
    /// Roman (no special style). Otherwise, styles are combined by
    /// addition." Priority: Fixed > Italic > Bold > Reverse.
    /// </remarks>
    public void SetTextStyle(int style)
    {
        if (style == 0)
            _currentStyle = 0;
        else
            _currentStyle |= style;

        OnSetTextStyle?.Invoke(_currentStyle);
    }

    /// <summary>
    /// @set_font font → (result). Switches to the given font. Returns
    /// the previous font number, or 0 if the font is not available.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@set_font" — Font 1 = normal, 3 = character graphics,
    /// 4 = fixed-pitch. Font 2 is undefined and must return 0.
    /// Fonts 5–1023 are reserved.
    /// </remarks>
    public ushort SetFont(int font)
    {
        // Font 0: return current font without changing.
        if (font == 0)
            return (ushort)_currentFont;

        // Font 2 and fonts 5+ are not available.
        if (font == 2 || font >= 5)
            return 0;

        int previous = _currentFont;

        if (OnSetFont != null)
        {
            int result = OnSetFont(font);
            if (result == 0)
                return 0;
        }

        _currentFont = font;
        return (ushort)previous;
    }

    /// <summary>
    /// @set_colour fg bg — sets foreground and background colours.
    /// Colour 0 means "current" (no change). Colour 1 means "default".
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Colour numbers" — 2=black, 3=red, 4=green, 5=yellow,
    /// 6=blue, 7=magenta, 8=cyan, 9=white. 10–12=Standard 1.1 greys
    /// (light, medium, dark).
    /// </remarks>
    public void SetColour(int fg, int bg)
    {
        if (fg != 0)
            _foregroundColor = fg;
        if (bg != 0)
            _backgroundColor = bg;

        OnSetColour?.Invoke(_foregroundColor, _backgroundColor);
    }

    /// <summary>
    /// @get_cursor array — writes the current cursor position to the
    /// given memory address as two consecutive words (line, column),
    /// both 1-based.
    /// </summary>
    public void GetCursor(ushort array)
    {
        var (line, column) = OnGetCursor?.Invoke() ?? (1, 1);
        _memory.WriteWord(array, (ushort)line);
        _memory.WriteWord(array + 2, (ushort)column);
    }

    /// <summary>
    /// @erase_line 1 — erases from the cursor to the end of the line.
    /// Only value 1 is meaningful; other values are ignored.
    /// </summary>
    public void EraseLine(ushort value)
    {
        if (value == 1)
            OnEraseLine?.Invoke();
    }

    /// <summary>
    /// @buffer_mode flag — enables (1) or disables (0) output buffering
    /// (word wrapping) for the lower window.
    /// </summary>
    public void BufferMode(ushort flag)
    {
        OnBufferMode?.Invoke(flag != 0);
    }

    /// <summary>
    /// @check_unicode char (EXT) — returns a bitmask indicating whether
    /// the character can be printed (bit 0) and/or accepted as input
    /// (bit 1). We support BMP printing and ASCII input range.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@check_unicode" — bit 0: interpreter can print,
    /// bit 1: interpreter can receive as input.
    /// </remarks>
    public ushort CheckUnicode(ushort charCode)
    {
        int result = 0;

        // Printable: any BMP character except control codes.
        if (charCode >= 32 && !(charCode >= 127 && charCode <= 159))
            result |= 1;

        // Input: printable ASCII range (32–126).
        if (charCode >= 32 && charCode <= 126)
            result |= 2;

        return (ushort)result;
    }

    /// <summary>
    /// @save_undo (EXT) — saves a snapshot of dynamic memory for
    /// single-level undo. Stores 1 on success, 0 on failure.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @save_undo: "Attempt to save the game state for
    /// undo purposes." Returns -1 if not supported; we support it.
    /// </remarks>
    public ushort SaveUndo()
    {
        int dynamicSize = _memory.StaticBase;
        _undoSnapshot = new byte[dynamicSize];
        Array.Copy(_memory.RawBytes.ToArray(), 0, _undoSnapshot, 0, dynamicSize);
        _undoAvailable = true;
        return 1;
    }

    /// <summary>
    /// @restore_undo (EXT) — restores the undo snapshot. Stores 2
    /// on success (distinguishing from a save), 0 on failure.
    /// </summary>
    public ushort RestoreUndo()
    {
        if (!_undoAvailable || _undoSnapshot == null)
            return 0;

        // Restore dynamic memory from the snapshot.
        for (int i = 0; i < _undoSnapshot.Length; i++)
            _memory.DynamicSpan[i] = _undoSnapshot[i];

        _undoAvailable = false;
        return 2;
    }

    /// <summary>
    /// Checks the fixed-pitch header bit (Flags 2, byte $10, bit 3)
    /// and returns whether fixed-pitch mode is requested by the game.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "The fixed-pitch header bit" — V5: the interpreter must
    /// honour Flags 2 bit 3 at runtime even though it is deprecated.
    /// </remarks>
    public bool IsFixedPitchRequested()
    {
        byte flags2Low = _memory.ReadByte(0x11);
        return (flags2Low & 0x08) != 0;
    }
}
