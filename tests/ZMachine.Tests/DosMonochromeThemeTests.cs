namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.6 — DOS Monochrome Themes: DosGreenTheme (P1 green
/// phosphor) and DosAmberTheme (P3 amber phosphor), both sharing the
/// DosMonochromeTheme base with 80×25 grid and EGA 8×14 font.
/// </summary>
public class DosMonochromeThemeTests
{
    #region DosGreenTheme — ITheme

    [Fact]
    public void DosGreenTheme_ImplementsITheme()
    {
        ITheme theme = new DosGreenTheme();
        Assert.Equal("DOS Green", theme.Name);
    }

    [Fact]
    public void DosGreenTheme_CreateConfig_ReturnsConfig()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("DOS Green", config.Name);
    }

    #endregion

    #region DosAmberTheme — ITheme

    [Fact]
    public void DosAmberTheme_ImplementsITheme()
    {
        ITheme theme = new DosAmberTheme();
        Assert.Equal("DOS Amber", theme.Name);
    }

    [Fact]
    public void DosAmberTheme_CreateConfig_ReturnsConfig()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("DOS Amber", config.Name);
    }

    #endregion

    #region Shared Layout — 80×25 EGA 8×14

    [Fact]
    public void DosGreen_Dimensions_80x25()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(80, config.Columns);
        Assert.Equal(25, config.Rows);
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(14, config.CharHeight);
    }

    [Fact]
    public void DosAmber_Dimensions_80x25()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(80, config.Columns);
        Assert.Equal(25, config.Rows);
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(14, config.CharHeight);
    }

    [Fact]
    public void DosGreen_PixelDimensions_Correct()
    {
        var config = new DosGreenTheme().CreateConfig();
        // 8*2 + 80*8 = 656
        Assert.Equal(656, config.PixelWidth);
        // 8*2 + 25*14 = 366
        Assert.Equal(366, config.PixelHeight);
    }

    [Fact]
    public void DosGreen_Font_IsEGA()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("EGA 8×14", config.Font!.Name);
    }

    [Fact]
    public void DosAmber_Font_IsEGA()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("EGA 8×14", config.Font!.Name);
    }

    #endregion

    #region Green Phosphor Palette

    [Fact]
    public void DosGreen_Palette_14Entries()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(14, config.ColorPalette.Length);
    }

    /// <summary>
    /// All colors except black map to green phosphor.
    /// </summary>
    [Fact]
    public void DosGreen_AllColors_MapToGreen()
    {
        var config = new DosGreenTheme().CreateConfig();
        var green = new SKColor(0x33, 0xFF, 0x33);
        var bright = new SKColor(0x66, 0xFF, 0x66);

        for (int color = 3; color <= 15; color++)
        {
            var c = config.GetColor(color);
            Assert.True(c == green || c == bright,
                $"Color {color} should be green or bright green, was {c}");
        }
    }

    [Fact]
    public void DosGreen_Black_MapsToBackground()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(new SKColor(0x0A, 0x1A, 0x0A), config.GetColor(2));
    }

    /// <summary>
    /// Default foreground (white=9) maps to bright green (intensified).
    /// </summary>
    [Fact]
    public void DosGreen_DefaultForeground_BrightGreen()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(9, config.DefaultForeground);
        Assert.Equal(new SKColor(0x66, 0xFF, 0x66), config.GetColor(9));
    }

    [Fact]
    public void DosGreen_BorderColor_MatchesBackground()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(new SKColor(0x0A, 0x1A, 0x0A), config.BorderColor);
    }

    #endregion

    #region Amber Phosphor Palette

    [Fact]
    public void DosAmber_Palette_14Entries()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(14, config.ColorPalette.Length);
    }

    /// <summary>
    /// All colors except black map to amber phosphor.
    /// </summary>
    [Fact]
    public void DosAmber_AllColors_MapToAmber()
    {
        var config = new DosAmberTheme().CreateConfig();
        var amber = new SKColor(0xFF, 0xB0, 0x00);
        var bright = new SKColor(0xFF, 0xD0, 0x60);

        for (int color = 3; color <= 15; color++)
        {
            var c = config.GetColor(color);
            Assert.True(c == amber || c == bright,
                $"Color {color} should be amber or bright amber, was {c}");
        }
    }

    [Fact]
    public void DosAmber_Black_MapsToBackground()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(new SKColor(0x1A, 0x0F, 0x00), config.GetColor(2));
    }

    /// <summary>
    /// Default foreground (white=9) maps to bright amber (intensified).
    /// </summary>
    [Fact]
    public void DosAmber_DefaultForeground_BrightAmber()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(9, config.DefaultForeground);
        Assert.Equal(new SKColor(0xFF, 0xD0, 0x60), config.GetColor(9));
    }

    [Fact]
    public void DosAmber_BorderColor_MatchesBackground()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(new SKColor(0x1A, 0x0F, 0x00), config.BorderColor);
    }

    #endregion

    #region Chrome and Effects

    [Fact]
    public void DosGreen_Chrome_Borderless()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.Equal(ChromeMode.Borderless, config.Chrome);
    }

    [Fact]
    public void DosAmber_Chrome_Borderless()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.Equal(ChromeMode.Borderless, config.Chrome);
    }

    [Fact]
    public void DosGreen_NoPostProcessing_ByDefault()
    {
        var config = new DosGreenTheme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    [Fact]
    public void DosAmber_NoPostProcessing_ByDefault()
    {
        var config = new DosAmberTheme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    #endregion

    #region Renderer Integration — Green

    [Fact]
    public void DosGreen_Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosGreenTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(656, renderer.BackBuffer!.Width);
        Assert.Equal(366, renderer.BackBuffer.Height);
    }

    /// <summary>
    /// Drawing text with green theme produces only green/dark-green pixels.
    /// </summary>
    [Fact]
    public void DosGreen_DrawCharacter_OnlyGreenPixels()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosGreenTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        var bright = new SKColor(0x66, 0xFF, 0x66);
        var dark = new SKColor(0x0A, 0x1A, 0x0A);

        for (int py = 0; py < 14; py++)
        for (int px = 0; px < 8; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == bright || pixel == dark,
                $"Pixel ({px},{py}) should be bright green or dark, was {pixel}");
        }
    }

    [Fact]
    public void DosGreen_RenderZork_ProducesGreenText()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosGreenTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        for (int i = 0; i < 4; i++)
            renderer.DrawCharacter(i, 0, "ZORK"[i], fg, bg, 0);

        var bright = new SKColor(0x66, 0xFF, 0x66);
        int greenPixels = 0;
        for (int col = 0; col < 4; col++)
        {
            int x = config.BorderWidth + col * 8;
            int y = config.BorderWidth;
            for (int py = 0; py < 14; py++)
            for (int px = 0; px < 8; px++)
            {
                if (renderer.BackBuffer!.GetPixel(x + px, y + py) == bright)
                    greenPixels++;
            }
        }
        Assert.True(greenPixels > 0, "ZORK should produce bright green text");
    }

    [Fact]
    public void DosGreen_GuiScreen_80x25()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosGreenTheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(25, rows);
    }

    #endregion

    #region Renderer Integration — Amber

    [Fact]
    public void DosAmber_Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosAmberTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(656, renderer.BackBuffer!.Width);
        Assert.Equal(366, renderer.BackBuffer.Height);
    }

    /// <summary>
    /// Drawing text with amber theme produces only amber/dark pixels.
    /// </summary>
    [Fact]
    public void DosAmber_DrawCharacter_OnlyAmberPixels()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosAmberTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;
        var bright = new SKColor(0xFF, 0xD0, 0x60);
        var dark = new SKColor(0x1A, 0x0F, 0x00);

        for (int py = 0; py < 14; py++)
        for (int px = 0; px < 8; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == bright || pixel == dark,
                $"Pixel ({px},{py}) should be bright amber or dark, was {pixel}");
        }
    }

    [Fact]
    public void DosAmber_RenderZork_ProducesAmberText()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosAmberTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        for (int i = 0; i < 4; i++)
            renderer.DrawCharacter(i, 0, "ZORK"[i], fg, bg, 0);

        var bright = new SKColor(0xFF, 0xD0, 0x60);
        int amberPixels = 0;
        for (int col = 0; col < 4; col++)
        {
            int x = config.BorderWidth + col * 8;
            int y = config.BorderWidth;
            for (int py = 0; py < 14; py++)
            for (int px = 0; px < 8; px++)
            {
                if (renderer.BackBuffer!.GetPixel(x + px, y + py) == bright)
                    amberPixels++;
            }
        }
        Assert.True(amberPixels > 0, "ZORK should produce bright amber text");
    }

    [Fact]
    public void DosAmber_GuiScreen_80x25()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosAmberTheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(25, rows);
    }

    #endregion

    #region Shared Base Class Behavior

    /// <summary>
    /// Both themes produce configs with the same dimensions and font.
    /// </summary>
    [Fact]
    public void BothThemes_SameDimensionsAndFont()
    {
        var green = new DosGreenTheme().CreateConfig();
        var amber = new DosAmberTheme().CreateConfig();

        Assert.Equal(green.Columns, amber.Columns);
        Assert.Equal(green.Rows, amber.Rows);
        Assert.Equal(green.CharWidth, amber.CharWidth);
        Assert.Equal(green.CharHeight, amber.CharHeight);
        Assert.Equal(green.Font!.Name, amber.Font!.Name);
    }

    /// <summary>
    /// Themes differ only in color palette.
    /// </summary>
    [Fact]
    public void BothThemes_DifferentColors()
    {
        var green = new DosGreenTheme().CreateConfig();
        var amber = new DosAmberTheme().CreateConfig();

        // Foreground colors should differ
        Assert.NotEqual(green.GetColor(9), amber.GetColor(9));
        // Background colors should differ
        Assert.NotEqual(green.GetColor(2), amber.GetColor(2));
    }

    #endregion
}
