namespace ZMachine.Core;

/// <summary>
/// Manages true colour state for Standard 1.1 interpreters. Handles
/// <c>@set_true_colour</c>, standard-to-true-colour equivalences,
/// non-standard colour tracking (colours 16–255), and V6 transparency.
/// </summary>
/// <remarks>
/// ZSpec11 "@set_true_colour" — 15-bit sRGB values, magic values -1 to -4.
/// ZSpec11 "Colour numbers" — standard colours 2–12, non-standard 16–255,
/// last 240 distinct non-standard colours tracked with wrap-around.
/// </remarks>
public class TrueColourManager
{
    /// <summary>15-bit sRGB equivalents for standard colour numbers 2–12.</summary>
    private static readonly ushort[] StandardTrueColours =
    {
        // Index 0-1 unused (colours 0/1 are current/default)
        0, 0,
        // 2=black, 3=red, 4=green, 5=yellow
        0x0000, 0x001D, 0x0340, 0x03BD,
        // 6=blue, 7=magenta, 8=cyan, 9=white
        0x59A0, 0x7C1F, 0x77A0, 0x7FFF,
        // 10=light grey, 11=medium grey, 12=dark grey
        0x5AD6, 0x4631, 0x2D6B,
    };

    // Non-standard colour ring buffer: tracks up to 240 true colour values,
    // mapped to colour numbers 16–255.
    private readonly ushort[] _nonStandardRing = new ushort[240];
    private int _nonStandardCount;
    private int _nonStandardNext;

    private readonly int _version;
    private readonly Memory _memory;
    private ushort _defaultTrueFg;
    private ushort _defaultTrueBg;

    /// <summary>Current true foreground colour (15-bit sRGB or magic).</summary>
    public int TrueForeground { get; private set; }

    /// <summary>Current true background colour (15-bit sRGB or magic).</summary>
    public int TrueBackground { get; private set; }

    /// <summary>
    /// Whether the game requests transparency support (Flags 3 bit 0).
    /// </summary>
    public bool TransparencyRequested { get; private set; }

    /// <summary>
    /// Callback invoked after true colour changes with (fg, bg) as
    /// standard colour numbers (2–12 for standard, 16–255 for non-standard).
    /// </summary>
    public Action<int, int>? OnColourChanged { get; set; }

    /// <summary>
    /// Creates a TrueColourManager, reading default true colours from
    /// the header extension table if present.
    /// </summary>
    public TrueColourManager(Memory memory, int version, HeaderExtension? headerExtension)
    {
        _memory = memory;
        _version = version;

        _defaultTrueFg = headerExtension?.TrueDefaultForeground ?? 0;
        _defaultTrueBg = headerExtension?.TrueDefaultBackground ?? 0;

        // If no defaults in the header extension, use white-on-black.
        if (_defaultTrueFg == 0) _defaultTrueFg = 0x7FFF;
        if (_defaultTrueBg == 0) _defaultTrueBg = 0x0000;

        TrueForeground = _defaultTrueFg;
        TrueBackground = _defaultTrueBg;

        TransparencyRequested = headerExtension != null &&
                                (headerExtension.Flags3 & 0x01) != 0;
    }

    /// <summary>
    /// EXT:13 <c>@set_true_colour fg bg</c> — sets the true foreground and
    /// background colours. Values are 15-bit sRGB (0x0000–0x7FFF) or magic
    /// values: -1=default, -2=current, -3=colour under cursor (V6),
    /// -4=transparent (V6 background only).
    /// </summary>
    /// <returns>
    /// A diagnostic message if transparent foreground is attempted, null otherwise.
    /// </returns>
    /// <remarks>
    /// ZSpec11 "@set_true_colour" — magic values, V6 transparency,
    /// window properties 16/17 update.
    /// </remarks>
    public string? SetTrueColour(int fg, int bg)
    {
        string? diagnostic = null;

        // Foreground
        if (fg == -1)
            TrueForeground = _defaultTrueFg;
        else if (fg == -2)
        { /* current — no change */ }
        else if (fg == -4)
            diagnostic = "Transparent foreground is not valid (ZSpec11 \"@set_colour\")";
        else if (fg >= 0)
            TrueForeground = fg;

        // Background
        if (bg == -1)
            TrueBackground = _defaultTrueBg;
        else if (bg == -2)
        { /* current — no change */ }
        else if (bg == -4 && _version == 6)
            TrueBackground = -4;
        else if (bg >= 0)
            TrueBackground = bg;

        return diagnostic;
    }

    /// <summary>
    /// Returns the standard colour number (2–12) for a given 15-bit true colour,
    /// or a non-standard colour number (16–255) if no standard match exists.
    /// Allocates a new non-standard slot if needed, wrapping after 240.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Colour numbers" — colours 16–255 represent the last 240
    /// distinct non-standard colours, re-using numbers after exhaustion.
    /// </remarks>
    public int TrueColourToNumber(int trueColour)
    {
        if (trueColour == -4)
            return 15; // transparent

        if (trueColour < 0)
            return 1; // default

        // Check standard colours 2–12
        for (int i = 2; i <= 12; i++)
        {
            if (StandardTrueColours[i] == (ushort)trueColour)
                return i;
        }

        // Check existing non-standard entries
        int limit = Math.Min(_nonStandardCount, 240);
        for (int i = 0; i < limit; i++)
        {
            if (_nonStandardRing[i] == (ushort)trueColour)
                return 16 + i;
        }

        // Allocate a new non-standard slot
        int slot = _nonStandardNext;
        _nonStandardRing[slot] = (ushort)trueColour;
        _nonStandardNext = (_nonStandardNext + 1) % 240;
        if (_nonStandardCount < 240)
            _nonStandardCount++;

        return 16 + slot;
    }

    /// <summary>
    /// Returns the 15-bit true colour value for a standard colour number (2–12),
    /// or the stored value for a non-standard colour (16–255). Returns 0 for
    /// unknown or out-of-range colour numbers.
    /// </summary>
    public int NumberToTrueColour(int colourNumber)
    {
        if (colourNumber == 1)
            return _defaultTrueFg; // default — context-dependent, return fg default

        if (colourNumber == 15)
            return -4; // transparent

        if (colourNumber >= 2 && colourNumber <= 12)
            return StandardTrueColours[colourNumber];

        int slot = colourNumber - 16;
        if (slot >= 0 && slot < _nonStandardCount)
            return _nonStandardRing[slot];

        return 0;
    }

    /// <summary>
    /// Returns the 15-bit true colour value for a standard colour number (2–12).
    /// Returns 0 for out-of-range values.
    /// </summary>
    public static int GetStandardTrueColour(int colourNumber)
    {
        if (colourNumber >= 2 && colourNumber <= 12)
            return StandardTrueColours[colourNumber];
        return 0;
    }

    /// <summary>
    /// Returns whether the given background true colour value represents
    /// transparency (-4 magic value or colour 15).
    /// </summary>
    public static bool IsTransparent(int trueColour)
    {
        return trueColour == -4;
    }
}
