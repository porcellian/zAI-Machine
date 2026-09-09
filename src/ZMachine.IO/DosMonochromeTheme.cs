namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Base class for DOS monochrome display themes (MDA/Hercules style).
/// Provides 80×25 character grid with EGA 8×14 font and monochrome
/// phosphor rendering. Subclasses supply the phosphor color.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model is presented in monochrome with MDA-style
/// text attributes: bold maps to intensified phosphor, italic maps to
/// underline rendering, reverse swaps fg/bg.
/// </remarks>
public abstract class DosMonochromeTheme : ITheme
{
    /// <summary>Display name for theme selection.</summary>
    public abstract string Name { get; }

    /// <summary>Normal phosphor foreground color.</summary>
    protected abstract SKColor PhosphorNormal { get; }

    /// <summary>Intensified (bright) phosphor color for bold text.</summary>
    protected abstract SKColor PhosphorBright { get; }

    /// <summary>Dark background color simulating an unlit phosphor screen.</summary>
    protected abstract SKColor PhosphorBackground { get; }

    /// <summary>
    /// Builds a monochrome palette where all Z-Machine colors map to the
    /// normal phosphor color, except black which maps to background.
    /// </summary>
    protected SKColor[] BuildMonochromePalette()
    {
        return
        [
            PhosphorBackground, //  2  Black → background
            PhosphorNormal,     //  3  Red → phosphor
            PhosphorNormal,     //  4  Green → phosphor
            PhosphorNormal,     //  5  Yellow → phosphor
            PhosphorNormal,     //  6  Blue → phosphor
            PhosphorNormal,     //  7  Magenta → phosphor
            PhosphorNormal,     //  8  Cyan → phosphor
            PhosphorBright,     //  9  White → bright phosphor
            PhosphorNormal,     // 10  Light grey → phosphor
            PhosphorNormal,     // 11  Medium grey → phosphor
            PhosphorNormal,     // 12  Dark grey → phosphor
            PhosphorNormal,     // 13  Orange → phosphor
            PhosphorNormal,     // 14  (reserved) → phosphor
            PhosphorNormal,     // 15  (reserved) → phosphor
        ];
    }

    /// <summary>
    /// Creates a ThemeConfig for this DOS monochrome theme.
    /// 80 columns × 25 rows with EGA 8×14 font, matching MDA resolution.
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
            ColorPalette = BuildMonochromePalette(),
            DefaultForeground = 9,  // White → bright phosphor
            DefaultBackground = 2,  // Black → background
            BorderColor = PhosphorBackground,
            Chrome = ChromeMode.Borderless,
            Font = font,
            Scanlines = false,
            CrtCurvature = false,
            PhosphorBloom = false,
        };
    }
}

/// <summary>
/// IBM MDA/Hercules green phosphor monitor theme. Renders all text in
/// green-on-dark using the classic P1 phosphor colors with intensified
/// green for bold/bright text.
/// </summary>
public class DosGreenTheme : DosMonochromeTheme
{
    /// <summary>Display name for theme selection.</summary>
    public override string Name => "DOS Green";

    /// <summary>Normal P1 green phosphor.</summary>
    protected override SKColor PhosphorNormal => new(0x33, 0xFF, 0x33);

    /// <summary>Intensified green for bold text.</summary>
    protected override SKColor PhosphorBright => new(0x66, 0xFF, 0x66);

    /// <summary>Dark green background.</summary>
    protected override SKColor PhosphorBackground => new(0x0A, 0x1A, 0x0A);
}

/// <summary>
/// IBM amber phosphor monitor theme. Same layout as the green screen
/// but with amber/orange phosphor colors matching the P3 amber CRT.
/// </summary>
public class DosAmberTheme : DosMonochromeTheme
{
    /// <summary>Display name for theme selection.</summary>
    public override string Name => "DOS Amber";

    /// <summary>Normal amber phosphor.</summary>
    protected override SKColor PhosphorNormal => new(0xFF, 0xB0, 0x00);

    /// <summary>Intensified amber for bold text.</summary>
    protected override SKColor PhosphorBright => new(0xFF, 0xD0, 0x60);

    /// <summary>Dark brown background.</summary>
    protected override SKColor PhosphorBackground => new(0x1A, 0x0F, 0x00);
}
