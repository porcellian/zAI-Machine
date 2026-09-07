namespace ZMachine.Core;

/// <summary>
/// Converts between ZSCII character codes and Unicode code points.
/// ZSCII 32–126 maps directly to ASCII. ZSCII 155–251 maps to "extra
/// characters" — a default Latin-like set that can be overridden by a
/// Unicode translation table in V5+.
/// </summary>
/// <remarks>
/// ZSpec S3.8 — ZSCII character set definition.
/// ZSpec S3.8.5 — Default extra characters (69 entries at ZSCII 155–223).
/// ZSpec11 "Character set" — $27 = right-single-quote, $60 = left-single-quote.
/// ZSpec11 "Unicode" — Control code filtering, no non-BMP.
/// </remarks>
public class ZsciiEncoder
{
    /// <summary>
    /// Default extra characters table: ZSCII 155–223 (69 entries).
    /// Each entry is a Unicode code point. ZSCII 224–251 are undefined
    /// by default (mapped to '?' as a fallback).
    /// </summary>
    /// <remarks>
    /// ZSpec S3.8.5 — The standard table covers common accented Latin
    /// characters used in Western European languages.
    /// </remarks>
    private static readonly char[] DefaultExtraCharacters =
    {
        // 155–160: ä ö ü Ä Ö Ü
        'ä', 'ö', 'ü', 'Ä', 'Ö', 'Ü',
        // 161–163: ß » «
        'ß', '»', '«',
        // 164–168: ë ï ÿ Ë Ï
        'ë', 'ï', 'ÿ', 'Ë', 'Ï',
        // 169–174: á é í ó ú ý
        'á', 'é', 'í', 'ó', 'ú', 'ý',
        // 175–180: Á É Í Ó Ú Ý
        'Á', 'É', 'Í', 'Ó', 'Ú', 'Ý',
        // 181–186: à è ì ò ù À
        'à', 'è', 'ì', 'ò', 'ù', 'À',
        // 187–191: È Ì Ò Ù â
        'È', 'Ì', 'Ò', 'Ù', 'â',
        // 192–196: ê î ô û Â
        'ê', 'î', 'ô', 'û', 'Â',
        // 197–201: Ê Î Ô Û å
        'Ê', 'Î', 'Ô', 'Û', 'å',
        // 202–205: Å ø Ø ã
        'Å', 'ø', 'Ø', 'ã',
        // 206–209: ñ õ Ã Ñ
        'ñ', 'õ', 'Ã', 'Ñ',
        // 210–213: Õ æ Æ ç
        'Õ', 'æ', 'Æ', 'ç',
        // 214–218: Ç þ ð Þ Ð
        'Ç', 'þ', 'ð', 'Þ', 'Ð',
        // 219–223: £ œ Œ ¡ ¿
        '£', 'œ', 'Œ', '¡', '¿',
    };

    private readonly char[] _extraCharacters;

    /// <summary>
    /// Reverse lookup: Unicode code point → ZSCII code. Built lazily
    /// from whichever extra characters table is active.
    /// </summary>
    private readonly Dictionary<char, int> _unicodeToZscii;

    /// <summary>
    /// Creates a ZSCII encoder using the default extra characters table.
    /// </summary>
    public ZsciiEncoder() : this(null, null)
    {
    }

    /// <summary>
    /// Creates a ZSCII encoder, optionally loading a Unicode translation
    /// table from story file memory.
    /// </summary>
    /// <param name="memory">
    /// Story file memory for reading the translation table, or null
    /// to use defaults.
    /// </param>
    /// <param name="headerExtension">
    /// Parsed header extension table (V5+), or null if no extension
    /// table is present.
    /// </param>
    public ZsciiEncoder(Memory? memory, HeaderExtension? headerExtension)
    {
        _extraCharacters = LoadExtraCharacters(memory, headerExtension);
        _unicodeToZscii = BuildReverseLookup(_extraCharacters);
    }

    /// <summary>
    /// Converts a ZSCII code to its Unicode character representation.
    /// </summary>
    /// <param name="zsciiCode">ZSCII character code (0–1023).</param>
    /// <returns>
    /// The Unicode character, or null if the code is undefined, a
    /// control code, or outside the valid range.
    /// </returns>
    public char? ZsciiToUnicode(int zsciiCode)
    {
        // ZSpec S3.8 — ZSCII 32–126: direct ASCII mapping.
        if (zsciiCode >= 32 && zsciiCode <= 126)
        {
            char c = (char)zsciiCode;

            // ZSpec11 "Character set" — $27 (39) = right-single-quote/apostrophe.
            if (zsciiCode == 0x27)
                return '\u2019';

            // ZSpec11 "Character set" — $60 (96) = left-single-quote, not grave accent.
            if (zsciiCode == 0x60)
                return '\u2018';

            return c;
        }

        // ZSpec S3.8 — ZSCII 0: null (used as string terminator in some contexts).
        if (zsciiCode == 0)
            return null;

        // ZSpec S3.8 — ZSCII 13: newline.
        if (zsciiCode == 13)
            return '\n';

        // ZSpec S3.8.5 — ZSCII 155–251: extra characters.
        if (zsciiCode >= 155 && zsciiCode <= 251)
        {
            int index = zsciiCode - 155;
            if (index < _extraCharacters.Length)
                return _extraCharacters[index];
            return null;
        }

        // Everything else (1–12, 14–31, 127–154, 252+) is undefined.
        return null;
    }

    /// <summary>
    /// Converts a Unicode character to its ZSCII code.
    /// </summary>
    /// <param name="unicodeChar">The Unicode character to convert.</param>
    /// <returns>
    /// The ZSCII code, or -1 if the character has no ZSCII representation.
    /// </returns>
    public int UnicodeToZscii(char unicodeChar)
    {
        // Newline → ZSCII 13 (before control code check — \n is U+000A).
        if (unicodeChar == '\n')
            return 13;

        // ZSpec11 "Unicode" — Reject control codes.
        if (IsControlCode(unicodeChar))
            return -1;

        // ZSpec11 "Character set" — Curly quotes map to $27/$60.
        if (unicodeChar == '\u2019') // U+2019 right single quote
            return 0x27;
        if (unicodeChar == '\u2018') // U+2018 left single quote
            return 0x60;

        // ASCII range (32–126) maps directly.
        if (unicodeChar >= 32 && unicodeChar <= 126)
            return unicodeChar;

        // Extra characters: reverse lookup.
        if (_unicodeToZscii.TryGetValue(unicodeChar, out int zsciiCode))
            return zsciiCode;

        return -1;
    }

    /// <summary>
    /// Returns true if the Unicode code point is a control code that
    /// must not be used in Z-Machine text.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Unicode" — U+0000–U+001F and U+007F–U+009F are
    /// control codes and must not be used.
    /// </remarks>
    public static bool IsControlCode(char c)
    {
        return c <= '\u001F' || (c >= '\u007F' && c <= '\u009F');
    }

    /// <summary>
    /// Returns true if the Unicode code point is within the Basic
    /// Multilingual Plane (U+0000–U+FFFF). The Z-Machine does not
    /// support characters outside this range.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Unicode" — No access to non-BMP characters.
    /// </remarks>
    public static bool IsBmpCodePoint(int codePoint)
    {
        return codePoint >= 0 && codePoint <= 0xFFFF;
    }

    /// <summary>
    /// Loads the extra characters table. If a Unicode translation table
    /// address is present in the header extension, reads custom entries
    /// from memory; otherwise uses the default table.
    /// </summary>
    /// <remarks>
    /// ZSpec S3.8.5.1 — The Unicode translation table begins with a
    /// count byte, followed by that many 16-bit Unicode code points.
    /// These replace the default extras starting at ZSCII 155.
    /// </remarks>
    private static char[] LoadExtraCharacters(Memory? memory, HeaderExtension? headerExtension)
    {
        if (memory == null || headerExtension == null || headerExtension.UnicodeTableAddress == 0)
            return DefaultExtraCharacters;

        int address = headerExtension.UnicodeTableAddress;
        int count = memory.ReadByte(address);
        var table = new char[count];

        for (int i = 0; i < count; i++)
        {
            ushort codePoint = memory.ReadWord(address + 1 + i * 2);

            // ZSpec11 "Unicode" — Reject control codes and non-BMP.
            if (IsControlCode((char)codePoint) || !IsBmpCodePoint(codePoint))
                table[i] = '?';
            else
                table[i] = (char)codePoint;
        }

        return table;
    }

    /// <summary>
    /// Builds a reverse lookup dictionary from Unicode characters to
    /// their ZSCII codes in the extra characters range (155+).
    /// </summary>
    private static Dictionary<char, int> BuildReverseLookup(char[] extraCharacters)
    {
        var lookup = new Dictionary<char, int>(extraCharacters.Length);
        for (int i = 0; i < extraCharacters.Length; i++)
        {
            char c = extraCharacters[i];
            // First occurrence wins (in case of duplicates in a custom table).
            if (!lookup.ContainsKey(c))
                lookup[c] = 155 + i;
        }
        return lookup;
    }
}
