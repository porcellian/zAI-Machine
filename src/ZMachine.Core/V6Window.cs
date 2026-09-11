namespace ZMachine.Core;

/// <summary>
/// Represents one of the 8 independent windows in a V6 Z-Machine.
/// Each window has 18 properties (ZSpec S8.8) covering position,
/// size, cursor, margins, styling, and attributes.
/// </summary>
/// <remarks>
/// ZSpec S8.8, ZSpec11 "Version 6 windows" — all 8 windows are
/// treated identically; differences arise only from default values
/// and @split_window targeting windows 0 and 1.
/// </remarks>
public class V6Window
{
    /// <summary>Window number (0–7).</summary>
    public int Number { get; }

    /// <summary>Property 0: Y position in pixels (top of window).</summary>
    public int Y { get; set; }

    /// <summary>Property 1: X position in pixels (left of window).</summary>
    public int X { get; set; }

    /// <summary>Property 2: Height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Property 3: Width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Property 4: Cursor Y position within window (1-based).</summary>
    public int CursorY { get; set; } = 1;

    /// <summary>Property 5: Cursor X position within window (1-based).</summary>
    public int CursorX { get; set; } = 1;

    /// <summary>Property 6: Left margin in pixels.</summary>
    public int LeftMargin { get; set; }

    /// <summary>Property 7: Right margin in pixels.</summary>
    public int RightMargin { get; set; }

    /// <summary>Property 8: Newline interrupt routine address.</summary>
    public int NewlineInterrupt { get; set; }

    /// <summary>Property 9: Interrupt countdown.</summary>
    public int InterruptCountdown { get; set; }

    /// <summary>
    /// Property 10: Current text style bitmask.
    /// 0=Roman, 1=Reverse, 2=Bold, 4=Italic, 8=Fixed-pitch.
    /// </summary>
    public int TextStyle { get; set; }

    /// <summary>
    /// Property 11: Colour data. High byte = background colour number,
    /// low byte = foreground colour number. Values >= 16 indicate
    /// non-standard colours from true colour or "under the cursor".
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Colour numbers" — the interpreter tracks the last
    /// 240 distinct non-standard colours.
    /// </remarks>
    public int ColourData { get; set; }

    /// <summary>Property 12: Font number (1=normal, 3=character graphics, 4=fixed-pitch).</summary>
    public int Font { get; set; } = 1;

    /// <summary>
    /// Property 13: Font size. High byte = height, low byte = width
    /// (both in pixels, as reported to the game).
    /// </summary>
    public int FontSize { get; set; }

    /// <summary>
    /// Property 14: Window attributes bitmask.
    /// Bit 0 = wrapping, bit 1 = scrolling, bit 2 = transcript,
    /// bit 3 = buffered.
    /// </summary>
    public int Attributes { get; set; }

    /// <summary>Property 15: Line count (for MORE prompting).</summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Property 16: True foreground colour (read-only via @get_wind_prop).
    /// 15-bit sRGB: bits 14–10=blue, 9–5=green, 4–0=red.
    /// -4 = transparent. ZSpec11 "@get_wind_prop".
    /// </summary>
    public int TrueForeground { get; set; }

    /// <summary>
    /// Property 17: True background colour (read-only via @get_wind_prop).
    /// Same encoding as TrueForeground. ZSpec11 "@get_wind_prop".
    /// </summary>
    public int TrueBackground { get; set; }

    /// <summary>Creates a V6 window with the given number.</summary>
    public V6Window(int number)
    {
        Number = number;
    }

    /// <summary>
    /// Gets a window property by index (0–17).
    /// ZSpec S8.8 property table.
    /// </summary>
    public int GetProperty(int index)
    {
        return index switch
        {
            0 => Y,
            1 => X,
            2 => Height,
            3 => Width,
            4 => CursorY,
            5 => CursorX,
            6 => LeftMargin,
            7 => RightMargin,
            8 => NewlineInterrupt,
            9 => InterruptCountdown,
            10 => TextStyle,
            11 => ColourData,
            12 => Font,
            13 => FontSize,
            14 => Attributes,
            15 => LineCount,
            16 => TrueForeground,
            17 => TrueBackground,
            _ => 0,
        };
    }

    /// <summary>
    /// Sets a window property by index (0–15). Properties 16–17
    /// are read-only (true colours) and cannot be set via this method.
    /// ZSpec11 "@get_wind_prop" — properties 16/17 not writable.
    /// </summary>
    /// <returns>True if the property was set, false if read-only or invalid.</returns>
    public bool SetProperty(int index, int value)
    {
        switch (index)
        {
            case 0: Y = value; return true;
            case 1: X = value; return true;
            case 2: Height = value; return true;
            case 3: Width = value; return true;
            case 4: CursorY = value; return true;
            case 5: CursorX = value; return true;
            case 6: LeftMargin = value; return true;
            case 7: RightMargin = value; return true;
            case 8: NewlineInterrupt = value; return true;
            case 9: InterruptCountdown = value; return true;
            case 10: TextStyle = value; return true;
            case 11: ColourData = value; return true;
            case 12: Font = value; return true;
            case 13: FontSize = value; return true;
            case 14: Attributes = value; return true;
            case 15: LineCount = value; return true;
            // Properties 16–17 are read-only
            default: return false;
        }
    }

    /// <summary>Attribute bit 0: word wrapping enabled.</summary>
    public bool Wrapping
    {
        get => (Attributes & 0x01) != 0;
        set => Attributes = value ? (Attributes | 0x01) : (Attributes & ~0x01);
    }

    /// <summary>Attribute bit 1: scrolling enabled.</summary>
    public bool Scrolling
    {
        get => (Attributes & 0x02) != 0;
        set => Attributes = value ? (Attributes | 0x02) : (Attributes & ~0x02);
    }

    /// <summary>Attribute bit 2: transcript (copy to stream 2).</summary>
    public bool Transcript
    {
        get => (Attributes & 0x04) != 0;
        set => Attributes = value ? (Attributes | 0x04) : (Attributes & ~0x04);
    }

    /// <summary>Attribute bit 3: buffered output.</summary>
    public bool Buffered
    {
        get => (Attributes & 0x08) != 0;
        set => Attributes = value ? (Attributes | 0x08) : (Attributes & ~0x08);
    }
}
