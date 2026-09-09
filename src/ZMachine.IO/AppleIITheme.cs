namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Classic Apple II monochrome phosphor display theme. Renders a 40×24
/// character grid using the Apple II 7×8 bitmap font with P1 green
/// phosphor colors. All Z-Machine colors map to the single phosphor
/// foreground — no color support, matching the original hardware.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model is presented in monochrome: all color
/// requests are ignored and rendered in the phosphor foreground color.
/// Reverse video (ZSpec S8.7.1) swaps foreground and background as
/// the only visual distinction beyond text content.
/// </remarks>
public class AppleIITheme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public string Name => "Apple II";

    /// <summary>P1 phosphor green foreground color.</summary>
    public static readonly SKColor PhosphorGreen = new(0x33, 0xFF, 0x33);

    /// <summary>Dark green background simulating an unlit phosphor screen.</summary>
    public static readonly SKColor PhosphorBackground = new(0x00, 0x11, 0x00);

    /// <summary>
    /// Monochrome palette: all 14 Z-Machine color slots (2–15) map to
    /// phosphor green. The only visual distinction is reverse video,
    /// which swaps foreground and background. Color 2 (black) maps to
    /// the dark phosphor background for correct reverse-video rendering.
    /// </summary>
    public static readonly SKColor[] MonochromePalette =
    [
        new(0x00, 0x11, 0x00), //  2  Black → phosphor background
        new(0x33, 0xFF, 0x33), //  3  Red → phosphor green
        new(0x33, 0xFF, 0x33), //  4  Green → phosphor green
        new(0x33, 0xFF, 0x33), //  5  Yellow → phosphor green
        new(0x33, 0xFF, 0x33), //  6  Blue → phosphor green
        new(0x33, 0xFF, 0x33), //  7  Magenta → phosphor green
        new(0x33, 0xFF, 0x33), //  8  Cyan → phosphor green
        new(0x33, 0xFF, 0x33), //  9  White → phosphor green
        new(0x33, 0xFF, 0x33), // 10  Light grey → phosphor green
        new(0x33, 0xFF, 0x33), // 11  Medium grey → phosphor green
        new(0x33, 0xFF, 0x33), // 12  Dark grey → phosphor green
        new(0x33, 0xFF, 0x33), // 13  Orange → phosphor green
        new(0x33, 0xFF, 0x33), // 14  (reserved) → phosphor green
        new(0x33, 0xFF, 0x33), // 15  (reserved) → phosphor green
    ];

    /// <summary>
    /// Creates a ThemeConfig for the Apple II display.
    /// 280×192 logical resolution at 40 columns × 24 rows with the
    /// Apple II 7×8 character generator font.
    /// </summary>
    public ThemeConfig CreateConfig()
    {
        var font = FontData.CreateAppleII();

        return new ThemeConfig
        {
            Name = Name,
            CharWidth = 7,
            CharHeight = 8,
            Columns = 40,
            Rows = 24,
            BorderWidth = 24,
            ColorPalette = MonochromePalette,
            DefaultForeground = 9,  // White → phosphor green
            DefaultBackground = 2,  // Black → phosphor background
            BorderColor = PhosphorBackground,
            Chrome = ChromeMode.Borderless,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
            PhosphorBloom = false,
        };
    }
}
