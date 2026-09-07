namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for TextEncoder — encoding text into packed Z-characters for
/// dictionary lookup. Verifies encoding rules, padding, shift selection,
/// 10-bit ZSCII escapes, V1-2 shift-lock and truncation, and custom
/// alphabet support. Includes verification against the zork1.z3 dictionary.
/// </summary>
public class TextEncoderTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region Basic V3 Encoding

    [Fact]
    public void EncodeForDictionary_Mailbox_MatchesZork1Dictionary()
    {
        // "mailbox" in zork1.z3 dictionary at 0x453F: 48 CE C4 F4
        // m=18, a=6, i=14, l=17, b=7, o=20 (all A0, z-chars = index+6)
        // Truncated to 6 z-chars: m, a, i, l, b, o
        // word1: (18<<10)|(6<<5)|14 = 0x48CE
        // word2: (17<<10)|(7<<5)|20 = 0x44F4 | 0x8000 = 0xC4F4
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("mailbox");

        Assert.Equal(4, encoded.Length);
        Assert.Equal(new byte[] { 0x48, 0xCE, 0xC4, 0xF4 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_Hello_MatchesZork1Dictionary()
    {
        // "hello" in zork1.z3: 35 51 C6 85
        // h=13, e=10, l=17, l=17, o=20, pad=5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("hello");

        Assert.Equal(4, encoded.Length);
        Assert.Equal(new byte[] { 0x35, 0x51, 0xC6, 0x85 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_North_MatchesZork1Dictionary()
    {
        // "north" in zork1.z3: 4E 97 E5 A5
        // n=19, o=20, r=23, t=25, h=13, pad=5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("north");

        Assert.Equal(4, encoded.Length);
        Assert.Equal(new byte[] { 0x4E, 0x97, 0xE5, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_ShortWord_PaddedWithZChar5()
    {
        // "hi" = h(13), i(14), pad, pad, pad, pad
        // word1: (13<<10)|(14<<5)|5 = 0x35C5
        // word2: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("hi");

        Assert.Equal(4, encoded.Length);
        Assert.Equal(new byte[] { 0x35, 0xC5, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_LongWord_TruncatedTo6ZChars()
    {
        // "northeast" truncates to "northe" in V3 (6 z-chars)
        // Same as zork1: 4E 97 E5 AA
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("northeast");

        // "northe": n=19, o=20, r=23, t=25, h=13, e=10
        Assert.Equal(new byte[] { 0x4E, 0x97, 0xE5, 0xAA }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_ProducesFourBytes()
    {
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("a");
        Assert.Equal(4, encoded.Length);
    }

    [Fact]
    public void EncodeForDictionary_V3_EndBitSetOnLastWord()
    {
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("test");

        // Last word should have top bit set
        int lastWord = (encoded[2] << 8) | encoded[3];
        Assert.True((lastWord & 0x8000) != 0, "End bit should be set on last word");
    }

    #endregion

    #region V4+ Encoding (6 bytes / 9 Z-chars)

    [Fact]
    public void EncodeForDictionary_V5_ProducesSixBytes()
    {
        var encoder = new TextEncoder(5);
        byte[] encoded = encoder.EncodeForDictionary("test");
        Assert.Equal(6, encoded.Length);
    }

    [Fact]
    public void EncodeForDictionary_V5_NineZCharsCapacity()
    {
        // "abcdefghi" = exactly 9 z-chars, should fit without truncation
        // a=6, b=7, c=8, d=9, e=10, f=11, g=12, h=13, i=14
        var encoder = new TextEncoder(5);
        byte[] encoded = encoder.EncodeForDictionary("abcdefghi");

        // word1: (6<<10)|(7<<5)|8 = 0x18E8
        // word2: (9<<10)|(10<<5)|11 = 0x254B
        // word3: (12<<10)|(13<<5)|14 = 0x31AE | 0x8000 = 0xB1AE
        Assert.Equal(new byte[] { 0x18, 0xE8, 0x25, 0x4B, 0xB1, 0xAE }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V5_LongWordTruncatedTo9ZChars()
    {
        // "abcdefghij" has 10 A0 z-chars, truncated to 9
        var encoder = new TextEncoder(5);
        byte[] same = encoder.EncodeForDictionary("abcdefghi");
        byte[] longer = encoder.EncodeForDictionary("abcdefghij");
        Assert.Equal(same, longer);
    }

    [Fact]
    public void EncodeForDictionary_V5_EndBitOnThirdWord()
    {
        var encoder = new TextEncoder(5);
        byte[] encoded = encoder.EncodeForDictionary("test");

        // First two words should NOT have top bit set
        int word1 = (encoded[0] << 8) | encoded[1];
        int word2 = (encoded[2] << 8) | encoded[3];
        int word3 = (encoded[4] << 8) | encoded[5];
        Assert.False((word1 & 0x8000) != 0, "Word 1 should not have end bit");
        Assert.False((word2 & 0x8000) != 0, "Word 2 should not have end bit");
        Assert.True((word3 & 0x8000) != 0, "Word 3 should have end bit");
    }

    #endregion

    #region Shift Characters (A1 and A2)

    [Fact]
    public void EncodeForDictionary_V3_UppercaseLetter_UsesShift4()
    {
        // "A" = shift4 + zc6, then 4 padding z-chars
        // shift4=4, A in A1 index 0 → zc 6
        // word1: (4<<10)|(6<<5)|5 = 0x10C5
        // word2: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("A");

        Assert.Equal(new byte[] { 0x10, 0xC5, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_Digit_UsesShift5()
    {
        // "0" is in A2 at index 2 → z-char 8
        // shift5=5, zc8, then 4 padding
        // word1: (5<<10)|(8<<5)|5 = 0x1505
        // word2: 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("0");

        Assert.Equal(new byte[] { 0x15, 0x05, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_Comma_UsesShift5()
    {
        // "," is in A2 at index 13 → z-char 19
        // shift5=5, zc19, pad×4
        // word1: (5<<10)|(19<<5)|5 = 5120+608+5 = 5733 = 0x1665
        // word2: 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary(",");

        Assert.Equal(new byte[] { 0x16, 0x65, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_H2O_MatchesZork1()
    {
        // "h2o" in zork1: 34 AA D0 A5
        // h=13, shift5, 2→zc10(A2 index 4), o=20, pad, pad
        // Wait, '2' in A2: A2 = " \n0123456789.,!?_#'"/-:()"
        // '0'=index 2, '1'=3, '2'=4 → z-char 4+6=10
        // h=13, shift5(5), '2'=10, o=20, pad(5), pad(5)
        // word1: (13<<10)|(5<<5)|10 = 13312+160+10 = 13482 = 0x34AA
        // word2: (20<<10)|(5<<5)|5 = 20480+160+5 = 20645 = 0x50A5 | 0x8000 = 0xD0A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("h2o");

        Assert.Equal(new byte[] { 0x34, 0xAA, 0xD0, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_Space_IsZChar0()
    {
        // "a b" = a(6), space(0), b(7), pad, pad, pad
        // word1: (6<<10)|(0<<5)|7 = 6144+0+7 = 6151 = 0x1807
        // word2: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("a b");

        Assert.Equal(new byte[] { 0x18, 0x07, 0x94, 0xA5 }, encoded);
    }

    #endregion

    #region 10-bit ZSCII Escape

    [Fact]
    public void EncodeForDictionary_V3_UnknownChar_Uses10BitEscape()
    {
        // A character not in any alphabet requires the ZSCII escape:
        // shift5 + z-char 6 + high 5 bits + low 5 bits = 4 z-chars.
        // Character '@' (ZSCII/ASCII 64 = 0x40)
        // high 5 bits: 64 >> 5 = 2, low 5 bits: 64 & 0x1F = 0
        // z-chars: 5, 6, 2, 0, pad(5), pad(5)
        // word1: (5<<10)|(6<<5)|2 = 5120+192+2 = 5314 = 0x14C2
        // word2: (0<<10)|(5<<5)|5 = 0+160+5 = 165 = 0x00A5 | 0x8000 = 0x80A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("@");

        Assert.Equal(new byte[] { 0x14, 0xC2, 0x80, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V5_CharWithEscapeAndLetters()
    {
        // "a@b" in V5:
        // a=6, shift5(5), 6, high=2, low=0, b=7, pad(5), pad(5), pad(5)
        // word1: (6<<10)|(5<<5)|6 = 6144+160+6 = 6310 = 0x18A6
        // word2: (2<<10)|(0<<5)|7 = 2048+0+7 = 2055 = 0x0807
        // word3: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(5);
        byte[] encoded = encoder.EncodeForDictionary("a@b");

        Assert.Equal(new byte[] { 0x18, 0xA6, 0x08, 0x07, 0x94, 0xA5 }, encoded);
    }

    #endregion

    #region V1-2 Shift-Lock

    [Fact]
    public void EncodeForDictionary_V2_ConsecutiveSameAlphabet_UsesShiftLock()
    {
        // "AB" in V2: both in A1. Next char 'B' is also A1, so use shift-lock (4).
        // After shift-lock, both A(=6) and B(=7) are in locked A1 — no second shift.
        // z-chars: 4, 6, 7, 5, 5, 5
        // word1: (4<<10)|(6<<5)|7 = 4096+192+7 = 4295 = 0x10C7
        // word2: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("AB");

        Assert.Equal(new byte[] { 0x10, 0xC7, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V2_SingleNonA0_UsesSingleShift()
    {
        // "aAb" in V2: 'a' is A0, 'A' is A1 but next char 'b' is A0 (not A1),
        // so use single-shift (2) for A1.
        // a=6, shift2(2), A=6, b=7, pad, pad
        // word1: (6<<10)|(2<<5)|6 = 6144+64+6 = 6214 = 0x1846
        // word2: (7<<10)|(5<<5)|5 = 7168+160+5 = 7333 = 0x1CA5 | 0x8000 = 0x9CA5
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("aAb");

        Assert.Equal(new byte[] { 0x18, 0x46, 0x9C, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V3_ConsecutiveSameAlphabet_StillUsesSingleShift()
    {
        // "AB" in V3: both A1, but V3 always uses single-shift (4).
        // shift4(4), A=6, shift4(4), B=7, pad, pad
        // word1: (4<<10)|(6<<5)|4 = 4096+192+4 = 4292 = 0x10C4
        // word2: (7<<10)|(5<<5)|5 = 7168+160+5 = 7333 = 0x1CA5 | 0x8000 = 0x9CA5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("AB");

        Assert.Equal(new byte[] { 0x10, 0xC4, 0x9C, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_V2_ConsecutiveA2_UsesShiftLock5()
    {
        // "12" in V2: both in A2. Next char is also A2, so use shift-lock (5).
        // In V2 A2: '1'=index 3 → zc 9, '2'=index 4 → zc 10
        // shift-lock5(5), '1'=9, '2'=10, pad, pad, pad
        // After shift-lock, the locked alphabet is A2 so '2' needs no shift.
        // word1: (5<<10)|(9<<5)|10 = 5120+288+10 = 5418 = 0x152A
        // word2: (5<<10)|(5<<5)|5 = 0x14A5 | 0x8000 = 0x94A5
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("12");

        Assert.Equal(new byte[] { 0x15, 0x2A, 0x94, 0xA5 }, encoded);
    }

    #endregion

    #region V1-2 Truncation (Incomplete Construction)

    [Fact]
    public void EncodeForDictionary_V2_TruncatedShift_EndBitNotSet()
    {
        // A word where truncation at 6 z-chars lands mid-shift.
        // "abcde0" in V2:
        // a=6, b=7, c=8, d=9, e=10, shift3(3) → 6 z-chars, but the '0' needs zc8 after shift
        // The shift at position 5 is incomplete → end-bit NOT set.
        // word1: (6<<10)|(7<<5)|8 = 0x18E8
        // word2: (9<<10)|(10<<5)|3 = 9216+320+3 = 9539 = 0x2543 (NO 0x8000)
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("abcde0");

        Assert.Equal(4, encoded.Length);
        int lastWord = (encoded[2] << 8) | encoded[3];
        Assert.False((lastWord & 0x8000) != 0,
            "End bit should NOT be set when truncation leaves incomplete shift");
    }

    [Fact]
    public void EncodeForDictionary_V2_CompleteTruncation_EndBitSet()
    {
        // "abcdef" = 6 A0 z-chars, no incomplete construction → end-bit set.
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("abcdef");

        int lastWord = (encoded[2] << 8) | encoded[3];
        Assert.True((lastWord & 0x8000) != 0,
            "End bit should be set when no construction is incomplete");
    }

    [Fact]
    public void EncodeForDictionary_V3_SameTruncation_EndBitAlwaysSet()
    {
        // V3+ always sets the end bit, even if truncation is incomplete.
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("abcde0");

        int lastWord = (encoded[2] << 8) | encoded[3];
        Assert.True((lastWord & 0x8000) != 0,
            "V3+ always sets end bit regardless of truncation");
    }

    #endregion

    #region Real Story File Verification

    [Fact]
    public void EncodeForDictionary_Zork1_VerifyMultipleEntries()
    {
        // Encode several words and compare against zork1.z3 dictionary entries.
        if (!File.Exists(Zork1Path))
            return; // Skip if zork1 not available

        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        var encoder = new TextEncoder(3);

        // Read dictionary entries for known words
        var expectedEntries = new Dictionary<string, int>
        {
            ["mailbox"] = 0x453F,
            ["hello"] = 0x42DE,
            ["north"] = 0x461F,
        };

        foreach (var (word, addr) in expectedEntries)
        {
            byte[] encoded = encoder.EncodeForDictionary(word);
            byte[] fromDict = new byte[4];
            for (int i = 0; i < 4; i++)
                fromDict[i] = memory.ReadByte(addr + i);

            Assert.Equal(fromDict, encoded);
        }
    }

    #endregion

    #region Custom Alphabet Tables

    [Fact]
    public void EncodeForDictionary_V5_CustomAlphabet()
    {
        // Create a synthetic V5 story with a custom alphabet where
        // A0 starts with "zyxw..." (reversed).
        byte[] data = new byte[0xA0];
        data[0] = 5; // version
        data[0x0E] = 0x00; data[0x0F] = 0xA0; // static base

        // Alphabet table at 0x40
        data[0x34] = 0x00; data[0x35] = 0x40;

        // Custom A0: reversed alphabet
        string customA0 = "zyxwvutsrqponmlkjihgfedcba";
        for (int i = 0; i < 26; i++)
            data[0x40 + i] = (byte)customA0[i];

        // Custom A1: same as default uppercase
        string customA1 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (int i = 0; i < 26; i++)
            data[0x40 + 26 + i] = (byte)customA1[i];

        // Custom A2: same as default
        string customA2 = " \n0123456789.,!?_#'\"/\\-:()";
        for (int i = 0; i < 26; i++)
            data[0x40 + 52 + i] = (byte)customA2[i];

        var memory = new Memory();
        memory.LoadStory(data);

        var encoder = new TextEncoder(5, 0x40, memory);

        // In the custom A0, 'z' is at index 0 → z-char 6
        // In default A0, 'z' is at index 25 → z-char 31
        byte[] encodedZ = encoder.EncodeForDictionary("z");

        // word1: (6<<10)|(5<<5)|5 = 6144+160+5 = 6309 = 0x18A5
        // word2: 0x94A5
        // word3: 0x94A5
        int word1 = (encodedZ[0] << 8) | encodedZ[1];
        int zchar0 = (word1 >> 10) & 0x1F;
        Assert.Equal(6, zchar0); // 'z' at index 0 in custom A0
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EncodeForDictionary_EmptyString_AllPadding()
    {
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("");

        // All z-char 5 padding
        // word1: (5<<10)|(5<<5)|5 = 0x14A5
        // word2: 0x14A5 | 0x8000 = 0x94A5
        Assert.Equal(new byte[] { 0x14, 0xA5, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_SingleChar_PaddedCorrectly()
    {
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("a");

        // a=6, pad×5
        // word1: (6<<10)|(5<<5)|5 = 6144+160+5 = 6309 = 0x18A5
        // word2: 0x14A5 | 0x8000 = 0x94A5
        Assert.Equal(new byte[] { 0x18, 0xA5, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void EncodeForDictionary_Newline_InA2()
    {
        // Newline is in A2 at index 1 → z-char 7
        // shift5(5), zc7, pad×4
        // word1: (5<<10)|(7<<5)|5 = 5120+224+5 = 5349 = 0x14E5
        // word2: 0x94A5
        var encoder = new TextEncoder(3);
        byte[] encoded = encoder.EncodeForDictionary("\n");

        Assert.Equal(new byte[] { 0x14, 0xE5, 0x94, 0xA5 }, encoded);
    }

    [Fact]
    public void V2_ZsciiEscape_WhenAlreadyLockedInA2_NoSpuriousShift()
    {
        // "12@": '1' and '2' shift-lock into A2, then '@' is not in any
        // alphabet so it uses the ZSCII escape. The escape must not emit
        // a shift z-char when already locked in A2.
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("12@");

        // Expected z-chars: [5, 9, 10, 6, hi(@=64), lo(@=64)]
        //   5 = shift-lock to A2
        //   9 = '1' in A2 (index 3 + 6)
        //  10 = '2' in A2 (index 4 + 6)
        //   6 = ZSCII escape introducer
        //   2 = 64 >> 5 = 2
        //   0 = 64 & 0x1F = 0
        // Packed into word 0: (5 << 10) | (9 << 5) | 10 = 0x1529 + end-bit → 0x952A
        // Word 1: (6 << 10) | (2 << 5) | 0 = 0x1840 + end-bit = 0x9840
        // Actually V2 has 2 words (4 bytes), end-bit semantics may vary.
        // The key assertion: decoding the result must not contain 'A' or
        // other garbage from a spurious shift to A1.

        // Simpler assertion: the encoded output should be exactly 4 bytes
        // and the z-char stream should NOT contain a shift-down (3) before
        // the escape marker (6).
        Assert.Equal(4, encoded.Length);

        // Unpack z-chars from the two words.
        int w0 = (encoded[0] << 8) | encoded[1];
        int w1 = (encoded[2] << 8) | encoded[3];
        byte[] zchars =
        [
            (byte)((w0 >> 10) & 0x1F),
            (byte)((w0 >> 5) & 0x1F),
            (byte)(w0 & 0x1F),
            (byte)((w1 >> 10) & 0x1F),
            (byte)((w1 >> 5) & 0x1F),
            (byte)(w1 & 0x1F),
        ];

        // z-chars[0] = 5 (shift-lock to A2)
        Assert.Equal(5, zchars[0]);
        // z-chars[1] = 9 ('1')
        Assert.Equal(9, zchars[1]);
        // z-chars[2] = 10 ('2')
        Assert.Equal(10, zchars[2]);
        // z-chars[3] = 6 (ZSCII escape), NOT 3 (shift-down)
        Assert.Equal(6, zchars[3]);
    }

    [Fact]
    public void V2_ShiftToA1_ZChar6IsLetterA_NotEscapeIntroducer()
    {
        // "abcdAxyz" — a,b,c,d in A0, then shift-up to A1, 'A' = A1
        // index 0 = z-char 6. Truncated to 6 z-chars the construction is
        // complete (shift + 1 char), so the end-bit must be set.
        var encoder = new TextEncoder(2);
        byte[] encoded = encoder.EncodeForDictionary("abcdAxyz");

        // Untruncated z-chars: [6,7,8,9, 2,6, 29,30,31]
        //   a=6, b=7, c=8, d=9 (A0 direct)
        //   2 = single-shift-up (A0→A1)
        //   6 = 'A' (A1 index 0)
        //   x=29, y=30, z=31 (A0)
        // Truncated to 6: [6,7,8,9, 2,6] — complete.
        // Packed word 1: (6<<10)|(7<<5)|8 = 0x18E8
        // Packed word 0: (9<<10)|(2<<5)|6 = 0x2446
        // End-bit on word 1: 0x18E8 | 0x8000 = not word 0...
        // Actually: word0 = zchars[0..2], word1 = zchars[3..5]
        // word0: (6<<10)|(7<<5)|8 = 0x18E8
        // word1: (9<<10)|(2<<5)|6 = 0x2446 + end-bit = 0xA446

        // The end-bit must be set (bit 15 of the last word).
        int lastWord = (encoded[2] << 8) | encoded[3];
        Assert.True((lastWord & 0x8000) != 0,
            "End-bit should be set — truncation did not break a multi-z-char construction");
    }

    #endregion

    #region Helpers

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName!;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
