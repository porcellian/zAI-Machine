namespace ZMachine.IO;

using SkiaSharp;

/// <summary>
/// Fixed-width bitmap font for pixel-perfect retro rendering. Each glyph
/// is stored as 1-bit-per-pixel row data (MSB = leftmost pixel, one byte
/// per row for widths ≤ 8). Fonts are loaded from embedded byte arrays.
/// </summary>
/// <remarks>
/// ZSpec S8 — The screen model uses fixed-width character cells.
/// Each vintage theme provides a BitmapFont matching its platform's
/// original character ROM (C64, Apple II, IBM PC, Amiga).
/// </remarks>
public class BitmapFont
{
    private readonly byte[] _data;
    private readonly int _firstChar;
    private readonly int _glyphCount;
    private readonly int[] _zsciiMap;

    /// <summary>Character cell width in pixels.</summary>
    public int CharWidth { get; }

    /// <summary>Character cell height in pixels.</summary>
    public int CharHeight { get; }

    /// <summary>Display name of this font.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates a BitmapFont from raw glyph data.
    /// </summary>
    /// <param name="name">Display name (e.g. "C64", "VGA 8×16").</param>
    /// <param name="data">
    /// Glyph bitmap data: one byte per row, MSB = leftmost pixel.
    /// Total size = glyphCount × charHeight bytes.
    /// </param>
    /// <param name="charWidth">Width of each character cell in pixels (max 8).</param>
    /// <param name="charHeight">Height of each character cell in pixels.</param>
    /// <param name="firstChar">First character code in the data (typically 32).</param>
    /// <param name="glyphCount">Number of glyphs in the data.</param>
    /// <param name="zsciiMap">
    /// Optional mapping for ZSCII extra characters 155–251. Each entry maps
    /// a ZSCII code to a character code in this font's range, or -1 for
    /// the fallback box glyph. Length must be 97 (codes 155–251).
    /// </param>
    public BitmapFont(string name, byte[] data, int charWidth, int charHeight,
        int firstChar = 32, int glyphCount = 95, int[]? zsciiMap = null)
    {
        Name = name;
        _data = data;
        CharWidth = charWidth;
        CharHeight = charHeight;
        _firstChar = firstChar;
        _glyphCount = glyphCount;
        _zsciiMap = zsciiMap ?? BuildDefaultZsciiMap();
    }

    /// <summary>
    /// Returns the raw glyph bitmap for a character: charHeight bytes,
    /// one byte per row, MSB = leftmost pixel.
    /// Returns the '?' glyph for characters outside the font's range.
    /// </summary>
    public byte[] GetGlyph(char c)
    {
        int index = ResolveGlyphIndex(c);
        var glyph = new byte[CharHeight];
        int offset = index * CharHeight;

        for (int i = 0; i < CharHeight && offset + i < _data.Length; i++)
            glyph[i] = _data[offset + i];

        return glyph;
    }

    /// <summary>
    /// Renders a glyph directly to an SKCanvas at pixel position (x, y).
    /// Draws foreground pixels from the bitmap and fills the cell background.
    /// </summary>
    public void RenderGlyph(SKCanvas canvas, SKBitmap bitmap, char c,
        int x, int y, SKColor fg, SKColor bg, int style)
    {
        var actualFg = fg;
        var actualBg = bg;

        // ZSpec S8.7.1 — Reverse video swaps foreground and background
        if ((style & 1) != 0)
            (actualFg, actualBg) = (actualBg, actualFg);

        // Fill background cell
        using var paint = new SKPaint { Color = actualBg };
        canvas.DrawRect(x, y, CharWidth, CharHeight, paint);

        int index = ResolveGlyphIndex(c);
        int glyphOffset = index * CharHeight;

        // Draw glyph pixels
        for (int py = 0; py < CharHeight; py++)
        {
            if (glyphOffset + py >= _data.Length) break;
            byte row = _data[glyphOffset + py];

            for (int px = 0; px < CharWidth; px++)
            {
                if ((row & (0x80 >> px)) != 0)
                    bitmap.SetPixel(x + px, y + py, actualFg);
            }
        }

        // ZSpec S8.7.1 — Bold: overdraw shifted right by 1 pixel
        if ((style & 2) != 0)
        {
            for (int py = 0; py < CharHeight; py++)
            {
                if (glyphOffset + py >= _data.Length) break;
                byte row = _data[glyphOffset + py];

                for (int px = 0; px < CharWidth - 1; px++)
                {
                    if ((row & (0x80 >> px)) != 0)
                        bitmap.SetPixel(x + px + 1, y + py, actualFg);
                }
            }
        }

        // ZSpec S8.7.1 — Italic: shift top half left by 1 pixel
        if ((style & 4) != 0)
        {
            int halfH = CharHeight / 2;
            for (int py = 0; py < halfH; py++)
            {
                if (glyphOffset + py >= _data.Length) break;
                byte row = _data[glyphOffset + py];

                for (int px = 1; px < CharWidth; px++)
                {
                    if ((row & (0x80 >> px)) != 0)
                        bitmap.SetPixel(x + px - 1, y + py, actualFg);
                }
            }
        }
    }

    /// <summary>
    /// Resolves a character to a glyph index in the font data.
    /// Handles ZSCII extra characters 155–251 via the mapping table.
    /// </summary>
    private int ResolveGlyphIndex(char c)
    {
        int code = c;

        // ZSCII extra characters 155–251
        if (code >= 155 && code <= 251)
        {
            int mapped = _zsciiMap[code - 155];
            if (mapped >= 0)
                code = mapped;
            else
                return '?' - _firstChar; // fallback
        }

        int index = code - _firstChar;
        if (index < 0 || index >= _glyphCount)
            return Math.Max(0, '?' - _firstChar);

        return index;
    }

    /// <summary>
    /// Builds a default ZSCII 155–251 mapping that maps accented Latin
    /// characters to their unaccented ASCII equivalents where possible.
    /// ZSpec S3.8.5 — Default extra characters.
    /// </summary>
    private static int[] BuildDefaultZsciiMap()
    {
        var map = new int[97];
        Array.Fill(map, -1);

        // ZSpec S3.8.5 — Default ZSCII extra characters (155–223 are defined)
        // Derived from ZsciiEncoder.DefaultExtraCharacters order:
        // 155 ä, 156 ö, 157 ü, 158 Ä, 159 Ö, 160 Ü,
        // 161 ß, 162 », 163 «,
        // 164 ë, 165 ï, 166 ÿ, 167 Ë, 168 Ï,
        // 169 á, 170 é, 171 í, 172 ó, 173 ú, 174 ý,
        // 175 Á, 176 É, 177 Í, 178 Ó, 179 Ú, 180 Ý,
        // 181 à, 182 è, 183 ì, 184 ò, 185 ù, 186 À,
        // 187 È, 188 Ì, 189 Ò, 190 Ù, 191 â,
        // 192 ê, 193 î, 194 ô, 195 û, 196 Â,
        // 197 Ê, 198 Î, 199 Ô, 200 Û, 201 å,
        // 202 Å, 203 ø, 204 Ø, 205 ã,
        // 206 ñ, 207 õ, 208 Ã, 209 Ñ,
        // 210 Õ, 211 æ, 212 Æ, 213 ç,
        // 214 Ç, 215 þ, 216 ð, 217 Þ, 218 Ð,
        // 219 £, 220 œ, 221 Œ, 222 ¡, 223 ¿
        char[] zsciiDefaults =
        [
            'a', 'o', 'u', 'A', 'O', 'U', 's', '>', '<', 'e',  // 155-164
            'i', 'y', 'E', 'I', 'a', 'e', 'i', 'o', 'u', 'y',  // 165-174
            'A', 'E', 'I', 'O', 'U', 'Y', 'a', 'e', 'i', 'o',  // 175-184
            'u', 'A', 'E', 'I', 'O', 'U', 'a', 'e', 'i', 'o',  // 185-194
            'u', 'A', 'E', 'I', 'O', 'U', 'a', 'A', 'o', 'O',  // 195-204
            'a', 'n', 'o', 'A', 'N', 'O', 'a', 'A', 'c', 'C',  // 205-214
            't', 'd', 'T', 'D', 'L', 'o', 'O', '!', '?',        // 215-223
        ];

        for (int i = 0; i < zsciiDefaults.Length && i < 97; i++)
            map[i] = zsciiDefaults[i];

        return map;
    }

    /// <summary>
    /// Creates a BitmapFont backed by BuiltInFont (VGA 8×16 fallback).
    /// </summary>
    public static BitmapFont CreateBuiltIn()
    {
        return new BitmapFont("VGA 8×16", BuiltInFont.Data,
            BuiltInFont.CharWidth, BuiltInFont.CharHeight,
            BuiltInFont.FirstChar, BuiltInFont.GlyphCount);
    }
}
