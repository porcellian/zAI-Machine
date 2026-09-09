namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.7 — DOS CGA Color Theme: full CGA 16-color palette
/// with 80×25 grid, EGA 8×14 font, and standard DOS text mode defaults.
/// </summary>
public class DosColorThemeTests
{
    #region ITheme Contract

    [Fact]
    public void DosColorTheme_ImplementsITheme()
    {
        ITheme theme = new DosColorTheme();
        Assert.Equal("DOS Color", theme.Name);
    }

    [Fact]
    public void CreateConfig_ReturnsConfig()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("DOS Color", config.Name);
    }

    #endregion

    #region Layout — 80×25 EGA 8×14

    [Fact]
    public void Dimensions_80x25()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(80, config.Columns);
        Assert.Equal(25, config.Rows);
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(14, config.CharHeight);
    }

    [Fact]
    public void PixelDimensions_Correct()
    {
        var config = new DosColorTheme().CreateConfig();
        // 8*2 + 80*8 = 656
        Assert.Equal(656, config.PixelWidth);
        // 8*2 + 25*14 = 366
        Assert.Equal(366, config.PixelHeight);
    }

    [Fact]
    public void Font_IsEGA()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("EGA 8×14", config.Font!.Name);
    }

    [Fact]
    public void BorderWidth_Is8()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(8, config.BorderWidth);
    }

    #endregion

    #region CGA 16-Color Palette

    [Fact]
    public void Palette_Has14Entries()
    {
        Assert.Equal(14, DosColorTheme.CgaPalette.Length);
    }

    [Fact]
    public void Color2_Black()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0x00), config.GetColor(2));
    }

    [Fact]
    public void Color3_Red()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xAA, 0x00, 0x00), config.GetColor(3));
    }

    [Fact]
    public void Color4_Green()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0xAA, 0x00), config.GetColor(4));
    }

    [Fact]
    public void Color5_Yellow()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0x55), config.GetColor(5));
    }

    [Fact]
    public void Color6_Blue()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0xAA), config.GetColor(6));
    }

    [Fact]
    public void Color7_Magenta()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xAA, 0x00, 0xAA), config.GetColor(7));
    }

    [Fact]
    public void Color8_Cyan()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0xAA, 0xAA), config.GetColor(8));
    }

    [Fact]
    public void Color9_White()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
    }

    [Fact]
    public void Color10_LightGrey()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xAA, 0xAA, 0xAA), config.GetColor(10));
    }

    [Fact]
    public void Color11_DarkGrey()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x55, 0x55, 0x55), config.GetColor(11));
    }

    [Fact]
    public void Color12_DarkGrey()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x55, 0x55, 0x55), config.GetColor(12));
    }

    [Fact]
    public void Color13_Brown()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xAA, 0x55, 0x00), config.GetColor(13));
    }

    [Fact]
    public void Color14_LightMagenta()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0x55, 0xFF), config.GetColor(14));
    }

    [Fact]
    public void Color15_LightCyan()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0x55, 0xFF, 0xFF), config.GetColor(15));
    }

    /// <summary>
    /// Every palette entry must use actual CGA hardware colors.
    /// CGA values use only 0x00, 0x55, 0xAA, 0xFF per channel.
    /// </summary>
    [Fact]
    public void AllColors_AreCgaValues()
    {
        byte[] cgaComponents = [0x00, 0x55, 0xAA, 0xFF];
        foreach (var color in DosColorTheme.CgaPalette)
        {
            Assert.Contains(color.Red, cgaComponents);
            Assert.Contains(color.Green, cgaComponents);
            Assert.Contains(color.Blue, cgaComponents);
        }
    }

    /// <summary>
    /// All 14 palette entries must be fully opaque.
    /// </summary>
    [Fact]
    public void AllColors_FullyOpaque()
    {
        foreach (var color in DosColorTheme.CgaPalette)
        {
            Assert.Equal(0xFF, color.Alpha);
        }
    }

    #endregion

    #region Default Colors — DOS Text Mode

    /// <summary>
    /// Default DOS text mode: light grey (color 10) on black (color 2).
    /// </summary>
    [Fact]
    public void DefaultForeground_LightGrey()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(10, config.DefaultForeground);
    }

    [Fact]
    public void DefaultBackground_Black()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(2, config.DefaultBackground);
    }

    /// <summary>
    /// Status line uses white on blue — colors 9 and 6 must exist
    /// with correct CGA values for the interpreter to use.
    /// </summary>
    [Fact]
    public void StatusLineColors_WhiteOnBlue_Available()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
        Assert.Equal(new SKColor(0x00, 0x00, 0xAA), config.GetColor(6));
    }

    #endregion

    #region Chrome and Effects

    [Fact]
    public void Chrome_Borderless()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(ChromeMode.Borderless, config.Chrome);
    }

    [Fact]
    public void BorderColor_Black()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.Equal(SKColors.Black, config.BorderColor);
    }

    [Fact]
    public void NoPostProcessing()
    {
        var config = new DosColorTheme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    #endregion

    #region Renderer Integration

    [Fact]
    public void Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosColorTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(656, renderer.BackBuffer!.Width);
        Assert.Equal(366, renderer.BackBuffer.Height);
    }

    /// <summary>
    /// Drawing a character produces pixels matching the CGA foreground
    /// and background colors, not arbitrary values.
    /// </summary>
    [Fact]
    public void DrawCharacter_UsesCgaColors()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosColorTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground); // light grey
        var bg = config.GetColor(config.DefaultBackground); // black
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        for (int py = 0; py < 14; py++)
        for (int px = 0; px < 8; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == fg || pixel == bg,
                $"Pixel ({px},{py}) should be light grey or black, was {pixel}");
        }
    }

    /// <summary>
    /// Drawing with red foreground on blue background produces only
    /// those two CGA colors.
    /// </summary>
    [Fact]
    public void DrawCharacter_RedOnBlue()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosColorTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var red = config.GetColor(3);
        var blue = config.GetColor(6);
        renderer.DrawCharacter(0, 0, 'Z', red, blue, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        for (int py = 0; py < 14; py++)
        for (int px = 0; px < 8; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == red || pixel == blue,
                $"Pixel ({px},{py}) should be CGA red or blue, was {pixel}");
        }
    }

    [Fact]
    public void GuiScreen_80x25()
    {
        using var renderer = new SkiaRenderer();
        var config = new DosColorTheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(25, rows);
    }

    #endregion

    #region Comparison with DOS Monochrome

    /// <summary>
    /// DOS Color and DOS Green share the same grid dimensions but differ
    /// in palette — the color theme has distinct per-color entries while
    /// monochrome maps everything to one phosphor.
    /// </summary>
    [Fact]
    public void VsMonochrome_SameDimensions()
    {
        var color = new DosColorTheme().CreateConfig();
        var mono = new DosGreenTheme().CreateConfig();

        Assert.Equal(color.Columns, mono.Columns);
        Assert.Equal(color.Rows, mono.Rows);
        Assert.Equal(color.CharWidth, mono.CharWidth);
        Assert.Equal(color.CharHeight, mono.CharHeight);
    }

    [Fact]
    public void VsMonochrome_DifferentPalettes()
    {
        var color = new DosColorTheme().CreateConfig();
        var mono = new DosGreenTheme().CreateConfig();

        // Red should differ: CGA red vs green phosphor
        Assert.NotEqual(color.GetColor(3), mono.GetColor(3));
        // Blue should differ: CGA blue vs green phosphor
        Assert.NotEqual(color.GetColor(6), mono.GetColor(6));
    }

    #endregion
}
