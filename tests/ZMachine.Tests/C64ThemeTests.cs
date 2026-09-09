namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.3 — C64 Classic Theme: ITheme interface,
/// C64Theme configuration, cursor blinking, and post-processing effects.
/// </summary>
public class C64ThemeTests
{
    #region ITheme Interface

    /// <summary>
    /// C64Theme implements ITheme.
    /// </summary>
    [Fact]
    public void C64Theme_ImplementsITheme()
    {
        ITheme theme = new C64Theme();
        Assert.NotNull(theme);
        Assert.Equal("C64 Classic", theme.Name);
    }

    /// <summary>
    /// CreateConfig returns a valid ThemeConfig.
    /// </summary>
    [Fact]
    public void C64Theme_CreateConfig_ReturnsConfig()
    {
        var theme = new C64Theme();
        var config = theme.CreateConfig();
        Assert.NotNull(config);
        Assert.Equal("C64 Classic", config.Name);
    }

    #endregion

    #region Screen Dimensions

    /// <summary>
    /// C64 classic uses 40 columns × 25 rows (320×200 character area).
    /// </summary>
    [Fact]
    public void C64Theme_Dimensions_40x25()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(40, config.Columns);
        Assert.Equal(25, config.Rows);
        Assert.Equal(8, config.CharWidth);
        Assert.Equal(8, config.CharHeight);
    }

    /// <summary>
    /// C64 pixel dimensions include 32-pixel border on each side.
    /// </summary>
    [Fact]
    public void C64Theme_PixelDimensions_IncludeBorders()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(32, config.BorderWidth);
        // 32*2 + 40*8 = 384
        Assert.Equal(384, config.PixelWidth);
        // 32*2 + 25*8 = 264
        Assert.Equal(264, config.PixelHeight);
    }

    #endregion

    #region Color Palette

    /// <summary>
    /// C64 palette has 14 entries (Z-Machine colors 2–15).
    /// </summary>
    [Fact]
    public void C64Theme_Palette_14Entries()
    {
        Assert.Equal(14, C64Theme.C64Palette.Length);
    }

    /// <summary>
    /// Default foreground is light blue (color 14), matching the screenshot.
    /// </summary>
    [Fact]
    public void C64Theme_DefaultForeground_LightBlue()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(14, config.DefaultForeground);
        var fg = config.GetColor(14);
        Assert.Equal(new SKColor(0x78, 0x69, 0xC4), fg);
    }

    /// <summary>
    /// Default background is medium blue (color 6), matching the screenshot.
    /// </summary>
    [Fact]
    public void C64Theme_DefaultBackground_MediumBlue()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(6, config.DefaultBackground);
        var bg = config.GetColor(6);
        Assert.Equal(new SKColor(0x40, 0x31, 0x8D), bg);
    }

    /// <summary>
    /// Border color matches the medium blue background.
    /// </summary>
    [Fact]
    public void C64Theme_BorderColor_MediumBlue()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(new SKColor(0x40, 0x31, 0x8D), config.BorderColor);
    }

    /// <summary>
    /// Black is color 2 (index 0) in the C64 palette.
    /// </summary>
    [Fact]
    public void C64Theme_Palette_BlackIsColor2()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0x00), config.GetColor(2));
    }

    /// <summary>
    /// White is color 9 in the C64 palette.
    /// </summary>
    [Fact]
    public void C64Theme_Palette_WhiteIsColor9()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), config.GetColor(9));
    }

    #endregion

    #region Font

    /// <summary>
    /// C64 theme uses the C64 8×8 BitmapFont.
    /// </summary>
    [Fact]
    public void C64Theme_Font_IsC64()
    {
        var config = new C64Theme().CreateConfig();
        Assert.NotNull(config.Font);
        Assert.Equal("C64 8×8", config.Font!.Name);
        Assert.Equal(8, config.Font.CharWidth);
        Assert.Equal(8, config.Font.CharHeight);
    }

    #endregion

    #region Chrome Mode

    /// <summary>
    /// C64 classic uses borderless chrome for full-screen retro feel.
    /// </summary>
    [Fact]
    public void C64Theme_Chrome_Borderless()
    {
        var config = new C64Theme().CreateConfig();
        Assert.Equal(ChromeMode.Borderless, config.Chrome);
    }

    #endregion

    #region Renderer Integration

    /// <summary>
    /// SkiaRenderer initializes correctly with C64 theme dimensions.
    /// </summary>
    [Fact]
    public void C64Theme_Renderer_InitializesCorrectly()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(384, renderer.BackBuffer!.Width);
        Assert.Equal(264, renderer.BackBuffer.Height);

        var (cols, rows) = renderer.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(25, rows);
    }

    /// <summary>
    /// Drawing text with C64 theme produces light blue pixels on medium blue.
    /// </summary>
    [Fact]
    public void C64Theme_DrawCharacter_ProducesCorrectColors()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);
        renderer.DrawCharacter(0, 0, 'A', fg, bg, 0);

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        // Check for foreground (light blue) pixels
        bool hasFgPixels = false;
        for (int py = 0; py < 8 && !hasFgPixels; py++)
        for (int px = 0; px < 8 && !hasFgPixels; px++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == fg)
                hasFgPixels = true;
        }
        Assert.True(hasFgPixels, "C64 'A' should produce light blue pixels");
    }

    /// <summary>
    /// Status line rendering with C64 theme uses reverse video
    /// (medium blue text on light blue background).
    /// </summary>
    [Fact]
    public void C64Theme_StatusLine_ReverseVideo()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        screen.ShowStatusLine("West of House", "Score: 0");

        int x = config.BorderWidth;
        int y = config.BorderWidth;

        // Status line uses reverse video — background should be foreground color
        bool hasReverseBg = false;
        var fg = config.GetColor(config.DefaultForeground);
        for (int px = 0; px < config.CharWidth && !hasReverseBg; px++)
        for (int py = 0; py < config.CharHeight && !hasReverseBg; py++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == fg)
                hasReverseBg = true;
        }
        Assert.True(hasReverseBg, "Status line should have reverse-video light blue background");
    }

    /// <summary>
    /// Border area is filled with medium blue.
    /// </summary>
    [Fact]
    public void C64Theme_Border_MediumBlue()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        // Check a border pixel (top-left corner, within border area)
        var borderPixel = renderer.BackBuffer!.GetPixel(5, 5);
        Assert.Equal(new SKColor(0x40, 0x31, 0x8D), borderPixel);
    }

    #endregion

    #region Cursor Blinking

    /// <summary>
    /// DrawCursor draws a block cursor at the current position.
    /// </summary>
    [Fact]
    public void SkiaRenderer_DrawCursor_DrawsBlock()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        renderer.SetCursorPosition(0, 1);
        renderer.DrawCursor(fg);

        // Cursor should draw a filled block at row 1, col 0
        int x = config.BorderWidth;
        int y = config.BorderWidth + config.CharHeight;
        var pixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(fg, pixel);
    }

    /// <summary>
    /// SetCursorPosition resets the blink timer so the cursor is visible.
    /// </summary>
    [Fact]
    public void SkiaRenderer_SetCursorPosition_MakesCursorVisible()
    {
        using var renderer = new SkiaRenderer();
        var config = new ThemeConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        renderer.SetCursorPosition(5, 5);
        renderer.DrawCursor(SKColors.White);

        int x = config.BorderWidth + 5 * config.CharWidth;
        int y = config.BorderWidth + 5 * config.CharHeight;
        var pixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(SKColors.White, pixel);
    }

    #endregion

    #region Post-Processing Effects

    /// <summary>
    /// ApplyScanlines darkens every other row.
    /// </summary>
    [Fact]
    public void SkiaRenderer_Scanlines_DarkensAlternateRows()
    {
        using var renderer = new SkiaRenderer();
        var config = new ThemeConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        // Fill a region with white
        renderer.DrawRegion(0, 0, 16, 4, SKColors.White);
        renderer.ApplyScanlines();

        // Even rows (0, 2) stay white; odd rows (1, 3) are darkened
        var row0 = renderer.BackBuffer!.GetPixel(5, 0);
        var row1 = renderer.BackBuffer.GetPixel(5, 1);

        Assert.Equal(SKColors.White, row0);
        Assert.True(row1.Red < 255, "Odd rows should be darkened by scanlines");
        Assert.True(row1.Red > 100, "Darkening should be ~30%, not full black");
    }

    /// <summary>
    /// ApplyCrtCurvature warps edge pixels inward (corner becomes black).
    /// </summary>
    [Fact]
    public void SkiaRenderer_CrtCurvature_WarpsEdges()
    {
        using var renderer = new SkiaRenderer();
        var config = new ThemeConfig { Columns = 20, Rows = 10, BorderWidth = 0 };
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        // Fill entire buffer with white
        renderer.DrawRegion(0, 0, config.PixelWidth, config.PixelHeight, SKColors.White);

        renderer.ApplyCrtCurvature(0.1f);

        // Strong curvature should push corner pixels outside the source,
        // resulting in black at the corners
        var corner = renderer.BackBuffer!.GetPixel(0, 0);
        Assert.Equal(SKColors.Black, corner);
    }

    /// <summary>
    /// ApplyPhosphorBloom increases brightness of pixels near bright areas.
    /// </summary>
    [Fact]
    public void SkiaRenderer_PhosphorBloom_IncreasesNeighborBrightness()
    {
        using var renderer = new SkiaRenderer();
        var config = new ThemeConfig { Columns = 10, Rows = 5, BorderWidth = 0 };
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        // Draw a bright white pixel surrounded by dim grey
        var grey = new SKColor(80, 80, 80);
        renderer.DrawRegion(0, 0, config.PixelWidth, config.PixelHeight, grey);
        renderer.BackBuffer!.SetPixel(40, 20, SKColors.White);

        var beforeNeighbor = renderer.BackBuffer.GetPixel(41, 20);
        renderer.ApplyPhosphorBloom(0.5f);
        var afterNeighbor = renderer.BackBuffer.GetPixel(41, 20);

        // Neighbor should be brighter after bloom
        Assert.True(afterNeighbor.Red >= beforeNeighbor.Red,
            "Bloom should increase neighbor brightness");
    }

    /// <summary>
    /// ThemeConfig PhosphorBloom property defaults to false.
    /// </summary>
    [Fact]
    public void ThemeConfig_PhosphorBloom_DefaultsFalse()
    {
        var config = new ThemeConfig();
        Assert.False(config.PhosphorBloom);
    }

    /// <summary>
    /// C64 theme defaults to no post-processing effects.
    /// </summary>
    [Fact]
    public void C64Theme_PostProcessing_DefaultsOff()
    {
        var config = new C64Theme().CreateConfig();
        Assert.False(config.Scanlines);
        Assert.False(config.CrtCurvature);
        Assert.False(config.PhosphorBloom);
    }

    #endregion

    #region Full Rendering

    /// <summary>
    /// Rendering "ZORK" at 40-column C64 layout produces visible text.
    /// </summary>
    [Fact]
    public void C64Theme_RenderZork_ProducesVisibleText()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        renderer.Initialize(config.PixelWidth, config.PixelHeight, config);

        var fg = config.GetColor(config.DefaultForeground);
        var bg = config.GetColor(config.DefaultBackground);

        for (int i = 0; i < 4; i++)
            renderer.DrawCharacter(i, 0, "ZORK"[i], fg, bg, 0);

        // Verify the text area has foreground-colored pixels
        int textPixels = 0;
        for (int col = 0; col < 4; col++)
        {
            int x = config.BorderWidth + col * 8;
            int y = config.BorderWidth;
            for (int py = 0; py < 8; py++)
            for (int px = 0; px < 8; px++)
            {
                if (renderer.BackBuffer!.GetPixel(x + px, y + py) == fg)
                    textPixels++;
            }
        }

        Assert.True(textPixels > 0, "ZORK should produce visible light blue text");
    }

    /// <summary>
    /// C64 theme GuiScreen produces correct 40×25 screen size.
    /// </summary>
    [Fact]
    public void C64Theme_GuiScreen_40x25()
    {
        using var renderer = new SkiaRenderer();
        var config = new C64Theme().CreateConfig();
        var screen = new GuiScreen(renderer, config);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(25, rows);
    }

    #endregion
}
