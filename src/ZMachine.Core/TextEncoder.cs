namespace ZMachine.Core;

/// <summary>
/// Encodes text into Z-character form for dictionary lookup. Used by
/// <c>@tokenise</c> and <c>@read</c> to convert player input into the
/// packed Z-character format that matches dictionary entries.
/// </summary>
/// <remarks>
/// ZSpec S3.7 — Encoding text: try A0 first, then A1 (shift 4), then
/// A2 (shift 5). If not found in any alphabet: A2-shift + Z-char 6 +
/// two 5-bit ZSCII halves.
/// ZSpec11 "Encoded text" — V1-2: use shift-lock (4/5) when next two
/// characters share a non-A0 alphabet; truncation of incomplete
/// multi-Z-char constructions leaves end-bit unset.
/// </remarks>
public class TextEncoder
{
    private static readonly char[] DefaultA0 =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    private static readonly char[] DefaultA1 =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    private static readonly char[] DefaultA2 =
        " \n0123456789.,!?_#'\"/\\-:()".ToCharArray();

    private static readonly char[] V1A2 =
        " 0123456789.,!?_#'\"/\\<-:()".ToCharArray();

    private readonly char[] _a0;
    private readonly char[] _a1;
    private readonly char[] _a2;
    private readonly int _version;

    /// <summary>
    /// Creates a text encoder for the given Z-Machine version.
    /// </summary>
    /// <param name="version">Z-Machine version (1-8).</param>
    /// <param name="alphabetTableAddress">
    /// Address of a custom alphabet table (V5+), or 0 for defaults.
    /// </param>
    /// <param name="memory">
    /// Story file memory for reading custom alphabets, or null.
    /// </param>
    public TextEncoder(int version, int alphabetTableAddress = 0, Memory? memory = null)
    {
        _version = version;

        if (alphabetTableAddress > 0 && memory != null)
        {
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
    /// Encodes a text string into packed Z-character bytes for dictionary
    /// lookup. V1-3: 4 bytes (6 Z-chars, 2 words). V4+: 6 bytes (9 Z-chars,
    /// 3 words).
    /// </summary>
    /// <param name="text">The text to encode (typically a single word).</param>
    /// <returns>
    /// The encoded bytes with the end-bit set on the last word, unless
    /// V1-2 truncation leaves an incomplete multi-Z-char construction.
    /// </returns>
    public byte[] EncodeForDictionary(string text)
    {
        int maxZChars = _version <= 3 ? 6 : 9;
        var zchars = EncodeToZChars(text, maxZChars);

        // Pack into 16-bit words.
        int wordCount = _version <= 3 ? 2 : 3;
        byte[] result = new byte[wordCount * 2];

        bool setEndBit = true;

        // ZSpec11 "Encoded text" — V1-2: if truncation leaves a multi-Z-char
        // construction incomplete, the end-bit of the last word is NOT set.
        if (_version <= 2)
            setEndBit = !IsTruncatedIncomplete(text, maxZChars);

        for (int w = 0; w < wordCount; w++)
        {
            int z0 = zchars[w * 3];
            int z1 = zchars[w * 3 + 1];
            int z2 = zchars[w * 3 + 2];
            int word = (z0 << 10) | (z1 << 5) | z2;

            if (w == wordCount - 1 && setEndBit)
                word |= 0x8000;

            result[w * 2] = (byte)(word >> 8);
            result[w * 2 + 1] = (byte)(word & 0xFF);
        }

        return result;
    }

    /// <summary>
    /// Encodes text into a list of Z-characters, handling V1-2 shift-lock
    /// state across characters. Pads or truncates to exactly maxZChars.
    /// </summary>
    private List<byte> EncodeToZChars(string text, int maxZChars)
    {
        var zchars = new List<byte>();

        // V1-2 shift-lock: the currently locked alphabet (-1 = A0 default).
        int lockedAlphabet = -1;

        for (int i = 0; i < text.Length && zchars.Count < maxZChars; i++)
        {
            char c = text[i];

            if (c == ' ')
            {
                zchars.Add(0);
                continue;
            }

            // Determine which alphabet this character is in.
            int charAlphabet = -1;
            int charIndex = -1;

            int idx = FindInAlphabet(_a0, c);
            if (idx >= 0)
            {
                charAlphabet = 0;
                charIndex = idx;
            }
            else
            {
                idx = FindInAlphabet(_a1, c);
                if (idx >= 0)
                {
                    charAlphabet = 1;
                    charIndex = idx;
                }
                else
                {
                    idx = FindInAlphabet(_a2, c);
                    if (idx >= 0 && idx != 0)
                    {
                        charAlphabet = 2;
                        charIndex = idx;
                    }
                }
            }

            if (charAlphabet < 0)
            {
                // Not in any alphabet — use 10-bit ZSCII escape.
                // If we're locked to a non-A0 alphabet, we need to
                // shift back to A0 first (then shift to A2 for the escape).
                // Actually, the ZSCII escape is always: shift-to-A2 + zc6 + hi + lo.
                EmitShiftIfNeeded(zchars, 2, lockedAlphabet, text, i, ref lockedAlphabet);
                zchars.Add(6);
                int zsciiCode = (int)c;
                zchars.Add((byte)((zsciiCode >> 5) & 0x1F));
                zchars.Add((byte)(zsciiCode & 0x1F));
                continue;
            }

            int currentBase = lockedAlphabet >= 0 ? lockedAlphabet : 0;

            if (charAlphabet == currentBase)
            {
                // Already in the right alphabet — emit directly.
                zchars.Add((byte)(charIndex + 6));
            }
            else
            {
                // Need to shift to this alphabet.
                EmitShiftIfNeeded(zchars, charAlphabet, lockedAlphabet, text, i, ref lockedAlphabet);
                zchars.Add((byte)(charIndex + 6));
            }
        }

        // Pad with Z-char 5.
        while (zchars.Count < maxZChars)
            zchars.Add(5);

        // Truncate if EncodeChar overshot.
        if (zchars.Count > maxZChars)
            zchars.RemoveRange(maxZChars, zchars.Count - maxZChars);

        return zchars;
    }

    /// <summary>
    /// Emits the appropriate shift Z-character(s) to switch from the
    /// current alphabet to the target alphabet. In V1-2, may emit a
    /// shift-lock that updates the locked state.
    /// </summary>
    private void EmitShiftIfNeeded(
        List<byte> zchars, int targetAlphabet, int lockedAlphabet,
        string text, int textIndex, ref int lockedAlphabetRef)
    {
        int currentBase = lockedAlphabet >= 0 ? lockedAlphabet : 0;

        // Already in the target alphabet — no shift z-char needed.
        if (currentBase == targetAlphabet)
            return;

        if (_version <= 2)
        {
            // V1-2: check if next character is also in targetAlphabet.
            // If so, use shift-lock instead of single-shift.
            bool useShiftLock = false;
            if (textIndex + 1 < text.Length)
            {
                char nextChar = text[textIndex + 1];
                int nextAlphabet = GetAlphabetFor(nextChar);
                if (nextAlphabet == targetAlphabet)
                    useShiftLock = true;
            }

            if (useShiftLock)
            {
                // Shift-lock: z-char 4 = shift-up, 5 = shift-down.
                // From A0: 4→A1, 5→A2. From A1: 4→A2, 5→A0. From A2: 4→A0, 5→A1.
                byte lockChar = GetV12ShiftChar(currentBase, targetAlphabet, isLock: true);
                zchars.Add(lockChar);
                lockedAlphabetRef = targetAlphabet;
            }
            else
            {
                // Single-shift: z-char 2 = shift-up, 3 = shift-down.
                byte shiftChar = GetV12ShiftChar(currentBase, targetAlphabet, isLock: false);
                zchars.Add(shiftChar);
                // No lock state change.
            }
        }
        else
        {
            // V3+: always single-shift. 4 = A1, 5 = A2.
            // If currently locked to a non-A0 alphabet (shouldn't happen in V3+,
            // but handle gracefully), we shift from A0 since V3+ has no locks.
            zchars.Add(targetAlphabet == 1 ? (byte)4 : (byte)5);
        }
    }

    /// <summary>
    /// Returns the V1-2 shift z-character to move from the current alphabet
    /// to the target. Shift-up (2 or 4) cycles A0→A1→A2→A0. Shift-down
    /// (3 or 5) cycles A0→A2→A1→A0.
    /// </summary>
    private static byte GetV12ShiftChar(int from, int to, bool isLock)
    {
        // "Up" = +1 mod 3, "Down" = +2 mod 3.
        // 2/4 = shift-up (single/lock), 3/5 = shift-down (single/lock).
        int delta = (to - from + 3) % 3;
        if (delta == 1)
            return isLock ? (byte)4 : (byte)2;
        else
            return isLock ? (byte)5 : (byte)3;
    }

    /// <summary>
    /// Returns which alphabet (0, 1, 2) a character belongs to, or -1
    /// if not found (requires ZSCII escape).
    /// </summary>
    private int GetAlphabetFor(char c)
    {
        if (FindInAlphabet(_a0, c) >= 0) return 0;
        if (FindInAlphabet(_a1, c) >= 0) return 1;
        int a2idx = FindInAlphabet(_a2, c);
        if (a2idx >= 0 && a2idx != 0) return 2;
        return -1;
    }

    /// <summary>
    /// Determines whether encoding the given text and truncating to
    /// maxZChars would leave an incomplete multi-Z-char construction.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Encoded text" — V1-2 only: if truncation to 6 Z-chars
    /// leaves a shift or ZSCII escape without its payload, the end-bit
    /// is not set.
    /// </remarks>
    private bool IsTruncatedIncomplete(string text, int maxZChars)
    {
        // Encode without truncation or padding to get the raw z-char sequence.
        var zchars = new List<byte>();
        int lockedAlphabet = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ' ') { zchars.Add(0); continue; }

            int charAlphabet = GetAlphabetFor(c);
            int currentBase = lockedAlphabet >= 0 ? lockedAlphabet : 0;

            if (charAlphabet < 0)
            {
                EmitShiftIfNeeded(zchars, 2, lockedAlphabet, text, i, ref lockedAlphabet);
                zchars.Add(6);
                zchars.Add((byte)(((int)c >> 5) & 0x1F));
                zchars.Add((byte)((int)c & 0x1F));
            }
            else if (charAlphabet != currentBase)
            {
                EmitShiftIfNeeded(zchars, charAlphabet, lockedAlphabet, text, i, ref lockedAlphabet);
                int idx = FindInAlphabetByNumber(charAlphabet, c);
                zchars.Add((byte)(idx + 6));
            }
            else
            {
                int idx = FindInAlphabetByNumber(charAlphabet, c);
                zchars.Add((byte)(idx + 6));
            }
        }

        if (zchars.Count <= maxZChars)
            return false;

        // Walk through z-chars to find if truncation at maxZChars
        // breaks a multi-z-char construction.
        int pos = 0;
        while (pos < maxZChars)
        {
            byte zc = zchars[pos];

            if (zc == 0 || zc >= 6)
            {
                pos++;
                continue;
            }

            // Shift: 2, 3, 4, 5
            if (zc >= 2 && zc <= 5)
            {
                pos++; // past the shift

                if (pos < zchars.Count && zchars[pos] == 6)
                    pos += 3; // ZSCII escape: 6 + hi + lo
                else
                    pos++; // shifted char

                if (pos > maxZChars)
                    return true;

                continue;
            }

            pos++;
        }

        return false;
    }

    /// <summary>
    /// Finds a character in the alphabet identified by number (0, 1, 2).
    /// </summary>
    private int FindInAlphabetByNumber(int alphabetNum, char c)
    {
        char[] table = alphabetNum switch
        {
            0 => _a0,
            1 => _a1,
            2 => _a2,
            _ => _a0,
        };
        return FindInAlphabet(table, c);
    }

    /// <summary>
    /// Finds a character in an alphabet table, returning its index
    /// (0-25) or -1 if not found.
    /// </summary>
    private static int FindInAlphabet(char[] alphabet, char c)
    {
        for (int i = 0; i < alphabet.Length; i++)
        {
            if (alphabet[i] == c)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Reads a 26-byte custom alphabet from memory.
    /// </summary>
    private static char[] ReadCustomAlphabet(Memory memory, int address)
    {
        var alphabet = new char[26];
        for (int i = 0; i < 26; i++)
            alphabet[i] = (char)memory.ReadByte(address + i);
        return alphabet;
    }
}
