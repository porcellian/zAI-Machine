namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.5 — Apple II Theme: monochrome P1 phosphor display,
/// 40×24 grid, 7×8 font, all colors mapped to green.
/// </summary>
public class AppleIIThemeTests
{
    #region ITheme Interface

    /// <summary>
    /// AppleIITheme implements ITheme.
    /// </summary>
    [Fact]
    public void AppleIITheme_ImplementsITheme()
    {
        ITheme theme = new AppleIITheme();
        Assert.Equal("Apple II", theme.Name);
    }

    /// <summary>
    /// CreateConfig returns a valid ThemeConfig.
    /// </summary>
    [Fact]
    public void AppleIITheme_CreateConfig_ReturnsConfig()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("Apple II", config.Name);
    }

    #endregion

    #region Screen Dimensions

    /// <summary>
    /// Apple II uses 40 columns × 24 rows (280×192 character area).
    /// </summary>
    [Fact]
    public void AppleIITheme_Dimensions_40x24()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(40, config.Columns);
        Assert.Equal(24, config.Rows);
    }

    /// <summary>
    /// Apple II character cells are 7×8 pixels.
    /// </summary>
    [Fact]
    public void AppleIITheme_CharCell_7x8()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(7, config.CharWidth);
        Assert.Equal(8, config.CharHeight);
    }

    /// <summary>
    /// Pixel dimensions include 24-pixel border.
    /// </summary>
    [Fact]
    public void AppleIITheme_PixelDimensions_IncludeBorders()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(24, config.BorderWidth);
        // 24*2 + 40*7 = 328
        Assert.Equal(328, config.PixelWidth);
        // 24*2 + 24*8 = 240
        Assert.Equal(240, config.PixelHeight);
    }

    #endregion

    #region Monochrome Palette

    /// <summary>
    /// All Z-Machine colors except black map to phosphor green.
    /// </summary>
    [Fact]
    public void AppleIITheme_AllColors_MapToGreen()
    {
        var config = new AppleIITheme().CreateConfig();
        var green = AppleIITheme.PhosphorGreen;

        for (int color = 3; color <= 15; color++)
            Assert.Equal(green, config.GetColor(color));
    }

    /// <summary>
    /// Color 2 (black) maps to the phosphor background for correct
    /// reverse-video rendering.
    /// </summary>
    [Fact]
    public void AppleIITheme_Black_MapsToBackground()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(AppleIITheme.PhosphorBackground, config.GetColor(2));
    }

    /// <summary>
    /// Default foreground resolves to phosphor green.
    /// </summary>
    [Fact]
    public void AppleIITheme_DefaultForeground_IsGreen()
    {
        var config = new AppleIITheme().CreateConfig();
        var fg = config.GetColor(config.DefaultForeground);
        Assert.Equal(AppleIITheme.PhosphorGreen, fg);
    }

    /// <summary>
    /// Default background resolves to phosphor dark green.
    /// </summary>
    [Fact]
    public void AppleIITheme_DefaultBackground_IsDarkGreen()
    {
        var config = new AppleIITheme().CreateConfig();
        var bg = config.GetColor(config.DefaultBackground);
        Assert.Equal(AppleIITheme.PhosphorBackground, bg);
    }

    /// <summary>
    /// Border color matches the phosphor background.
    /// </summary>
    [Fact]
    public void AppleIITheme_BorderColor_MatchesBackground()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(AppleIITheme.PhosphorBackground, config.BorderColor);
    }

    /// <summary>
    /// Palette has exactly 14 entries.
    /// </summary>
    [Fact]
    public void AppleIITheme_Palette_14Entries()
    {
        Assert.Equal(14, AppleIITheme.MonochromePalette.Length);
    }

    #endregion

    #region Font

    /// <summary>
    /// Apple II theme uses the Apple II 7×8 BitmapFont.
    /// </summary>
    [Fact]
    public void AppleIITheme_Font_IsAppleII()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("Apple II 7×8", config.Font!.Name);
        Assert.Equal(7, config.Font.CharWidth);
        Assert.Equal(8, config.Font.CharHeight);
    }

    #endregion

    #region Chrome and Effects

    /// <summary>
    /// Borderless chrome for full-screen retro feel.
    /// </summary>
    [Fact]
    public void AppleIITheme_Chrome_Borderless()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.Equal(ChromeMode.Borderless, config.Chrome);
    }

    /// <summary>
    /// No CRT effects by default.
    /// </summary>
    [Fact]
    public void AppleIITheme_NoPostProcessing_ByDefault()
    {
        var config = new AppleIITheme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    #endregion

    #region Renderer Integration

    /// <summary>
    /// SkiaRenderer initializes correctly with Apple II dimensions.
    /// </summary>
    [Fact]
    public void AppleIITheme_Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(328, renderer.BackBuffer!.Width);
        Assert.Equal(240, renderer.BackBuffer.Height);

        var (cols, rows) = renderer.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(24, rows);
    }

    /// <summary>
    /// Drawing text produces only green phosphor pixels (no other colors).
    /// </summary>
    [Fact]
    public void AppleIITheme_DrawCharacter_OnlyGreenPixels()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        var green = AppleIITheme.PhosphorGreen;
        var darkGreen = AppleIITheme.PhosphorBackground;

        for (int py = 0; py < 8; py++)
        for (int px = 0; px < 7; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == green || pixel == darkGreen,
                $"Pixel at ({px},{py}) should be green or dark green, was {pixel}");
        }
    }

    /// <summary>
    /// All Z-Machine colors render as green on this monochrome display.
    /// </summary>
    [Fact]
    public void AppleIITheme_AllColors_RenderAsGreen()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var green = AppleIITheme.PhosphorGreen;
        var darkGreen = AppleIITheme.PhosphorBackground;

        // Draw 'A' with "red" fg — should still be green
        var redColor = config.GetColor(3);
        renderer.DrawCharacter(0, 0, 'A', redColor, darkGreen, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        bool hasFgPixels = false;
        for (int py = 0; py < 8; py++)
        for (int px = 0; px < 7; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            if (pixel == green) hasFgPixels = true;
            Assert.True(pixel == green || pixel == darkGreen,
                $"'Red' text should render as green on Apple II");
        }
        Assert.True(hasFgPixels, "'A' should produce green pixels");
    }

    /// <summary>
    /// Reverse video swaps phosphor green and dark green.
    /// </summary>
    [Fact]
    public void AppleIITheme_ReverseVideo_SwapsGreenAndDark()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        // Draw with reverse video (style=1)
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 1);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        // With reverse, background fill is phosphor green
        var bgPixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(AppleIITheme.PhosphorGreen, bgPixel);
    }

    /// <summary>
    /// Status line renders in reverse video (green background, dark text).
    /// </summary>
    [Fact]
    public void AppleIITheme_StatusLine_ReverseGreen()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        screen.ShowStatusLine("West of House", "Score: 0");

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        // Status line background should be phosphor green (reverse)
        bool hasGreenBg = false;
        for (int px = 0; px < config.CharWidth && !hasGreenBg; px++)
        for (int py = 0; py < config.CharHeight && !hasGreenBg; py++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == AppleIITheme.PhosphorGreen)
                hasGreenBg = true;
        }
        Assert.True(hasGreenBg, "Status line should have green reverse-video background");
    }

    /// <summary>
    /// Border area is filled with phosphor dark green.
    /// </summary>
    [Fact]
    public void AppleIITheme_Border_DarkGreen()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var borderPixel = renderer.BackBuffer!.GetPixel(5, 5);
        Assert.Equal(AppleIITheme.PhosphorBackground, borderPixel);
    }

    /// <summary>
    /// GuiScreen reports correct 40×24 dimensions.
    /// </summary>
    [Fact]
    public void AppleIITheme_GuiScreen_40x24()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(24, rows);
    }

    /// <summary>
    /// Rendering "ZORK" produces visible green text.
    /// </summary>
    [Fact]
    public void AppleIITheme_RenderZork_ProducesGreenText()
    {
        using var renderer = new SkiaRenderer();
        var config = new AppleIITheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        for (int i = 0; i < 4; i++)
            renderer.DrawCharacter(i, 0, "ZORK"[i], fg, bg, 0);

        int greenPixels = 0;
        for (int col = 0; col < 4; col++)
        {
            int x = config.BorderWidth + col * 7;
            int y = config.BorderWidth;
            for (int py = 0; py < 8; py++)
            for (int px = 0; px < 7; px++)
            {
                if (renderer.BackBuffer!.GetPixel(x + px, y + py) == AppleIITheme.PhosphorGreen)
                    greenPixels++;
            }
        }
        Assert.True(greenPixels > 0, "ZORK should produce visible green text");
    }

    #endregion
}
