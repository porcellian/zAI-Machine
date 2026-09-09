namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// IBM PC CGA/EGA 16-color text mode theme — the classic DOS gaming look.
/// Renders an 80×25 character grid with the full CGA 16-color palette
/// and EGA 8×14 font. Default DOS colors: light grey text on black
/// background with white-on-blue status line.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model is presented with the standard CGA/EGA
/// 16-color palette. Z-Machine colors 2–15 map directly to the CGA
/// color indices, providing full color support for games that use
/// @set_colour (ZSpec S8.3.1).
/// </remarks>
public class DosColorTheme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public string Name => "DOS Color";

    /// <summary>
    /// CGA/EGA 16-color palette mapped to Z-Machine colors 2–15.
    /// Standard IBM PC text mode colors matching the original CGA adapter.
    /// </summary>
    public static readonly SKColor[] CgaPalette =
    [
        new(0x00, 0x00, 0x00), //  2  Black          (CGA 0)
        new(0xAA, 0x00, 0x00), //  3  Red            (CGA 4)
        new(0x00, 0xAA, 0x00), //  4  Green          (CGA 2)
        new(0xFF, 0xFF, 0x55), //  5  Yellow          (CGA 14)
        new(0x00, 0x00, 0xAA), //  6  Blue           (CGA 1)
        new(0xAA, 0x00, 0xAA), //  7  Magenta        (CGA 5)
        new(0x00, 0xAA, 0xAA), //  8  Cyan           (CGA 3)
        new(0xFF, 0xFF, 0xFF), //  9  White          (CGA 15)
        new(0xAA, 0xAA, 0xAA), // 10  Light grey     (CGA 7)
        new(0x55, 0x55, 0x55), // 11  Medium grey    (CGA 8)
        new(0x55, 0x55, 0x55), // 12  Dark grey      (CGA 8)
        new(0xAA, 0x55, 0x00), // 13  Brown/Orange   (CGA 6)
        new(0xFF, 0x55, 0xFF), // 14  Light magenta  (CGA 13)
        new(0x55, 0xFF, 0xFF), // 15  Light cyan     (CGA 11)
    ];

    /// <summary>
    /// Creates a ThemeConfig for DOS CGA/EGA color text mode.
    /// 80 columns × 25 rows with EGA 8×14 font, light grey on black
    /// default colors matching standard DOS text mode.
    /// </summary>
    public ThemeConfig CreateConfig()
    {
        var font = FontData.CreateEGA();

        return new ThemeConfig
        {
            Name = Name,
            CharWidth = 8,
            CharHeight = 14,
            Columns = 80,
            Rows = 25,
            BorderWidth = 8,
            ColorPalette = CgaPalette,
            DefaultForeground = 10, // Light grey (CGA 7 — standard DOS text)
            DefaultBackground = 2,  // Black
            BorderColor = SKColors.Black,
            Chrome = ChromeMode.Borderless,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
            PhosphorBloom = false,
        };
    }
}
