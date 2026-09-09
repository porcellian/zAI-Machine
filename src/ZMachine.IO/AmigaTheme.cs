namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Amiga Workbench 1.x-inspired theme with the original Infocom V6 color
/// set. Renders an 80×25 character grid using the Amiga Topaz 8×8 font
/// with the gamma-adjusted Amiga palette from ZSpec11.
/// </summary>
/// <remarks>
/// ZSpec11 "Colour numbers" — The colour palette uses the original Amiga
/// Version 6 colour set, gamma-adjusted from 8-bit Amiga values assuming
/// system gamma of 1/1.8: Z = 31 * [(Amiga / 15) ^ (1.8/2.2)].
/// The Workbench 1.x chrome provides the classic blue/white/orange look.
/// </remarks>
public class AmigaTheme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public string Name => "Amiga Workbench";

    /// <summary>
    /// Amiga V6 colour set from ZSpec11, gamma-adjusted to sRGB.
    /// Each 15-bit true colour (bits 14–10 blue, 9–5 green, 4–0 red)
    /// is expanded to 8-bit per channel via (val &lt;&lt; 3) | (val &gt;&gt; 2).
    /// </summary>
    public static readonly SKColor[] AmigaPalette =
    [
        // ZSpec11 true colour values, gamma-adjusted from Amiga originals
        new(0x00, 0x00, 0x00), //  2  Black        ($0000)
        new(0xEF, 0x00, 0x00), //  3  Red          ($001D)
        new(0x00, 0xD6, 0x00), //  4  Green        ($0340)
        new(0xEF, 0xEF, 0x00), //  5  Yellow       ($03BD)
        new(0x00, 0x6B, 0xB5), //  6  Blue         ($59A0)
        new(0xFF, 0x00, 0xFF), //  7  Magenta      ($7C1F)
        new(0x00, 0xEF, 0xEF), //  8  Cyan         ($77A0)
        new(0xFF, 0xFF, 0xFF), //  9  White        ($7FFF)
        new(0xB5, 0xB5, 0xB5), // 10  Light grey   ($5AD6)
        new(0x8C, 0x8C, 0x8C), // 11  Medium grey  ($4631)
        new(0x5A, 0x5A, 0x5A), // 12  Dark grey    ($2D6B)
        new(0xFF, 0x88, 0x00), // 13  WB orange    (Workbench accent)
        new(0x00, 0x55, 0xAA), // 14  WB blue      (Workbench background)
        new(0xAA, 0xAA, 0xAA), // 15  WB grey      (Workbench panel)
    ];

    /// <summary>Workbench 1.x desktop blue for borders and chrome.</summary>
    public static readonly SKColor WorkbenchBlue = new(0x00, 0x55, 0xAA);

    /// <summary>
    /// Creates a ThemeConfig for the Amiga Workbench theme.
    /// 80 columns × 25 rows with Topaz 8×8 font. The Workbench 1.x
    /// blue border and standard chrome give the classic Amiga frame.
    /// </summary>
    public ThemeConfig CreateConfig()
    {
        var font = FontData.CreateAmiga();

        return new ThemeConfig
        {
            Name = Name,
            CharWidth = 8,
            CharHeight = 8,
            Columns = 80,
            Rows = 25,
            BorderWidth = 16,
            ColorPalette = AmigaPalette,
            DefaultForeground = 9,  // White
            DefaultBackground = 2,  // Black
            BorderColor = WorkbenchBlue,
            Chrome = ChromeMode.Standard,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
            PhosphorBloom = false,
        };
    }
}
