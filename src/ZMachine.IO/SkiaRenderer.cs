namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// SkiaSharp-based IRenderer that draws to an off-screen SKBitmap back
/// buffer. All character and image drawing goes through SkiaSharp pixel
/// operations for pixel-precise retro rendering.
/// </summary>
/// <remarks>
/// ZSpec S8 — Screen output is drawn character-by-character into a grid.
/// The back buffer is blitted to the Avalonia canvas on Refresh().
/// </remarks>
public class SkiaRenderer : IRenderer, IDisposable
{
    private SKBitmap? _backBuffer;
    private SKCanvas? _canvas;
    private ThemeConfig _theme = new();
    private SKPaint _paint = new();

    // Built-in 8x16 font as a fallback when no bitmap font is provided.
    // Each glyph is 8 pixels wide × 16 pixels tall, 1 byte per row.
    private byte[]? _fontData;
    private int _fontFirstChar;
    private int _fontGlyphCount;
    private int _fontCharWidth;
    private int _fontCharHeight;

    private int _cursorCol;
    private int _cursorRow;

    /// <summary>Event raised when a Refresh() completes, for UI invalidation.</summary>
    public event Action? OnRefresh;

    public SKBitmap? BackBuffer => _backBuffer;

    public void Initialize(int widthPixels, int heightPixels, ThemeConfig theme)
    {
        _backBuffer?.Dispose();
        _canvas?.Dispose();

        _theme = theme;
        _backBuffer = new SKBitmap(widthPixels, heightPixels, SKColorType.Rgba8888, SKAlphaType.Premul);
        _canvas = new SKCanvas(_backBuffer);

        _fontCharWidth = theme.CharWidth;
        _fontCharHeight = theme.CharHeight;
        _fontFirstChar = theme.FontFirstChar;
        _fontGlyphCount = theme.FontGlyphCount;

        if (theme.FontBitmap != null)
        {
            _fontData = theme.FontBitmap;
        }
        else
        {
            _fontData = BuiltInFont.Data;
            _fontFirstChar = BuiltInFont.FirstChar;
            _fontGlyphCount = BuiltInFont.GlyphCount;
        }

        // Clear to border color
        _canvas.Clear(theme.BorderColor);

        // Clear character area to default background
        var bgColor = theme.GetColor(theme.DefaultBackground);
        DrawRegion(theme.BorderWidth, theme.BorderWidth,
            theme.Columns * theme.CharWidth, theme.Rows * theme.CharHeight,
            bgColor);
    }

    public void DrawCharacter(int col, int row, char c, SKColor fg, SKColor bg, int style)
    {
        if (_canvas == null || _fontData == null) return;

        int x = _theme.BorderWidth + col * _fontCharWidth;
        int y = _theme.BorderWidth + row * _fontCharHeight;

        var actualFg = fg;
        var actualBg = bg;

        // ZSpec S8.7.1 — Reverse video swaps foreground and background
        if ((style & 1) != 0)
            (actualFg, actualBg) = (actualBg, actualFg);

        // Draw background cell
        _paint.Color = actualBg;
        _canvas.DrawRect(x, y, _fontCharWidth, _fontCharHeight, _paint);

        // Draw the glyph from font bitmap
        int glyphIndex = c - _fontFirstChar;
        if (glyphIndex < 0 || glyphIndex >= _fontGlyphCount)
            glyphIndex = '?' - _fontFirstChar;

        int glyphOffset = glyphIndex * _fontCharHeight;

        for (int py = 0; py < _fontCharHeight; py++)
        {
            if (glyphOffset + py >= _fontData.Length) break;
            byte row8 = _fontData[glyphOffset + py];

            for (int px = 0; px < _fontCharWidth; px++)
            {
                // MSB first — bit 7 is leftmost pixel
                if ((row8 & (0x80 >> px)) != 0)
                {
                    _backBuffer!.SetPixel(x + px, y + py, actualFg);
                }
            }
        }

        // ZSpec S8.7.1 — Bold: draw shifted right by 1 pixel (OR)
        if ((style & 2) != 0)
        {
            for (int py = 0; py < _fontCharHeight; py++)
            {
                if (glyphOffset + py >= _fontData.Length) break;
                byte row8 = _fontData[glyphOffset + py];

                for (int px = 0; px < _fontCharWidth - 1; px++)
                {
                    if ((row8 & (0x80 >> px)) != 0)
                        _backBuffer!.SetPixel(x + px + 1, y + py, actualFg);
                }
            }
        }

        // Italic: shift top half left by 1 pixel (approximation)
        if ((style & 4) != 0)
        {
            int halfH = _fontCharHeight / 2;
            for (int py = 0; py < halfH; py++)
            {
                if (glyphOffset + py >= _fontData.Length) break;
                byte row8 = _fontData[glyphOffset + py];

                for (int px = 1; px < _fontCharWidth; px++)
                {
                    if ((row8 & (0x80 >> px)) != 0)
                        _backBuffer!.SetPixel(x + px - 1, y + py, actualFg);
                }
            }
        }
    }

    public void DrawRegion(int x, int y, int w, int h, SKColor color)
    {
        if (_canvas == null) return;
        _paint.Color = color;
        _canvas.DrawRect(x, y, w, h, _paint);
    }

    public void DrawImage(int x, int y, SKBitmap image, int scaledW, int scaledH)
    {
        if (_canvas == null) return;
        var dest = new SKRect(x, y, x + scaledW, y + scaledH);
        _canvas.DrawBitmap(image, dest, SKSamplingOptions.Default);
    }

    public void SetCursorPosition(int col, int row)
    {
        _cursorCol = col;
        _cursorRow = row;
    }

    public void Refresh()
    {
        OnRefresh?.Invoke();
    }

    public (int Columns, int Rows) GetScreenSize()
    {
        return (_theme.Columns, _theme.Rows);
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _backBuffer?.Dispose();
        _paint.Dispose();
        GC.SuppressFinalize(this);
    }
}
