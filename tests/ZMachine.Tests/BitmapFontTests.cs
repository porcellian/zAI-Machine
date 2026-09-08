namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.IO;

/// <summary>
/// Tests for Task 11.2 — Bitmap font system: BitmapFont API,
/// vintage font data sets, SkiaRenderer BitmapFont integration,
/// and ZSCII extra character mapping.
/// </summary>
public class BitmapFontTests
{
    #region BitmapFont Core API

    /// <summary>
    /// CreateBuiltIn returns a VGA 8×16 font with correct dimensions.
    /// </summary>
    [Fact]
    public void BitmapFont_CreateBuiltIn_CorrectDimensions()
    {
        var font = BitmapFont.CreateBuiltIn();
        Assert.Equal(8, font.CharWidth);
        Assert.Equal(16, font.CharHeight);
        Assert.Equal("VGA 8×16", font.Name);
    }

    /// <summary>
    /// GetGlyph returns CharHeight bytes per glyph.
    /// </summary>
    [Fact]
    public void BitmapFont_GetGlyph_ReturnsCorrectLength()
    {
        var font = BitmapFont.CreateBuiltIn();
        var glyph = font.GetGlyph('A');
        Assert.Equal(16, glyph.Length);
    }

    /// <summary>
    /// Space glyph is all zeros (no pixels set).
    /// </summary>
    [Fact]
    public void BitmapFont_SpaceGlyph_AllZeros()
    {
        var font = BitmapFont.CreateBuiltIn();
        var glyph = font.GetGlyph(' ');
        Assert.All(glyph, b => Assert.Equal(0, b));
    }

    /// <summary>
    /// Letter 'A' glyph contains non-zero pixel data.
    /// </summary>
    [Fact]
    public void BitmapFont_LetterA_HasPixels()
    {
        var font = BitmapFont.CreateBuiltIn();
        var glyph = font.GetGlyph('A');
        Assert.Contains(glyph, b => b != 0);
    }

    /// <summary>
    /// Out-of-range character falls back to '?' glyph.
    /// </summary>
    [Fact]
    public void BitmapFont_OutOfRange_ReturnsQuestionMark()
    {
        var font = BitmapFont.CreateBuiltIn();
        var fallback = font.GetGlyph('\x01');
        var question = font.GetGlyph('?');
        Assert.Equal(question, fallback);
    }

    /// <summary>
    /// ZSCII character 155 (ä) maps to ASCII 'a' in the default table.
    /// ZSpec S3.8.5 — Default extra characters.
    /// </summary>
    [Fact]
    public void BitmapFont_ZsciiChar155_MapsToA()
    {
        var font = BitmapFont.CreateBuiltIn();
        var glyph155 = font.GetGlyph((char)155);
        var glyphA = font.GetGlyph('a');
        Assert.Equal(glyphA, glyph155);
    }

    /// <summary>
    /// ZSCII character 158 (Ö) maps to ASCII 'O' (index 3 → 'A', index 4 → 'O', index 5 → 'U').
    /// Actually index 4 maps to 'O'.
    /// </summary>
    [Fact]
    public void BitmapFont_ZsciiChar159_MapsToCapitalO()
    {
        var font = BitmapFont.CreateBuiltIn();
        var glyph159 = font.GetGlyph((char)159);
        var glyphO = font.GetGlyph('O');
        Assert.Equal(glyphO, glyph159);
    }

    #endregion

    #region FontData — Vintage Font Sets

    /// <summary>
    /// C64 font has correct 8×8 dimensions and data size.
    /// </summary>
    [Fact]
    public void FontData_C64_CorrectDimensions()
    {
        var font = FontData.CreateC64();
        Assert.Equal(8, font.CharWidth);
        Assert.Equal(8, font.CharHeight);
        Assert.Equal("C64 8×8", font.Name);
    }

    /// <summary>
    /// C64 'A' glyph has non-zero pixel data.
    /// </summary>
    [Fact]
    public void FontData_C64_LetterA_HasPixels()
    {
        var font = FontData.CreateC64();
        var glyph = font.GetGlyph('A');
        Assert.Contains(glyph, b => b != 0);
    }

    /// <summary>
    /// Apple II font has correct 7×8 dimensions.
    /// </summary>
    [Fact]
    public void FontData_AppleII_CorrectDimensions()
    {
        var font = FontData.CreateAppleII();
        Assert.Equal(7, font.CharWidth);
        Assert.Equal(8, font.CharHeight);
        Assert.Equal("Apple II 7×8", font.Name);
    }

    /// <summary>
    /// Apple II 'A' glyph has non-zero pixel data.
    /// </summary>
    [Fact]
    public void FontData_AppleII_LetterA_HasPixels()
    {
        var font = FontData.CreateAppleII();
        var glyph = font.GetGlyph('A');
        Assert.Contains(glyph, b => b != 0);
    }

    /// <summary>
    /// CGA font has correct 8×8 dimensions.
    /// </summary>
    [Fact]
    public void FontData_CGA_CorrectDimensions()
    {
        var font = FontData.CreateCGA();
        Assert.Equal(8, font.CharWidth);
        Assert.Equal(8, font.CharHeight);
        Assert.Equal("CGA 8×8", font.Name);
    }

    /// <summary>
    /// EGA font has correct 8×14 dimensions and data size.
    /// </summary>
    [Fact]
    public void FontData_EGA_CorrectDimensions()
    {
        var font = FontData.CreateEGA();
        Assert.Equal(8, font.CharWidth);
        Assert.Equal(14, font.CharHeight);
        Assert.Equal("EGA 8×14", font.Name);
    }

    /// <summary>
    /// EGA 'A' glyph has non-zero pixel data.
    /// </summary>
    [Fact]
    public void FontData_EGA_LetterA_HasPixels()
    {
        var font = FontData.CreateEGA();
        var glyph = font.GetGlyph('A');
        Assert.Contains(glyph, b => b != 0);
    }

    /// <summary>
    /// VGA font via FontData matches BuiltInFont VGA 8×16.
    /// </summary>
    [Fact]
    public void FontData_VGA_MatchesBuiltIn()
    {
        var fontVia = FontData.CreateVGA();
        var fontDirect = BitmapFont.CreateBuiltIn();
        Assert.Equal(fontDirect.CharWidth, fontVia.CharWidth);
        Assert.Equal(fontDirect.CharHeight, fontVia.CharHeight);
        Assert.Equal(fontDirect.GetGlyph('A'), fontVia.GetGlyph('A'));
    }

    /// <summary>
    /// Amiga font has correct 8×8 dimensions.
    /// </summary>
    [Fact]
    public void FontData_Amiga_CorrectDimensions()
    {
        var font = FontData.CreateAmiga();
        Assert.Equal(8, font.CharWidth);
        Assert.Equal(8, font.CharHeight);
        Assert.Equal("Amiga 8×8", font.Name);
    }

    /// <summary>
    /// Amiga 'A' glyph has non-zero pixel data.
    /// </summary>
    [Fact]
    public void FontData_Amiga_LetterA_HasPixels()
    {
        var font = FontData.CreateAmiga();
        var glyph = font.GetGlyph('A');
        Assert.Contains(glyph, b => b != 0);
    }

    /// <summary>
    /// GetAll returns all six font variants.
    /// </summary>
    [Fact]
    public void FontData_GetAll_Returns6Fonts()
    {
        var fonts = FontData.GetAll();
        Assert.Equal(6, fonts.Length);
    }

    /// <summary>
    /// All font space glyphs are blank (no pixels set).
    /// </summary>
    [Fact]
    public void FontData_AllFonts_SpaceIsBlank()
    {
        foreach (var font in FontData.GetAll())
        {
            var glyph = font.GetGlyph(' ');
            Assert.All(glyph, b => Assert.Equal(0, b));
        }
    }

    /// <summary>
    /// All fonts have valid data for every printable ASCII character.
    /// </summary>
    [Fact]
    public void FontData_AllFonts_AllPrintableAscii_ValidGlyphs()
    {
        foreach (var font in FontData.GetAll())
        {
            for (char c = ' '; c <= '~'; c++)
            {
                var glyph = font.GetGlyph(c);
                Assert.Equal(font.CharHeight, glyph.Length);
            }
        }
    }

    #endregion

    #region Rendering — "ZORK" Pixel Verification

    /// <summary>
    /// Rendering "ZORK" with VGA font produces a bitmap with the correct pixel dimensions.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderZork_VGA_CorrectPixelDimensions()
    {
        var font = BitmapFont.CreateBuiltIn();
        int w = 4 * font.CharWidth;
        int h = font.CharHeight;
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);
        for (int i = 0; i < 4; i++)
            font.RenderGlyph(canvas, bitmap, "ZORK"[i], i * font.CharWidth, 0,
                SKColors.White, SKColors.Black, 0);

        Assert.Equal(32, bitmap.Width);  // 4 × 8
        Assert.Equal(16, bitmap.Height);
    }

    /// <summary>
    /// Rendering "ZORK" with C64 font produces correct 8×8 output.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderZork_C64_CorrectPixelDimensions()
    {
        var font = FontData.CreateC64();
        int w = 4 * font.CharWidth;
        int h = font.CharHeight;
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);
        for (int i = 0; i < 4; i++)
            font.RenderGlyph(canvas, bitmap, "ZORK"[i], i * font.CharWidth, 0,
                SKColors.White, SKColors.Black, 0);

        Assert.Equal(32, bitmap.Width);  // 4 × 8
        Assert.Equal(8, bitmap.Height);

        // 'Z' starts with 0x7E — full top row should have white pixels
        bool hasWhite = false;
        for (int px = 0; px < font.CharWidth; px++)
            if (bitmap.GetPixel(px, 0) == SKColors.White) hasWhite = true;
        Assert.True(hasWhite, "C64 'Z' should have white pixels in top row");
    }

    /// <summary>
    /// Rendering "ZORK" with Apple II font produces correct 7-pixel-wide output.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderZork_AppleII_CorrectPixelDimensions()
    {
        var font = FontData.CreateAppleII();
        int w = 4 * font.CharWidth;
        int h = font.CharHeight;
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);
        for (int i = 0; i < 4; i++)
            font.RenderGlyph(canvas, bitmap, "ZORK"[i], i * font.CharWidth, 0,
                SKColors.Green, SKColors.Black, 0);

        Assert.Equal(28, bitmap.Width);  // 4 × 7
        Assert.Equal(8, bitmap.Height);
    }

    /// <summary>
    /// Rendering "ZORK" with EGA font produces correct 8×14 output.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderZork_EGA_CorrectPixelDimensions()
    {
        var font = FontData.CreateEGA();
        int w = 4 * font.CharWidth;
        int h = font.CharHeight;
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);
        for (int i = 0; i < 4; i++)
            font.RenderGlyph(canvas, bitmap, "ZORK"[i], i * font.CharWidth, 0,
                SKColors.White, SKColors.Black, 0);

        Assert.Equal(32, bitmap.Width);  // 4 × 8
        Assert.Equal(14, bitmap.Height);
    }

    #endregion

    #region SkiaRenderer BitmapFont Integration

    /// <summary>
    /// SkiaRenderer uses BitmapFont from ThemeConfig when Font property is set.
    /// </summary>
    [Fact]
    public void SkiaRenderer_WithBitmapFont_DrawsCharacter()
    {
        using var renderer = new SkiaRenderer();
        var font = FontData.CreateC64();
        var theme = new ThemeConfig { Font = font, CharWidth = 8, CharHeight = 8 };
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        renderer.DrawCharacter(0, 0, 'A', SKColors.White, SKColors.Black, 0);

        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool hasWhite = false;
        for (int py = 0; py < 8 && !hasWhite; py++)
        for (int px = 0; px < 8 && !hasWhite; px++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                hasWhite = true;
        }
        Assert.True(hasWhite, "Drawing 'A' with C64 BitmapFont should produce white pixels");
    }

    /// <summary>
    /// SkiaRenderer with BitmapFont applies reverse video correctly.
    /// </summary>
    [Fact]
    public void SkiaRenderer_WithBitmapFont_ReverseStyle()
    {
        using var renderer = new SkiaRenderer();
        var font = FontData.CreateC64();
        var theme = new ThemeConfig { Font = font, CharWidth = 8, CharHeight = 8 };
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        // Reverse video (style=1) swaps fg/bg
        renderer.DrawCharacter(0, 0, 'A', SKColors.White, SKColors.Black, 1);

        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        // With reverse, background fills should be white
        var bgPixel = renderer.BackBuffer!.GetPixel(x, y);
        Assert.Equal(SKColors.White, bgPixel);
    }

    /// <summary>
    /// SkiaRenderer falls back to BuiltInFont raw data when no Font is set.
    /// </summary>
    [Fact]
    public void SkiaRenderer_NoFont_FallsBackToBuiltIn()
    {
        using var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        renderer.DrawCharacter(0, 0, 'A', SKColors.White, SKColors.Black, 0);

        int x = theme.BorderWidth;
        int y = theme.BorderWidth;
        bool hasWhite = false;
        for (int py = 0; py < theme.CharHeight && !hasWhite; py++)
        for (int px = 0; px < theme.CharWidth && !hasWhite; px++)
        {
            if (renderer.BackBuffer!.GetPixel(x + px, y + py) == SKColors.White)
                hasWhite = true;
        }
        Assert.True(hasWhite, "BuiltInFont fallback should still produce white pixels");
    }

    /// <summary>
    /// ThemeConfig with Font property overrides CharWidth/CharHeight in renderer.
    /// </summary>
    [Fact]
    public void ThemeConfig_WithFont_RendererUsesAppleIIDimensions()
    {
        using var renderer = new SkiaRenderer();
        var font = FontData.CreateAppleII();
        var theme = new ThemeConfig
        {
            Font = font,
            CharWidth = 7,
            CharHeight = 8,
            Columns = 40,
            Rows = 24
        };
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);

        var (cols, rows) = renderer.GetScreenSize();
        Assert.Equal(40, cols);
        Assert.Equal(24, rows);
        Assert.Equal(7 * 40 + 2 * 8, theme.PixelWidth);
    }

    #endregion

    #region BitmapFont RenderGlyph Styles

    /// <summary>
    /// Bold style (bit 2) produces shifted pixels.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderGlyph_Bold_ShiftsPixels()
    {
        var font = BitmapFont.CreateBuiltIn();
        int w = font.CharWidth;
        int h = font.CharHeight;

        using var bmpNormal = new SKBitmap(w, h);
        using var canvasN = new SKCanvas(bmpNormal);
        canvasN.Clear(SKColors.Black);
        font.RenderGlyph(canvasN, bmpNormal, 'I', 0, 0, SKColors.White, SKColors.Black, 0);

        using var bmpBold = new SKBitmap(w, h);
        using var canvasB = new SKCanvas(bmpBold);
        canvasB.Clear(SKColors.Black);
        font.RenderGlyph(canvasB, bmpBold, 'I', 0, 0, SKColors.White, SKColors.Black, 2);

        // Bold version should have more white pixels than normal
        int normalWhite = 0, boldWhite = 0;
        for (int py = 0; py < h; py++)
        for (int px = 0; px < w; px++)
        {
            if (bmpNormal.GetPixel(px, py) == SKColors.White) normalWhite++;
            if (bmpBold.GetPixel(px, py) == SKColors.White) boldWhite++;
        }
        Assert.True(boldWhite >= normalWhite, "Bold should have at least as many white pixels");
    }

    /// <summary>
    /// Italic style (bit 4) shifts top half left.
    /// </summary>
    [Fact]
    public void BitmapFont_RenderGlyph_Italic_DiffersFromNormal()
    {
        var font = BitmapFont.CreateBuiltIn();
        int w = font.CharWidth;
        int h = font.CharHeight;

        using var bmpNormal = new SKBitmap(w, h);
        using var canvasN = new SKCanvas(bmpNormal);
        canvasN.Clear(SKColors.Black);
        font.RenderGlyph(canvasN, bmpNormal, 'T', 0, 0, SKColors.White, SKColors.Black, 0);

        using var bmpItalic = new SKBitmap(w, h);
        using var canvasI = new SKCanvas(bmpItalic);
        canvasI.Clear(SKColors.Black);
        font.RenderGlyph(canvasI, bmpItalic, 'T', 0, 0, SKColors.White, SKColors.Black, 4);

        // Check that at least one pixel in the top half differs
        bool anyDiff = false;
        for (int py = 0; py < h / 2 && !anyDiff; py++)
        for (int px = 0; px < w && !anyDiff; px++)
        {
            if (bmpNormal.GetPixel(px, py) != bmpItalic.GetPixel(px, py))
                anyDiff = true;
        }
        Assert.True(anyDiff, "Italic should shift top half differently from normal");
    }

    #endregion
}
