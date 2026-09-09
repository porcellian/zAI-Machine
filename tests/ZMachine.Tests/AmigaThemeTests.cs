namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.8 — Amiga Theme: Workbench 1.x-inspired theme with
/// the ZSpec11 gamma-adjusted Amiga V6 colour set and Topaz 8×8 font.
/// </summary>
public class AmigaThemeTests
{
    #region ITheme Contract

    [Fact]
    public void AmigaTheme_ImplementsITheme()
    {
        ITheme theme = new AmigaTheme();
        Assert.Equal("Amiga Workbench", theme.Name);
    }

    [Fact]
    public void CreateConfig_ReturnsConfig()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("Amiga Workbench", config.Name);
    }

    #endregion

    #region Layout — 80×25 Topaz 8×8

    [Fact]
    public void Dimensions_80x25()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(80, config.Columns);
        Assert.Equal(25, config.Rows);
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(8, config.CharHeight);
    }

    [Fact]
    public void PixelDimensions_Correct()
    {
        var config = new AmigaTheme().CreateConfig();
        // 16*2 + 80*8 = 672
        Assert.Equal(672, config.PixelWidth);
        // 16*2 + 25*8 = 232
        Assert.Equal(232, config.PixelHeight);
    }

    [Fact]
    public void Font_IsAmigaTopaz()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("Amiga 8×8", config.Font!.Name);
    }

    [Fact]
    public void BorderWidth_Is16()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(16, config.BorderWidth);
    }

    #endregion

    #region ZSpec11 Amiga V6 Colour Set — Gamma-Adjusted

    [Fact]
    public void Palette_Has14Entries()
    {
        Assert.Equal(14, AmigaTheme.AmigaPalette.Length);
    }

    /// <summary>
    /// ZSpec11: colour 2 = black (true $0000).
    /// </summary>
    [Fact]
    public void Color2_Black_Spec0000()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0x00), config.GetColor(2));
    }

    /// <summary>
    /// ZSpec11: colour 3 = red (true $001D). R=29→0xEF, G=0, B=0.
    /// </summary>
    [Fact]
    public void Color3_Red_Spec001D()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xEF, 0x00, 0x00), config.GetColor(3));
    }

    /// <summary>
    /// ZSpec11: colour 4 = green (true $0340). G=26→0xD6.
    /// </summary>
    [Fact]
    public void Color4_Green_Spec0340()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0xD6, 0x00), config.GetColor(4));
    }

    /// <summary>
    /// ZSpec11: colour 5 = yellow (true $03BD). R=29→0xEF, G=29→0xEF.
    /// </summary>
    [Fact]
    public void Color5_Yellow_Spec03BD()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xEF, 0xEF, 0x00), config.GetColor(5));
    }

    /// <summary>
    /// ZSpec11: colour 6 = blue (true $59A0). G=13→0x6B, B=22→0xB5.
    /// </summary>
    [Fact]
    public void Color6_Blue_Spec59A0()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x6B, 0xB5), config.GetColor(6));
    }

    /// <summary>
    /// ZSpec11: colour 7 = magenta (true $7C1F). R=31→0xFF, B=31→0xFF.
    /// </summary>
    [Fact]
    public void Color7_Magenta_Spec7C1F()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0x00, 0xFF), config.GetColor(7));
    }

    /// <summary>
    /// ZSpec11: colour 8 = cyan (true $77A0). G=29→0xEF, B=29→0xEF.
    /// </summary>
    [Fact]
    public void Color8_Cyan_Spec77A0()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0xEF, 0xEF), config.GetColor(8));
    }

    /// <summary>
    /// ZSpec11: colour 9 = white (true $7FFF). All channels = 31→0xFF.
    /// </summary>
    [Fact]
    public void Color9_White_Spec7FFF()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
    }

    /// <summary>
    /// ZSpec11: colour 10 = light grey (true $5AD6). All=22→0xB5.
    /// </summary>
    [Fact]
    public void Color10_LightGrey_Spec5AD6()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xB5, 0xB5, 0xB5), config.GetColor(10));
    }

    /// <summary>
    /// ZSpec11: colour 11 = medium grey (true $4631). All=17→0x8C.
    /// </summary>
    [Fact]
    public void Color11_MediumGrey_Spec4631()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x8C, 0x8C, 0x8C), config.GetColor(11));
    }

    /// <summary>
    /// ZSpec11: colour 12 = dark grey (true $2D6B). All=11→0x5A.
    /// </summary>
    [Fact]
    public void Color12_DarkGrey_Spec2D6B()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x5A, 0x5A, 0x5A), config.GetColor(12));
    }

    /// <summary>
    /// All palette entries must be fully opaque.
    /// </summary>
    [Fact]
    public void AllColors_FullyOpaque()
    {
        foreach (var color in AmigaTheme.AmigaPalette)
        {
            Assert.Equal(0xFF, color.Alpha);
        }
    }

    /// <summary>
    /// The ZSpec11 grey values should be uniformly grey (R==G==B)
    /// for colours 10–12.
    /// </summary>
    [Fact]
    public void GreyColors_UniformChannels()
    {
        var config = new AmigaTheme().CreateConfig();
        for (int c = 10; c <= 12; c++)
        {
            var color = config.GetColor(c);
            Assert.Equal(color.Red, color.Green);
            Assert.Equal(color.Green, color.Blue);
        }
    }

    #endregion

    #region Workbench Accent Colours (13–15)

    [Fact]
    public void Color13_WorkbenchOrange()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0x88, 0x00), config.GetColor(13));
    }

    [Fact]
    public void Color14_WorkbenchBlue()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x55, 0xAA), config.GetColor(14));
    }

    [Fact]
    public void Color15_WorkbenchGrey()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xAA, 0xAA, 0xAA), config.GetColor(15));
    }

    #endregion

    #region Default Colors and Chrome

    [Fact]
    public void DefaultForeground_White()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(9, config.DefaultForeground);
    }

    [Fact]
    public void DefaultBackground_Black()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(2, config.DefaultBackground);
    }

    /// <summary>
    /// Status line colours (white on blue) available for the Amiga
    /// title-bar-style status display.
    /// </summary>
    [Fact]
    public void StatusLineColors_WhiteOnBlue_Available()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
        Assert.Equal(new SKColor(0x00, 0x6B, 0xB5), config.GetColor(6));
    }

    [Fact]
    public void Chrome_Standard()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(ChromeMode.Standard, config.Chrome);
    }

    [Fact]
    public void BorderColor_WorkbenchBlue()
    {
        var config = new AmigaTheme().CreateConfig();
        Assert.Equal(AmigaTheme.WorkbenchBlue, config.BorderColor);
    }

    [Fact]
    public void NoPostProcessing()
    {
        var config = new AmigaTheme().CreateConfig();
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
        var config = new AmigaTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(672, renderer.BackBuffer!.Width);
        Assert.Equal(232, renderer.BackBuffer.Height);
    }

    [Fact]
    public void DrawCharacter_UsesAmigaColors()
    {
        using var renderer = new SkiaRenderer();
        var config = new AmigaTheme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground); // white
        var bg = config.GetColor(config.DefaultBackground); // black
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        for (int py = 0; py < 8; py++)
        for (int px = 0; px < 8; px++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            Assert.True(pixel == fg || pixel == bg,
                $"Pixel ({px},{py}) should be white or black, was {pixel}");
        }
    }

    [Fact]
    public void GuiScreen_80x25()
    {
        using var renderer = new SkiaRenderer();
        var config = new AmigaTheme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(80, cols);
        Assert.Equal(25, rows);
    }

    #endregion

    #region ZSpec11 True Color Verification

    /// <summary>
    /// Verifies that each palette entry's 8-bit channels can be traced back
    /// to the ZSpec11 15-bit true colour values via (val &lt;&lt; 3) | (val &gt;&gt; 2).
    /// This conversion from 5-bit to 8-bit is the standard expansion method.
    /// </summary>
    [Theory]
    [InlineData(2,  0x0000, 0x00, 0x00, 0x00)] // black
    [InlineData(3,  0x001D, 0xEF, 0x00, 0x00)] // red
    [InlineData(4,  0x0340, 0x00, 0xD6, 0x00)] // green
    [InlineData(5,  0x03BD, 0xEF, 0xEF, 0x00)] // yellow
    [InlineData(6,  0x59A0, 0x00, 0x6B, 0xB5)] // blue
    [InlineData(7,  0x7C1F, 0xFF, 0x00, 0xFF)] // magenta
    [InlineData(8,  0x77A0, 0x00, 0xEF, 0xEF)] // cyan
    [InlineData(9,  0x7FFF, 0xFF, 0xFF, 0xFF)] // white
    [InlineData(10, 0x5AD6, 0xB5, 0xB5, 0xB5)] // light grey
    [InlineData(11, 0x4631, 0x8C, 0x8C, 0x8C)] // medium grey
    [InlineData(12, 0x2D6B, 0x5A, 0x5A, 0x5A)] // dark grey
    public void PaletteEntry_MatchesZSpec11TrueColor(
        int zColor, int trueColor, byte expectedR, byte expectedG, byte expectedB)
    {
        // ZSpec11 S8.3.1 — 15-bit: bits 14-10 blue, 9-5 green, 4-0 red
        int r5 = trueColor & 0x1F;
        int g5 = (trueColor >> 5) & 0x1F;
        int b5 = (trueColor >> 10) & 0x1F;

        byte r8 = (byte)((r5 << 3) | (r5 >> 2));
        byte g8 = (byte)((g5 << 3) | (g5 >> 2));
        byte b8 = (byte)((b5 << 3) | (b5 >> 2));

        Assert.Equal(expectedR, r8);
        Assert.Equal(expectedG, g8);
        Assert.Equal(expectedB, b8);

        var config = new AmigaTheme().CreateConfig();
        var actual = config.GetColor(zColor);
        Assert.Equal(new SKColor(expectedR, expectedG, expectedB), actual);
    }

    #endregion
}
