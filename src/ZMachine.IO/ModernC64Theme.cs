namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Modern windowed take on the C64 aesthetic. Standard OS window chrome
/// with menu bar, 80-column layout, bright blue background, and white
/// text. No CRT effects — a clean, modern-retro presentation.
/// Visual reference: examples/zork_i_modC64.jpg.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model is presented in a modern windowed layout
/// with wider columns than the original C64, matching contemporary
/// terminal proportions while retaining the iconic blue-on-white palette.
/// </remarks>
public class ModernC64Theme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public string Name => "Modern C64";

    /// <summary>
    /// Bright blue palette inspired by the C64 but with more saturated,
    /// modern colors. Mapped to Z-Machine colors 2–15.
    /// </summary>
    public static readonly SKColor[] ModernC64Palette =
    [
        new(0x00, 0x00, 0x00), //  2  Black
        new(0xCC, 0x33, 0x33), //  3  Red
        new(0x33, 0xCC, 0x33), //  4  Green
        new(0xCC, 0xCC, 0x33), //  5  Yellow
        new(0x00, 0x50, 0xA4), //  6  Blue (bright blue — background)
        new(0xCC, 0x33, 0xCC), //  7  Magenta
        new(0x33, 0xCC, 0xCC), //  8  Cyan
        new(0xFF, 0xFF, 0xFF), //  9  White
        new(0xCC, 0xCC, 0xCC), // 10  Light grey
        new(0x88, 0x88, 0x88), // 11  Medium grey
        new(0x44, 0x44, 0x44), // 12  Dark grey
        new(0xFF, 0x88, 0x00), // 13  Orange
        new(0x66, 0xAA, 0xFF), // 14  Light blue
        new(0xAA, 0xDD, 0xAA), // 15  Light green
    ];

    /// <summary>
    /// Creates a ThemeConfig for the modern C64 windowed layout.
    /// 80 columns × 30 rows with VGA 8×16 font for comfortable
    /// reading in a standard window.
    /// </summary>
    public ThemeConfig CreateConfig()
    {
        var font = FontData.CreateVGA();

        return new ThemeConfig
        {
            Name = Name,
            CharWidth = 8,
            CharHeight = 16,
            Columns = 80,
            Rows = 30,
            BorderWidth = 4,
            ColorPalette = ModernC64Palette,
            DefaultForeground = 9,  // White
            DefaultBackground = 6,  // Bright blue
            BorderColor = new SKColor(0x00, 0x50, 0xA4), // Match background
            Chrome = ChromeMode.Standard,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
            PhosphorBloom = false,
        };
    }
}
