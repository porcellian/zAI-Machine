namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.4 — Modern C64 Theme: windowed layout with standard
/// chrome, 80-column display, bright blue background, white text.
/// </summary>
public class ModernC64ThemeTests
{
    #region ITheme Interface

    /// <summary>
    /// ModernC64Theme implements ITheme.
    /// </summary>
    [Fact]
    public void ModernC64Theme_ImplementsITheme()
    {
        ITheme theme = new ModernC64Theme();
        Assert.NotNull(theme);
        Assert.Equal("Modern C64", theme.Name);
    }

    /// <summary>
    /// CreateConfig returns a valid ThemeConfig.
    /// </summary>
    [Fact]
    public void ModernC64Theme_CreateConfig_ReturnsConfig()
    {
        var theme = new ModernC64Theme();
        var config = theme.CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("Modern C64", config.Name);
    }

    #endregion

    #region Screen Dimensions

    /// <summary>
    /// Modern C64 uses 80 columns × 30 rows for a wider, modern layout.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Dimensions_80x30()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(80, config.Columns);
        Assert.Equal(30, config.Rows);
    }

    /// <summary>
    /// Uses VGA 8×16 font for comfortable reading at modern resolutions.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Font_VGA8x16()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(16, config.CharHeight);
    }

    /// <summary>
    /// Minimal border (4px) — text extends nearly edge to edge.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Border_Minimal()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(4, config.BorderWidth);
    }

    /// <summary>
    /// Pixel dimensions are correct for 80×30 grid with 4px border.
    /// </summary>
    [Fact]
    public void ModernC64Theme_PixelDimensions_Correct()
    {
        var config = new ModernC64Theme().CreateConfig();
        // 4*2 + 80*8 = 648
        Assert.Equal(648, config.PixelWidth);
        // 4*2 + 30*16 = 488
        Assert.Equal(488, config.PixelHeight);
    }

    #endregion

    #region Color Palette

    /// <summary>
    /// Palette has 14 entries (Z-Machine colors 2–15).
    /// </summary>
    [Fact]
    public void ModernC64Theme_Palette_14Entries()
    {
        Assert.Equal(14, ModernC64Theme.ModernC64Palette.Length);
    }

    /// <summary>
    /// Default foreground is white (color 9), matching the screenshot.
    /// </summary>
    [Fact]
    public void ModernC64Theme_DefaultForeground_White()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(9, config.DefaultForeground);
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
    }

    /// <summary>
    /// Default background is bright blue (color 6), matching the screenshot.
    /// </summary>
    [Fact]
    public void ModernC64Theme_DefaultBackground_BrightBlue()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(6, config.DefaultBackground);
        Assert.Equal(new SKColor(0x00, 0x50, 0xA4), config.GetColor(6));
    }

    /// <summary>
    /// Border color matches the bright blue background.
    /// </summary>
    [Fact]
    public void ModernC64Theme_BorderColor_MatchesBackground()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x50, 0xA4), config.BorderColor);
    }

    /// <summary>
    /// Black is color 2 (index 0).
    /// </summary>
    [Fact]
    public void ModernC64Theme_Palette_BlackIsColor2()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0x00), config.GetColor(2));
    }

    #endregion

    #region Chrome and Effects

    /// <summary>
    /// Modern C64 uses standard chrome (OS window with title bar and menu).
    /// </summary>
    [Fact]
    public void ModernC64Theme_Chrome_Standard()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.Equal(ChromeMode.Standard, config.Chrome);
    }

    /// <summary>
    /// No CRT post-processing effects — clean modern presentation.
    /// </summary>
    [Fact]
    public void ModernC64Theme_NoPostProcessing()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    #endregion

    #region Font

    /// <summary>
    /// Uses VGA BitmapFont for clean, legible rendering.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Font_IsVGA()
    {
        var config = new ModernC64Theme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("VGA 8×16", config.Font!.Name);
    }

    #endregion

    #region Renderer Integration

    /// <summary>
    /// SkiaRenderer initializes correctly with Modern C64 dimensions.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new ModernC64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(648, renderer.BackBuffer!.Width);
        Assert.Equal(488, renderer.BackBuffer.Height);

        var (cols, rows) = renderer.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(30, rows);
    }

    /// <summary>
    /// Drawing text produces white pixels on bright blue background.
    /// </summary>
    [Fact]
    public void ModernC64Theme_DrawCharacter_WhiteOnBlue()
    {
        using var renderer = new SkiaRenderer();
        var config = new ModernC64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        bool hasWhite = false;
        for (int py = 0; py < 16 && !hasWhite; py++)
        for (int px = 0; px < 8 && !hasWhite; px++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                hasWhite = true;
        }
        Assert.True(hasWhite, "'A' should produce white pixels on blue background");
    }

    /// <summary>
    /// Border area is filled with bright blue matching background.
    /// </summary>
    [Fact]
    public void ModernC64Theme_Border_BrightBlue()
    {
        using var renderer = new SkiaRenderer();
        var config = new ModernC64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var borderPixel = renderer.BackBuffer!.GetPixel(1, 1);
        Assert.Equal(new SKColor(0x00, 0x50, 0xA4), borderPixel);
    }

    /// <summary>
    /// GuiScreen reports correct 80×30 dimensions.
    /// </summary>
    [Fact]
    public void ModernC64Theme_GuiScreen_80x30()
    {
        using var renderer = new SkiaRenderer();
        var config = new ModernC64Theme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(30, rows);
    }

    /// <summary>
    /// Rendering "ZORK" at 80 columns produces visible white text.
    /// </summary>
    [Fact]
    public void ModernC64Theme_RenderZork_ProducesVisibleText()
    {
        using var renderer = new SkiaRenderer();
        var config = new ModernC64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        for (int i = 0; i < 4; i++)
            renderer.DrawCharacter(i, 0, "ZORK"[i], fg, bg, 0);

        int textPixels = 0;
        for (int col = 0; col < 4; col++)
        {
            int x = config.BorderWidth + col * 8;
            int y = config.BorderWidth;
            for (int py = 0; py < 16; py++)
            for (int px = 0; px < 8; px++)
            {
                if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                    textPixels++;
            }
        }
        Assert.True(textPixels > 0, "ZORK should produce visible white text");
    }

    #endregion

    #region Differences from C64 Classic

    /// <summary>
    /// Modern C64 has wider columns than C64 Classic (80 vs 40).
    /// </summary>
    [Fact]
    public void ModernC64Theme_WiderThanClassic()
    {
        var classic = new C64Theme().CreateConfig();
        var modern = new ModernC64Theme().CreateConfig();

        Assert.Equal(40, classic.Columns);
        Assert.Equal(80, modern.Columns);
    }

    /// <summary>
    /// Modern C64 uses standard chrome vs C64 Classic's borderless.
    /// </summary>
    [Fact]
    public void ModernC64Theme_StandardChrome_VsBorderless()
    {
        var classic = new C64Theme().CreateConfig();
        var modern = new ModernC64Theme().CreateConfig();

        Assert.Equal(ChromeMode.Borderless, classic.Chrome);
        Assert.Equal(ChromeMode.Standard, modern.Chrome);
    }

    /// <summary>
    /// Modern C64 background is brighter blue than C64 Classic.
    /// </summary>
    [Fact]
    public void ModernC64Theme_BrighterBlue_ThanClassic()
    {
        var classic = new C64Theme().CreateConfig();
        var modern = new ModernC64Theme().CreateConfig();

        var classicBg = classic.GetColor(classic.DefaultBackground);
        var modernBg = modern.GetColor(modern.DefaultBackground);

        // Modern blue (#0050A4) should have higher blue channel than
        // classic medium blue (#40318D)
        Assert.True(modernBg.Blue > classicBg.Blue,
            "Modern blue should be brighter than classic C64 blue");
    }

    #endregion
}
