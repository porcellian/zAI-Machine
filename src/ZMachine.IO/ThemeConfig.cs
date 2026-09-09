namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Configuration for a vintage display theme. Defines the font data,
/// color palette, screen dimensions, and visual effects that a renderer
/// uses to draw the Z-Machine display.
/// </summary>
/// <remarks>
/// ZSpec S8.1 — The screen model's visual parameters vary by theme.
/// Each theme provides a ThemeConfig that the SkiaRenderer consumes.
/// </remarks>
public class ThemeConfig
{
    /// <summary>Display name shown in the theme selector menu.</summary>
    public string Name { get; init; } = "Default";

    /// <summary>Width of each character cell in pixels.</summary>
    public int CharWidth { get; init; } = 8;

    /// <summary>Height of each character cell in pixels.</summary>
    public int CharHeight { get; init; } = 16;

    /// <summary>Number of character columns on screen.</summary>
    public int Columns { get; init; } = 80;

    /// <summary>Number of character rows on screen.</summary>
    public int Rows { get; init; } = 25;

    /// <summary>Border width in pixels around the character grid.</summary>
    public int BorderWidth { get; init; } = 8;

    /// <summary>
    /// Z-Machine color palette mapping colors 2–15 to RGB values.
    /// Index 0 = color 2 (black), index 13 = color 15.
    /// ZSpec S8.3.1 — True colour table.
    /// </summary>
    public SKColor[] ColorPalette { get; init; } = DefaultPalette;

    /// <summary>Default foreground Z-Machine color number (2–15).</summary>
    public int DefaultForeground { get; init; } = 9; // White

    /// <summary>Default background Z-Machine color number (2–15).</summary>
    public int DefaultBackground { get; init; } = 2; // Black

    /// <summary>Border color.</summary>
    public SKColor BorderColor { get; init; } = SKColors.Black;

    /// <summary>Whether to render CRT scanline effects.</summary>
    public bool Scanlines { get; init; }

    /// <summary>Whether to apply CRT curvature distortion.</summary>
    public bool CrtCurvature { get; init; }

    /// <summary>Whether to apply phosphor bloom/glow on bright pixels.</summary>
    public bool PhosphorBloom { get; init; }

    /// <summary>
    /// Window chrome mode: borderless fills the entire window with the
    /// character grid; standard uses a windowed layout with native chrome.
    /// </summary>
    public ChromeMode Chrome { get; init; } = ChromeMode.Borderless;

    /// <summary>
    /// BitmapFont used for rendering. When set, overrides FontBitmap and
    /// determines CharWidth/CharHeight for pixel rendering.
    /// </summary>
    public BitmapFont? Font { get; init; }

    /// <summary>Bitmap font data (1-bit-per-pixel), or null for built-in.</summary>
    public byte[]? FontBitmap { get; init; }

    /// <summary>First character code in the font bitmap (typically 32).</summary>
    public int FontFirstChar { get; init; } = 32;

    /// <summary>Number of character glyphs in the font bitmap.</summary>
    public int FontGlyphCount { get; init; } = 95;

    /// <summary>
    /// Resolves a Z-Machine color number (2–15) to an SKColor.
    /// Colors 0 (current) and 1 (default) are resolved by the caller.
    /// </summary>
    public SKColor GetColor(int zColor)
    {
        int index = zColor - 2;
        if (index >= 0 && index < ColorPalette.Length)
            return ColorPalette[index];
        return SKColors.White;
    }

    /// <summary>Total pixel width including borders.</summary>
    public int PixelWidth => BorderWidth * 2 + Columns * CharWidth;

    /// <summary>Total pixel height including borders.</summary>
    public int PixelHeight => BorderWidth * 2 + Rows * CharHeight;

    /// <summary>
    /// ZSpec S8.3.1 — Standard Z-Machine true colour table (colors 2–15).
    /// </summary>
    public static readonly SKColor[] DefaultPalette =
    [
        new(0x00, 0x00, 0x00), // 2  Black
        new(0xEF, 0x08, 0x08), // 3  Red
        new(0x00, 0xD0, 0x00), // 4  Green
        new(0xE6, 0xDA, 0x00), // 5  Yellow
        new(0x00, 0x6B, 0xB5), // 6  Blue
        new(0xFF, 0x00, 0xFF), // 7  Magenta
        new(0x00, 0xE8, 0xD4), // 8  Cyan
        new(0xFF, 0xFF, 0xFF), // 9  White
        new(0xBB, 0xBB, 0xBB), // 10 Light grey
        new(0x88, 0x88, 0x88), // 11 Medium grey
        new(0x44, 0x44, 0x44), // 12 Dark grey
        new(0xFF, 0x80, 0x00), // 13 Orange (V6+ extension — unused in V1-5)
        new(0x80, 0x80, 0x00), // 14 (reserved)
        new(0x40, 0x40, 0x40), // 15 (reserved)
    ];
}

/// <summary>
/// Window chrome mode for themes.
/// </summary>
public enum ChromeMode
{
    /// <summary>Character grid fills the entire window (retro fullscreen).</summary>
    Borderless,

    /// <summary>Standard windowed layout with native menu bar and chrome.</summary>
    Standard
}
