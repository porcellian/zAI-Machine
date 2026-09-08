namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.1 — Avalonia UI rendering abstraction layer:
/// ThemeConfig, BuiltInFont, SkiaRenderer, and GuiScreen.
/// </summary>
public class GuiRenderingTests
{
    #region ThemeConfig

    /// <summary>
    /// Default ThemeConfig has standard Z-Machine screen dimensions.
    /// </summary>
    [Fact]
    public void ThemeConfig_Defaults_80x25()
    {
        var theme = new ThemeConfig();
        Assert.Equal(80, theme.Columns);
        Assert.Equal(25, theme.Rows);
        Assert.Equal(8, theme.CharWidth);
        Assert.Equal(16, theme.CharHeight);
    }

    /// <summary>
    /// PixelWidth/PixelHeight includes borders.
    /// </summary>
    [Fact]
    public void ThemeConfig_PixelDimensions_IncludeBorders()
    {
        var theme = new ThemeConfig { Columns = 40, Rows = 25, CharWidth = 8, CharHeight = 16, BorderWidth = 16 };
        Assert.Equal(16 * 2 + 40 * 8, theme.PixelWidth);
        Assert.Equal(16 * 2 + 25 * 16, theme.PixelHeight);
    }

    /// <summary>
    /// GetColor maps Z-Machine color numbers 2–15 to palette entries.
    /// ZSpec S8.3.1 — True colour table.
    /// </summary>
    [Fact]
    public void ThemeConfig_GetColor_MapsZColors()
    {
        var theme = new ThemeConfig();
        Assert.Equal(new SKColor(0x00, 0x00, 0x00), theme.GetColor(2));  // Black
        Assert.Equal(new SKColor(0xFF, 0xFF, 0xFF), theme.GetColor(9));  // White
        Assert.Equal(new SKColor(0xEF, 0x08, 0x08), theme.GetColor(3));  // Red
    }

    /// <summary>
    /// GetColor returns white for out-of-range color numbers.
    /// </summary>
    [Fact]
    public void ThemeConfig_GetColor_OutOfRange_ReturnsWhite()
    {
        var theme = new ThemeConfig();
        Assert.Equal(SKColors.White, theme.GetColor(0));
        Assert.Equal(SKColors.White, theme.GetColor(1));
        Assert.Equal(SKColors.White, theme.GetColor(99));
    }

    /// <summary>
    /// Default palette has exactly 14 entries (colors 2–15).
    /// </summary>
    [Fact]
    public void ThemeConfig_DefaultPalette_14Entries()
    {
        Assert.Equal(14, ThemeConfig.DefaultPalette.Length);
    }

    #endregion

    #region BuiltInFont

    /// <summary>
    /// Built-in font data has the correct size for 95 glyphs × 16 rows.
    /// </summary>
    [Fact]
    public void BuiltInFont_DataSize_Correct()
    {
        Assert.Equal(95 * 16, BuiltInFont.Data.Length);
    }

    /// <summary>
    /// Space character (first glyph) is all zeros.
    /// </summary>
    [Fact]
    public void BuiltInFont_Space_AllZeros()
    {
        for (int i = 0; i < 16; i++)
            Assert.Equal(0, BuiltInFont.Data[i]);
    }

    /// <summary>
    /// 'A' glyph has non-zero rows (it's not blank).
    /// </summary>
    [Fact]
    public void BuiltInFont_LetterA_HasPixels()
    {
        int offset = ('A' - 32) * 16;
        bool hasPixels = false;
        for (int i = 0; i < 16; i++)
        {
            if (BuiltInFont.Data[offset + i] != 0)
                hasPixels = true;
        }
        Assert.True(hasPixels, "'A' glyph should have non-zero pixel data");
    }

    /// <summary>
    /// All printable ASCII characters have glyph data defined.
    /// </summary>
    [Fact]
    public void BuiltInFont_AllPrintableAscii_Covered()
    {
        Assert.Equal(32, BuiltInFont.FirstChar);
        Assert.Equal(95, BuiltInFont.GlyphCount);
        Assert.Equal(8, BuiltInFont.CharWidth);
        Assert.Equal(16, BuiltInFont.CharHeight);
    }

    #endregion

    #region SkiaRenderer

    /// <summary>
    /// Initialize creates a back buffer of the correct size.
    /// </summary>
    [Fact]
    public void SkiaRenderer_Initialize_CreatesBackBuffer()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        Assert.NotNull(renderer.BackBuffer);
        Assert.Equal(theme.PixelWidth, renderer.BackBuffer!.Width);
        Assert.Equal(theme.PixelHeight, renderer.BackBuffer.Height);
    }

    /// <summary>
    /// GetScreenSize returns the theme's column/row dimensions.
    /// </summary>
    [Fact]
    public void SkiaRenderer_GetScreenSize_MatchesTheme()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig { Columns = 40, Rows = 20 };
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        var (cols, rows) = renderer.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(20, rows);
    }

    /// <summary>
    /// DrawCharacter modifies pixels in the back buffer.
    /// </summary>
    [Fact]
    public void SkiaRenderer_DrawCharacter_ModifiesPixels()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        // Record the background pixel before drawing
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        var before = renderer.BackBuffer!.GetPixel(x, y);

        // Draw 'A' in white on black
        renderer.DrawCharacter(0, 0, 'A', SKColors.White, SKColors.Black, 0);

        // At least one pixel in the character cell should now differ
        bool anyChanged = false;
        for (int py = 0; py < theme.CharHeight && !anyChanged; py++)
        for (int px = 0; px < theme.CharWidth && !anyChanged; px++)
        {
            var pixel = renderer.BackBuffer.GetPixel(x + px, y + py);
            if (pixel == SKColors.White)
                anyChanged = true;
        }
        Assert.True(anyChanged, "Drawing 'A' should produce white pixels");
    }

    /// <summary>
    /// DrawRegion fills a rectangular area with the specified color.
    /// </summary>
    [Fact]
    public void SkiaRenderer_DrawRegion_FillsColor()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        var color = new SKColor(0xFF, 0x00, 0x00);
        renderer.DrawRegion(10, 10, 20, 20, color);

        var pixel = renderer.BackBuffer!.GetPixel(15, 15);
        Assert.Equal(color, pixel);
    }

    /// <summary>
    /// Refresh fires the OnRefresh event.
    /// </summary>
    [Fact]
    public void SkiaRenderer_Refresh_FiresEvent()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        bool fired = false;
        renderer.OnRefresh += () => fired = true;
        renderer.Refresh();

        Assert.True(fired);
    }

    /// <summary>
    /// Reverse style (bit 1) swaps foreground and background.
    /// </summary>
    [Fact]
    public void SkiaRenderer_DrawCharacter_ReverseStyle_SwapsColors()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        // Draw 'A' with reverse (style=1), white fg / black bg
        // With reverse, background should be white, glyph pixels black
        renderer.DrawCharacter(0, 0, 'A', SKColors.White, SKColors.Black, 1);

        int x = theme.BorderWidth;
        int y = theme.BorderWidth;

        // Background pixels (corners of cell) should be white (reversed)
        var bgPixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(SKColors.White, bgPixel);
    }

    #endregion

    #region GuiScreen

    /// <summary>
    /// GuiScreen reports the correct screen size from the theme.
    /// </summary>
    [Fact]
    public void GuiScreen_GetScreenSize_MatchesTheme()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig { Columns = 40, Rows = 20 };
        var screen = new GuiScreen(renderer, theme);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(20, rows);
    }

    /// <summary>
    /// Print writes characters that appear in the back buffer.
    /// </summary>
    [Fact]
    public void GuiScreen_Print_DrawsToRenderer()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        screen.Print("Hi");
        screen.ForceRefresh();

        // The 'H' character should have white pixels at col 0, row 0
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool hasPixels = false;
        for (int py = 0; py < theme.CharHeight && !hasPixels; py++)
        for (int px = 0; px < theme.CharWidth && !hasPixels; px++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                hasPixels = true;
        }
        Assert.True(hasPixels, "Printed text should produce pixels");
    }

    /// <summary>
    /// SplitWindow / SetWindow switches between upper and lower windows.
    /// ZSpec S8.7.
    /// </summary>
    [Fact]
    public void GuiScreen_SplitAndSetWindow_SwitchesWindows()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        screen.SplitWindow(3);
        screen.SetWindow(1);
        screen.Print("UPPER");
        screen.SetWindow(0);
        screen.Print("lower");
        screen.ForceRefresh();

        // Verify upper window area (row 0) has content
        int ux = theme.BorderWidth;
        int uy = theme.BorderWidth;
        bool upperHasPixels = false;
        for (int px = 0; px < theme.CharWidth * 5 && !upperHasPixels; px++)
        for (int py = 0; py < theme.CharHeight && !upperHasPixels; py++)
        {
            if (renderer.BackBuffer!.GetPixel(ux + px, uy + py) == SKColors.White)
                upperHasPixels = true;
        }
        Assert.True(upperHasPixels, "Upper window should have rendered text");
    }

    /// <summary>
    /// EraseWindow -1 clears the screen and unsplits.
    /// ZSpec S8.7.3.
    /// </summary>
    [Fact]
    public void GuiScreen_EraseWindow_Minus1_ClearsAndUnsplits()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        screen.SplitWindow(3);
        screen.SetWindow(1);
        screen.Print("TEXT");
        screen.EraseWindow(-1);
        screen.ForceRefresh();

        // After erase, the area where TEXT was should be cleared to background
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool allBg = true;
        for (int px = 0; px < theme.CharWidth * 4 && allBg; px++)
        for (int py = 0; py < theme.CharHeight && allBg; py++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            if (pixel != SKColors.Black && pixel != theme.GetColor(theme.DefaultBackground))
                allBg = false;
        }
        Assert.True(allBg, "Screen should be cleared after EraseWindow(-1)");
    }

    /// <summary>
    /// SetTextStyle changes the rendering style for subsequent output.
    /// </summary>
    [Fact]
    public void GuiScreen_SetTextStyle_AffectsOutput()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        // Reverse video
        screen.SetTextStyle(1);
        screen.Print("R");
        screen.ForceRefresh();

        // With reverse, the background of the cell should be white (fg)
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        var bgPixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(SKColors.White, bgPixel);
    }

    /// <summary>
    /// ShowStatusLine renders reverse-video status text in the top row.
    /// ZSpec S8.2.
    /// </summary>
    [Fact]
    public void GuiScreen_ShowStatusLine_RendersTopRow()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        screen.ShowStatusLine("West of House", "Score: 0  Turns: 0");

        // Status line should have pixels in the first row (reverse video)
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool hasContent = false;
        for (int px = 0; px < theme.CharWidth * 10 && !hasContent; px++)
        for (int py = 0; py < theme.CharHeight && !hasContent; py++)
        {
            var pixel = renderer.BackBuffer!.GetPixel(x + px, y + py);
            if (pixel != SKColors.Black && pixel != theme.GetColor(theme.DefaultBackground))
                hasContent = true;
        }
        Assert.True(hasContent, "Status line should render visible content");
    }

    /// <summary>
    /// BufferMode toggle flushes the buffer when disabling.
    /// </summary>
    [Fact]
    public void GuiScreen_BufferMode_FlushesOnDisable()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        var screen = new GuiScreen(renderer, theme);

        screen.Print("Hello");
        screen.BufferMode(false);

        // After disabling buffer mode, text should be drawn
        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool hasPixels = false;
        for (int px = 0; px < theme.CharWidth * 5 && !hasPixels; px++)
        for (int py = 0; py < theme.CharHeight && !hasPixels; py++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                hasPixels = true;
        }
        Assert.True(hasPixels, "Buffered text should be flushed when buffer mode disabled");
    }

    #endregion

    #region GuiInputStream

    /// <summary>
    /// GuiInputStream enqueues and dequeues line input correctly.
    /// </summary>
    [Fact]
    public void GuiInputStream_EnqueueLine_DequeuesCorrectly()
    {
        var input = new GuiInputStream();

        input.EnqueueLine("go north");
        var (text, term) = input.ReadLine(255);

        Assert.Equal("go north", text);
        Assert.Equal(13, term);
    }

    /// <summary>
    /// GuiInputStream enqueues and dequeues character input.
    /// </summary>
    [Fact]
    public void GuiInputStream_EnqueueChar_DequeuesCorrectly()
    {
        var input = new GuiInputStream();

        input.EnqueueChar(65); // 'A'
        int result = input.ReadChar();

        Assert.Equal(65, result);
    }

    /// <summary>
    /// ReadLine with timeout returns empty string and 0 terminator on timeout.
    /// </summary>
    [Fact]
    public void GuiInputStream_ReadLine_Timeout_ReturnsEmpty()
    {
        var input = new GuiInputStream();

        var (text, term) = input.ReadLine(255, 1); // 0.1 second timeout

        Assert.Equal("", text);
        Assert.Equal(0, term);
    }

    /// <summary>
    /// ReadChar with timeout returns 0 on timeout.
    /// </summary>
    [Fact]
    public void GuiInputStream_ReadChar_Timeout_ReturnsZero()
    {
        var input = new GuiInputStream();

        int result = input.ReadChar(1); // 0.1 second timeout

        Assert.Equal(0, result);
    }

    /// <summary>
    /// ReadLine truncates to maxLength.
    /// </summary>
    [Fact]
    public void GuiInputStream_ReadLine_TruncatesToMaxLength()
    {
        var input = new GuiInputStream();

        input.EnqueueLine("this is a long line of text");
        var (text, _) = input.ReadLine(10);

        Assert.Equal("this is a ", text);
    }

    #endregion
}
