namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Authentic Commodore 64 display theme. Renders a 40×25 character grid
/// using the C64 8×8 bitmap font with the original VIC-II 16-color palette.
/// Visual reference: examples/zork_i_c64.png.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model is presented in C64 style:
/// 320×200 logical pixels in a 40-column layout with medium blue
/// background/border and light blue text, matching the original
/// Infocom C64 releases.
/// </remarks>
public class C64Theme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public string Name => "C64 Classic";

    /// <summary>
    /// VIC-II 16-color palette mapped to Z-Machine colors 2–15.
    /// Colors sourced from the VICE emulator's PAL palette.
    /// The two extra C64 colors (indices 14–15) map to the
    /// Z-Machine's reserved color slots.
    /// </summary>
    public static readonly SKColor[] C64Palette =
    [
        new(0x00, 0x00, 0x00), //  2  Black         (C64 color 0)
        new(0x68, 0x37, 0x2B), //  3  Red           (C64 color 2 — brown/red)
        new(0x58, 0x8D, 0x43), //  4  Green         (C64 color 5)
        new(0xB8, 0xC7, 0x6F), //  5  Yellow        (C64 color 7 — yellow)
        new(0x40, 0x31, 0x8D), //  6  Blue          (C64 color 6 — medium blue)
        new(0x8B, 0x3F, 0x96), //  7  Magenta       (C64 color 4 — purple)
        new(0x70, 0xA4, 0xB2), //  8  Cyan          (C64 color 3 — cyan)
        new(0xFF, 0xFF, 0xFF), //  9  White         (C64 color 1)
        new(0xB8, 0xC7, 0x6F), // 10  Light grey    (C64 color 13 — light green)
        new(0x6F, 0x6F, 0x6F), // 11  Medium grey   (C64 color 12 — medium grey)
        new(0x44, 0x44, 0x44), // 12  Dark grey     (C64 color 11 — dark grey)
        new(0xA0, 0x57, 0x00), // 13  Orange        (C64 color 8)
        new(0x78, 0x69, 0xC4), // 14  Light blue    (C64 color 14 — light blue)
        new(0x9A, 0xD2, 0x84), // 15  Light green   (C64 color 13)
    ];

    /// <summary>
    /// Creates a ThemeConfig with C64 screen dimensions, palette, and font.
    /// 320×200 logical resolution at 40 columns × 25 rows with 32-pixel
    /// side borders and 4-pixel top/bottom borders (matching PAL output).
    /// </summary>
    public ThemeConfig CreateConfig()
    {
        var font = FontData.CreateC64();

        return new ThemeConfig
        {
            Name = Name,
            CharWidth = 8,
            CharHeight = 8,
            Columns = 40,
            Rows = 25,
            BorderWidth = 32,
            ColorPalette = C64Palette,
            DefaultForeground = 14, // Light blue (C64 color 14)
            DefaultBackground = 6,  // Medium blue (C64 color 6)
            BorderColor = new SKColor(0x40, 0x31, 0x8D), // Medium blue border
            Chrome = ChromeMode.Borderless,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
        };
    }
}
