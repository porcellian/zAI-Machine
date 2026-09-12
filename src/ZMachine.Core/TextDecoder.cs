namespace ZMachine.Core;

using System.Text;

/// <summary>
/// Decodes Z-Machine encoded text (Z-strings) into readable strings.
/// Z-text packs three 5-bit Z-characters into each 16-bit word. Z-chars
/// are mapped through three alphabet tables (A0, A1, A2) to produce
/// ZSCII output characters.
/// </summary>
/// <remarks>
/// ZSpec S3.1–S3.6 — Encoded text format, alphabet tables, shifts.
/// ZSpec S15 — Default alphabet tables.
/// ZSpec11 "Encoded text" — V1–2 shift-lock semantics, V3+ single-shift.
/// </remarks>
public class TextDecoder
{
    /// <summary>
    /// Default A0 alphabet: lowercase letters.
    /// Index 0–25 maps Z-chars 6–31 to output characters.
    /// </summary>
    private static readonly char[] DefaultA0 =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    /// <summary>
    /// Default A1 alphabet: uppercase letters.
    /// Index 0–25 maps Z-chars 6–31 to output characters.
    /// </summary>
    private static readonly char[] DefaultA1 =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    /// <summary>
    /// Default A2 alphabet: punctuation and digits. Z-char 6 in A2 is the
    /// 10-bit ZSCII escape, so slot 0 is unused as a direct mapping.
    /// Z-chars 7–31 map to slots 1–25.
    /// </summary>
    /// <remarks>
    /// ZSpec S3.5.3 — A2 char 6 means "the next two Z-characters specify
    /// a ten-bit ZSCII character code."
    /// </remarks>
    private static readonly char[] DefaultA2 =
        " \n0123456789.,!?_#'\"/\\-:()".ToCharArray();

    /// <summary>
    /// V1 A2 alphabet differs: slot 0 is unused (ZSCII escape), slot 1
    /// is newline, and the rest are digits and punctuation in a different
    /// order than V2+.
    /// </summary>
    /// <remarks>
    /// ZSpec S3.5.1 — V1 uses a different A2 table.
    /// </remarks>
    private static readonly char[] V1A2 =
        " 0123456789.,!?_#'\"/\\<-:()".ToCharArray();

    private readonly Memory _memory;
    private readonly int _version;
    private readonly char[] _a0;
    private readonly char[] _a1;
    private readonly char[] _a2;
    private readonly int _abbreviationTableAddress;

    // ZSpec S3.3 — 96 abbreviation slots (3 banks × 32 entries).
    // Cached on first use to avoid re-decoding on every reference.
    private readonly string?[] _abbreviationCache = new string?[96];

    /// <summary>
    /// Creates a text decoder for the given story file.
    /// </summary>
    /// <param name="memory">The story file memory.</param>
    /// <param name="version">Z-Machine version (1–8).</param>
    /// <param name="abbreviationTableAddress">
    /// Address of the abbreviation table (header word $18).
    /// Zero if no abbreviations (V1).
    /// </param>
    /// <param name="alphabetTableAddress">
    /// Address of a custom alphabet table (header word $34, V5+ only).
    /// Zero means use default alphabets.
    /// </param>
    public TextDecoder(Memory memory, int version, int abbreviationTableAddress, int alphabetTableAddress = 0)
    {
        _memory = memory;
        _version = version;
        _abbreviationTableAddress = abbreviationTableAddress;

        if (alphabetTableAddress > 0)
        {
            // ZSpec S3.5.5 — V5+ custom alphabet: 78 bytes = 3×26 characters.
            // Each byte is a ZSCII code for the corresponding position.
            _a0 = ReadCustomAlphabet(memory, alphabetTableAddress);
            _a1 = ReadCustomAlphabet(memory, alphabetTableAddress + 26);
            _a2 = ReadCustomAlphabet(memory, alphabetTableAddress + 52);
        }
        else
        {
            _a0 = DefaultA0;
            _a1 = DefaultA1;
            _a2 = _version == 1 ? V1A2 : DefaultA2;
        }
    }

    /// <summary>
    /// Decodes a Z-string starting at the given byte address.
    /// </summary>
    /// <param name="address">Byte address of the first word of the Z-string.</param>
    /// <returns>
    /// The decoded string and the number of bytes consumed (always a
    /// multiple of 2).
    /// </returns>
    public (string Text, int ByteLength) DecodeZString(int address)
    {
        return DecodeZStringInternal(address, isAbbreviation: false);
    }

    /// <summary>
    /// Core decoding loop shared by top-level strings and abbreviation
    /// expansion.
    /// </summary>
    /// <param name="address">Starting byte address.</param>
    /// <param name="isAbbreviation">
    /// True when expanding an abbreviation — prevents recursive expansion
    /// (ZSpec S3.3: "An abbreviation string must not itself use
    /// abbreviations").
    /// </param>
    private (string Text, int ByteLength) DecodeZStringInternal(int address, bool isAbbreviation)
    {
        var sb = new StringBuilder();
        var zchars = new List<byte>();
        int pos = address;

        // ZSpec S3.1 — Read 16-bit words until the top bit is set.
        bool lastWord;
        do
        {
            ushort word = _memory.ReadWord(pos);
            pos += 2;
            lastWord = (word & 0x8000) != 0;

            // ZSpec S3.2 — Three 5-bit Z-characters packed per word:
            // bits 14–10 = first, 9–5 = second, 4–0 = third.
            zchars.Add((byte)((word >> 10) & 0x1F));
            zchars.Add((byte)((word >> 5) & 0x1F));
            zchars.Add((byte)(word & 0x1F));
        } while (!lastWord);

        int byteLength = pos - address;
        int currentAlphabet = 0;  // 0=A0, 1=A1, 2=A2
        // V1–2 shift-lock: the alphabet to revert to after each output
        // character. -1 means no lock active (revert to A0).
        int lockedAlphabet = -1;

        for (int i = 0; i < zchars.Count; i++)
        {
            byte zc = zchars[i];

            // ZSpec S3.4 — Z-char 0 is always a space.
            if (zc == 0)
            {
                sb.Append(' ');
                RevertAlphabet(ref currentAlphabet, lockedAlphabet);
                continue;
            }

            // ZSpec S3.3 — Abbreviation triggers.
            if (IsAbbreviationTrigger(zc))
            {
                if (isAbbreviation)
                    throw new InvalidOperationException(
                        "Recursive abbreviation expansion is illegal (ZSpec S3.3).");

                if (i + 1 >= zchars.Count) break;
                byte next = zchars[++i];
                int entryIndex = (zc - 1) * 32 + next;
                string abbrText = _abbreviationCache[entryIndex]
                    ?? CacheAbbreviation(entryIndex);
                sb.Append(abbrText);
                RevertAlphabet(ref currentAlphabet, lockedAlphabet);
                continue;
            }

            // ZSpec S3.4 — Shift characters.
            // V1–2: z-chars 2,3 = single-shift; 4,5 = shift-lock.
            // V3+:  z-chars 4,5 = single-shift (2,3 are abbreviation triggers).
            if (_version <= 2 && (zc == 2 || zc == 3))
            {
                // Single-shift from the current base (locked or A0).
                int baseAlphabet = lockedAlphabet >= 0 ? lockedAlphabet : 0;
                if (zc == 2)
                    currentAlphabet = (baseAlphabet + 1) % 3;
                else
                    currentAlphabet = (baseAlphabet + 2) % 3;
                continue;
            }

            if (zc == 4 || zc == 5)
            {
                if (_version <= 2)
                {
                    // ZSpec11 "Encoded text" — V1–2: 4/5 are shift-locks.
                    int baseAlphabet = lockedAlphabet >= 0 ? lockedAlphabet : 0;
                    if (zc == 4)
                        currentAlphabet = (baseAlphabet + 1) % 3;
                    else
                        currentAlphabet = (baseAlphabet + 2) % 3;
                    lockedAlphabet = currentAlphabet;
                }
                else
                {
                    // V3+ — single shift only
                    if (zc == 4)
                        currentAlphabet = 1; // A1
                    else
                        currentAlphabet = 2; // A2
                }
                continue;
            }

            // ZSpec S3.5.3 — A2 Z-char 6: 10-bit ZSCII literal.
            if (currentAlphabet == 2 && zc == 6)
            {
                if (i + 2 >= zchars.Count) break;
                byte hi = zchars[++i];
                byte lo = zchars[++i];
                int zsciiCode = (hi << 5) | lo;
                sb.Append((char)zsciiCode);
                RevertAlphabet(ref currentAlphabet, lockedAlphabet);
                continue;
            }

            // ZSpec S3.5 — Map Z-char 6–31 through the current alphabet.
            if (zc >= 6 && zc <= 31)
            {
                char[] table = currentAlphabet switch
                {
                    0 => _a0,
                    1 => _a1,
                    2 => _a2,
                    _ => _a0,
                };
                int index = zc - 6;
                sb.Append(table[index]);
                RevertAlphabet(ref currentAlphabet, lockedAlphabet);
                continue;
            }

            if (_version == 1 && zc == 1)
            {
                sb.Append('\n');
                RevertAlphabet(ref currentAlphabet, lockedAlphabet);
                continue;
            }
        }

        return (sb.ToString(), byteLength);
    }

    /// <summary>
    /// After outputting a character, revert to the locked alphabet (V1–2
    /// shift-lock) or to A0 (no lock / V3+).
    /// </summary>
    private static void RevertAlphabet(ref int currentAlphabet, int lockedAlphabet)
    {
        currentAlphabet = lockedAlphabet >= 0 ? lockedAlphabet : 0;
    }

    /// <summary>
    /// Returns true if the given Z-character triggers abbreviation lookup.
    /// </summary>
    private bool IsAbbreviationTrigger(byte zc)
    {
        // ZSpec S3.3 — V1: no abbreviations. V2: z-char 1 only. V3+: z-chars 1, 2, 3.
        if (_version == 1) return false;
        if (_version == 2) return zc == 1;
        return zc >= 1 && zc <= 3;
    }

    /// <summary>
    /// Decodes and caches an abbreviation by its slot index (0–95).
    /// </summary>
    private string CacheAbbreviation(int entryIndex)
    {
        int tableEntry = _abbreviationTableAddress + entryIndex * 2;
        int abbrAddress = _memory.ReadWord(tableEntry) * 2;
        var (text, _) = DecodeZStringInternal(abbrAddress, isAbbreviation: true);
        _abbreviationCache[entryIndex] = text;
        return text;
    }

    /// <summary>
    /// Clears the abbreviation cache. Call after @restart to pick up
    /// any changes to the abbreviation table in dynamic memory.
    /// </summary>
    public void ClearAbbreviationCache()
    {
        Array.Clear(_abbreviationCache);
    }

    /// <summary>
    /// Reads a 26-byte custom alphabet from memory. Each byte is a ZSCII
    /// code mapped to a Unicode character (assuming ZSCII = ASCII for the
    /// printable range).
    /// </summary>
    private static char[] ReadCustomAlphabet(Memory memory, int address)
    {
        var alphabet = new char[26];
        for (int i = 0; i < 26; i++)
            alphabet[i] = (char)memory.ReadByte(address + i);
        return alphabet;
    }
}
