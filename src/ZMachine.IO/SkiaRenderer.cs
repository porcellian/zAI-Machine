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
    private BitmapFont? _font;

    // Raw font fields used when no BitmapFont is provided
    private byte[]? _fontData;
    private int _fontFirstChar;
    private int _fontGlyphCount;
    private int _fontCharWidth;
    private int _fontCharHeight;

    private int _cursorCol;
    private int _cursorRow;
    private bool _cursorVisible;
    private long _lastCursorToggle;

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

        // Prefer BitmapFont object; fall back to raw byte[] or BuiltInFont
        if (theme.Font != null)
        {
            _font = theme.Font;
            _fontCharWidth = _font.CharWidth;
            _fontCharHeight = _font.CharHeight;
        }
        else
        {
            _font = null;
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
        if (_canvas == null) return;

        int x = _theme.BorderWidth + col * _fontCharWidth;
        int y = _theme.BorderWidth + row * _fontCharHeight;

        // Delegate to BitmapFont when available
        if (_font != null)
        {
            _font.RenderGlyph(_canvas, _backBuffer!, c, x, y, fg, bg, style);
            return;
        }

        if (_fontData == null) return;

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
        _cursorVisible = true;
        _lastCursorToggle = Environment.TickCount64;
    }

    /// <summary>
    /// Draws a blinking block cursor at the current cursor position.
    /// Toggles visibility at ~1 Hz (every 500ms).
    /// </summary>
    public void DrawCursor(SKColor fg)
    {
        if (_canvas == null || _backBuffer == null) return;

        long now = Environment.TickCount64;
        if (now - _lastCursorToggle >= 500)
        {
            _cursorVisible = !_cursorVisible;
            _lastCursorToggle = now;
        }

        if (!_cursorVisible) return;

        int x = _theme.BorderWidth + _cursorCol * _fontCharWidth;
        int y = _theme.BorderWidth + _cursorRow * _fontCharHeight;

        _paint.Color = fg;
        _canvas.DrawRect(x, y, _fontCharWidth, _fontCharHeight, _paint);
    }

    /// <summary>
    /// Applies scanline overlay to the back buffer: darkens every other
    /// pixel row by ~30% to simulate CRT scanline gaps.
    /// </summary>
    public void ApplyScanlines()
    {
        if (_backBuffer == null) return;

        int w = _backBuffer.Width;
        int h = _backBuffer.Height;

        for (int y = 1; y < h; y += 2)
        {
            for (int x = 0; x < w; x++)
            {
                var pixel = _backBuffer.GetPixel(x, y);
                var darkened = new SKColor(
                    (byte)(pixel.Red * 0.7f),
                    (byte)(pixel.Green * 0.7f),
                    (byte)(pixel.Blue * 0.7f),
                    pixel.Alpha);
                _backBuffer.SetPixel(x, y, darkened);
            }
        }
    }

    /// <summary>
    /// Applies CRT barrel distortion to the back buffer. Warps pixels
    /// outward from center to simulate a curved CRT screen surface.
    /// </summary>
    public void ApplyCrtCurvature(float strength = 0.02f)
    {
        if (_backBuffer == null) return;

        int w = _backBuffer.Width;
        int h = _backBuffer.Height;

        using var source = _backBuffer.Copy();
        if (source == null) return;

        float cx = w / 2f;
        float cy = h / 2f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / cx;
                float dy = (y - cy) / cy;
                float r2 = dx * dx + dy * dy;

                float sx = cx + dx * (1 + strength * r2) * cx;
                float sy = cy + dy * (1 + strength * r2) * cy;

                int ix = (int)sx;
                int iy = (int)sy;

                if (ix >= 0 && ix < w && iy >= 0 && iy < h)
                    _backBuffer.SetPixel(x, y, source.GetPixel(ix, iy));
                else
                    _backBuffer.SetPixel(x, y, SKColors.Black);
            }
        }
    }

    /// <summary>
    /// Applies phosphor bloom/glow by blending each pixel with its
    /// neighbors' brightness, simulating CRT phosphor bleed.
    /// </summary>
    public void ApplyPhosphorBloom(float intensity = 0.15f)
    {
        if (_backBuffer == null) return;

        int w = _backBuffer.Width;
        int h = _backBuffer.Height;

        using var source = _backBuffer.Copy();
        if (source == null) return;

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                var c = source.GetPixel(x, y);

                // Only bloom bright pixels
                int brightness = (c.Red + c.Green + c.Blue) / 3;
                if (brightness < 64) continue;

                var left = source.GetPixel(x - 1, y);
                var right = source.GetPixel(x + 1, y);
                var up = source.GetPixel(x, y - 1);
                var down = source.GetPixel(x, y + 1);

                byte r = (byte)Math.Min(255, c.Red + intensity *
                    (left.Red + right.Red + up.Red + down.Red) / 4f);
                byte g = (byte)Math.Min(255, c.Green + intensity *
                    (left.Green + right.Green + up.Green + down.Green) / 4f);
                byte b = (byte)Math.Min(255, c.Blue + intensity *
                    (left.Blue + right.Blue + up.Blue + down.Blue) / 4f);

                _backBuffer.SetPixel(x, y, new SKColor(r, g, b, c.Alpha));
            }
        }
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
