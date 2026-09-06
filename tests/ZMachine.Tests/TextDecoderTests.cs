namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for TextDecoder — Z-character decoding, alphabet tables, shifts,
/// 10-bit ZSCII literals, abbreviation expansion, and custom alphabets.
/// Uses real story files (minizork.z3, zork1.z3) where possible.
/// </summary>
public class TextDecoderTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MinizorkPath = Path.Combine(RepoRoot, "stories/minizork.z3");
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region Real Story File — Basic Decoding

    [Fact]
    public void Minizork_Object1_Forest()
    {
        // Object 1 short name: "forest" at 0x0A50, 2 words (4 bytes)
        var (memory, decoder) = LoadMinizork();
        var (text, byteLen) = decoder.DecodeZString(0x0A50);

        Assert.Equal("forest", text);
        Assert.Equal(4, byteLen);
    }

    [Fact]
    public void Minizork_Object8_Torch()
    {
        // Object 8: "torch" at 0x0ACE, 2 words
        var (memory, decoder) = LoadMinizork();
        var (text, byteLen) = decoder.DecodeZString(0x0ACE);

        Assert.Equal("torch", text);
        Assert.Equal(4, byteLen);
    }

    [Fact]
    public void Minizork_Object10_Lunch()
    {
        // Object 10: "lunch" at 0x0B07, 2 words
        var (memory, decoder) = LoadMinizork();
        var (text, byteLen) = decoder.DecodeZString(0x0B07);

        Assert.Equal("lunch", text);
        Assert.Equal(4, byteLen);
    }

    #endregion

    #region Real Story File — Shift to A1 (Uppercase)

    [Fact]
    public void Minizork_Object2_UpATree()
    {
        // Object 2: "Up a Tree" — uses A1 shifts for 'U' and 'T'
        var (_, decoder) = LoadMinizork();
        var (text, _) = decoder.DecodeZString(0x0A5E);

        Assert.Equal("Up a Tree", text);
    }

    [Fact]
    public void Minizork_Object18_Kitchen()
    {
        // Object 18: "Kitchen" — A1 shift for 'K'
        var (_, decoder) = LoadMinizork();
        var (text, _) = decoder.DecodeZString(0x0BC9);

        Assert.Equal("Kitchen", text);
    }

    #endregion

    #region Real Story File — Shift to A2 (Punctuation)

    [Fact]
    public void Zork1_Object95_ZorkOwnersManual()
    {
        // Object 95: "ZORK owner's manual" — multiple A1 shifts, A2 apostrophe
        var (_, decoder) = LoadZork1();
        var (text, byteLen) = decoder.DecodeZString(0x1404);

        Assert.Equal("ZORK owner's manual", text);
        Assert.Equal(16, byteLen);
    }

    #endregion

    #region Synthetic — Z-char Packing and End Bit

    [Fact]
    public void SingleWord_EndsImmediately()
    {
        // Encode "a" as: z-chars [6, 5, 5] with end bit
        // A0[0]='a'=z-char 6, pad with shift-A2 (5)
        // Word: 0x8000 | (6<<10) | (5<<5) | 5 = 0x8000 | 0x1800 | 0xA0 | 0x05 = 0x98A5
        var mem = CreateSyntheticMemory(0x98, 0xA5);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, byteLen) = decoder.DecodeZString(0x40);
        Assert.Equal("a", text);
        Assert.Equal(2, byteLen);
    }

    [Fact]
    public void TwoWords_SpaceCharacter()
    {
        // Encode "a b": z-chars [6, 0, 7] with end bit in 2nd word would need
        // only one word: [6, 0, 7] = (6<<10)|(0<<5)|7 = 0x1807 + end bit
        var mem = CreateSyntheticMemory(0x98, 0x07);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("a b", text);
    }

    [Fact]
    public void ByteLength_AlwaysMultipleOf2()
    {
        // Three words = 6 bytes
        // "abcdefghi" = z-chars [6,7,8,9,10,11,12,13,14] = 3 words
        var data = new byte[0x48];
        data[0] = 3; // V3
        data[0x0E] = 0x00; data[0x0F] = 0x48; // static base
        // Word 1: [6,7,8] = (6<<10)|(7<<5)|8 = 0x18E8
        data[0x40] = 0x18; data[0x41] = 0xE8;
        // Word 2: [9,10,11] = (9<<10)|(10<<5)|11 = 0x254B
        data[0x42] = 0x25; data[0x43] = 0x4B;
        // Word 3: [12,13,14] + end bit = 0x8000|(12<<10)|(13<<5)|14 = 0xB1AE
        data[0x44] = 0xB1; data[0x45] = 0xAE;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, byteLen) = decoder.DecodeZString(0x40);
        Assert.Equal("abcdefghi", text);
        Assert.Equal(6, byteLen);
    }

    #endregion

    #region Synthetic — 10-Bit ZSCII Escape

    [Fact]
    public void A2Char6_TenBitZsciiLiteral()
    {
        // Shift to A2 (5), then z-char 6 = ZSCII escape,
        // next two z-chars form 10-bit code: hi=2, lo=1 → code 65 = 'A'
        // Z-chars: [5, 6, 2, 1, 5, 5] across 2 words
        // Word 1: [5, 6, 2] = (5<<10)|(6<<5)|2 = 0x14C2
        // Word 2: [1, 5, 5] + end = 0x8000|(1<<10)|(5<<5)|5 = 0x84A5
        var data = new byte[0x48];
        data[0] = 3;
        data[0x0E] = 0x00; data[0x0F] = 0x48;
        data[0x40] = 0x14; data[0x41] = 0xC2;
        data[0x42] = 0x84; data[0x43] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("A", text);
    }

    [Fact]
    public void A2Char6_HighCodePoint()
    {
        // 10-bit code for ZSCII 233 (é in default ZSCII table)
        // 233 = 7*32 + 9, so hi=7, lo=9
        // Z-chars: [5, 6, 7, 9, 5, 5]
        // Word 1: [5, 6, 7] = (5<<10)|(6<<5)|7 = 0x14C7
        // Word 2: [9, 5, 5] + end = 0x8000|(9<<10)|(5<<5)|5 = 0xA4A5
        var data = new byte[0x48];
        data[0] = 3;
        data[0x0E] = 0x00; data[0x0F] = 0x48;
        data[0x40] = 0x14; data[0x41] = 0xC7;
        data[0x42] = 0xA4; data[0x43] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("é", text); // é
    }

    #endregion

    #region Synthetic — Abbreviation Expansion

    [Fact]
    public void Abbreviation_ExpandsCorrectly()
    {
        // Set up: abbreviation 0 at word address $30 (byte $60), decodes to "the"
        // Main string uses z-char 1, next z-char 0 (entry index = 0*32+0 = 0)
        var data = new byte[0x80];
        data[0] = 3;
        data[0x0E] = 0x00; data[0x0F] = 0x80; // static base
        data[0x18] = 0x00; data[0x19] = 0x50;  // abbreviation table at $50

        // Abbreviation table entry 0 at $50: word address $30 → byte $60
        data[0x50] = 0x00; data[0x51] = 0x30;

        // Abbreviation string at $60: "the" = z-chars [25,13,10,5,5,5]
        // Word 1: [25,13,10] = (25<<10)|(13<<5)|10 = 0x65AA
        // Word 2: [5,5,5] + end = 0x8000|(5<<10)|(5<<5)|5 = 0x94A5
        data[0x60] = 0x65; data[0x61] = 0xAA;
        data[0x62] = 0x94; data[0x63] = 0xA5;

        // Main string at $70: z-chars [1, 0, 5, 5, 5, 5]
        // z-char 1 = abbreviation trigger, next z-char 0 = entry index
        // Word 1: [1, 0, 5] = (1<<10)|(0<<5)|5 = 0x0405
        // Word 2: [5, 5, 5] + end = 0x94A5
        data[0x70] = 0x04; data[0x71] = 0x05;
        data[0x72] = 0x94; data[0x73] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 3, 0x50);

        var (text, _) = decoder.DecodeZString(0x70);
        Assert.Equal("the", text);
    }

    [Fact]
    public void Abbreviation_RecursionThrows()
    {
        // An abbreviation string that itself contains an abbreviation trigger
        var data = new byte[0x80];
        data[0] = 3;
        data[0x0E] = 0x00; data[0x0F] = 0x80;
        data[0x18] = 0x00; data[0x19] = 0x50;

        // Entry 0 points to byte $60
        data[0x50] = 0x00; data[0x51] = 0x30; // word addr $30 → byte $60

        // Abbreviation at $60 tries to use abbreviation (z-char 1, 0)
        // Word: [1, 0, 5] + end = 0x8000|(1<<10)|5 = 0x8405
        data[0x60] = 0x84; data[0x61] = 0x05;

        // Main string at $70: abbreviation trigger
        data[0x70] = 0x04; data[0x71] = 0x05;
        data[0x72] = 0x94; data[0x73] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 3, 0x50);

        Assert.Throws<InvalidOperationException>(() => decoder.DecodeZString(0x70));
    }

    [Fact]
    public void Abbreviation_V2_OnlyZchar1()
    {
        // In V2, only z-char 1 triggers abbreviation. Z-chars 2,3 are shift chars.
        // Encode z-char 2 followed by something — should shift, not abbreviate.
        // V2: z-char 2 = shift to A1 (in V1–2, 2/3 are shifts, 4/5 are shift-locks)
        // Wait — in V1–2, the shift characters are actually 2,3 (single-shift)
        // and 4,5 (shift-lock). In V3+, shifts are 4,5 and 1,2,3 are abbreviations.
        // So in V2 with z-char 2: it's a single-shift to A1.
        // Z-chars: [2, 6, 5, 5, 5, 5]  (shift to A1, then A1[0]='A', pad)
        // Wait, this is getting complex. Let me just verify that V2 z-char 1
        // triggers abbreviation and z-chars 2,3 don't.

        // Actually in V1-2: z-chars 2,3 = single-shift A1,A2; 4,5 = shift-lock
        // In V3+: z-chars 1,2,3 = abbreviation; 4,5 = single-shift A1,A2

        // For V2 let me just test that z-char 1 triggers abbreviation
        var data = new byte[0x80];
        data[0] = 2; // V2
        data[0x0E] = 0x00; data[0x0F] = 0x80;
        data[0x18] = 0x00; data[0x19] = 0x50;

        data[0x50] = 0x00; data[0x51] = 0x30;

        // "hi" at $60
        // h=13, i=14: [13,14,5] + end = 0x8000|(13<<10)|(14<<5)|5 = 0xB5C5
        data[0x60] = 0xB5; data[0x61] = 0xC5;

        // Main at $70: [1, 0, 5, 5, 5, 5]
        data[0x70] = 0x04; data[0x71] = 0x05;
        data[0x72] = 0x94; data[0x73] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 2, 0x50);

        var (text, _) = decoder.DecodeZString(0x70);
        Assert.Equal("hi", text);
    }

    [Fact]
    public void V1_ZChar1_IsNewline()
    {
        // In V1, z-char 1 = newline, not abbreviation
        // Z-chars: [6, 1, 7] + end → "a\nb"
        var mem = CreateSyntheticMemory(0x98, 0x27, version: 1);
        var decoder = new TextDecoder(mem, 1, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("a\nb", text);
    }

    #endregion

    #region V1–2 Shift Semantics

    [Fact]
    public void V2_SingleShift_FromA0_ToA1()
    {
        // V2: z-char 2 = single-shift to next alphabet (A0→A1).
        // Z-chars: [2, 6, 7] → shift-A1, A1[0]='A', then back to A0, A0[1]='b'
        // Word: 0x8000 | (2<<10)|(6<<5)|7 = 0x80C7 — wait, that encodes wrong.
        // Actually: (2<<10)|(6<<5)|7 = 2048+192+7 = 2247 = 0x08C7
        // With end bit: 0x88C7
        var data = new byte[0x48];
        data[0] = 2; // V2
        data[0x0E] = 0x00; data[0x0F] = 0x48;
        data[0x40] = 0x88; data[0x41] = 0xC7;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 2, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("Ab", text);
    }

    [Fact]
    public void V2_SingleShift_RevertsAfterOneChar()
    {
        // V2: z-char 2 shifts to A1 for exactly one character, then reverts.
        // Z-chars: [2, 6, 6, 5, 5, 5] → shift-A1, A1[0]='A', back to A0, A0[0]='a', pad
        // Word 1: (2<<10)|(6<<5)|6 = 0x08C6
        // Word 2: 0x8000|(5<<10)|(5<<5)|5 = 0x94A5
        var data = new byte[0x48];
        data[0] = 2;
        data[0x0E] = 0x00; data[0x0F] = 0x48;
        data[0x40] = 0x08; data[0x41] = 0xC6;
        data[0x42] = 0x94; data[0x43] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 2, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("Aa", text);
    }

    [Fact]
    public void V2_ShiftLockThenSingleShift_RevertsToLockedAlphabet()
    {
        // The key bug scenario: shift-lock to A1 (z-char 4), then single-shift
        // to A2 (z-char 3), should revert to A1 (not A2 or A0) after one char.
        //
        // Z-chars: [4, 6, 3, 7, 8] → lock-A1, A1[0]='A', shift-A2, A2[1]='\n', A1[2]='C'
        //
        // 4 = shift-lock to next (A0→A1), lockedAlphabet=1
        // 6 in A1 = 'A', revert → stays A1 (locked)
        // 3 = single-shift prev from A1 → (1+2)%3 = A0...
        // Wait: 3 = shift to *previous* alphabet. From A1, previous = A0.
        // Let me use: lock to A1 (4), print from A1, single-shift next from A1 (2) → A2,
        // print from A2, then should revert to A1.
        //
        // Z-chars: [4, 6, 2, 8, 7, 5, 5, 5, 5]
        //   4 → lock A1 (lockedAlphabet=1, currentAlphabet=1)
        //   6 in A1 → 'A', revert to locked A1
        //   2 → single-shift next from A1 → (1+1)%3 = A2
        //   8 in A2 → A2[8-6] = A2[2] = '0'
        //   revert to locked A1
        //   7 in A1 → A1[7-6] = A1[1] = 'B'
        //   pad...
        //
        // Word 1: (4<<10)|(6<<5)|2 = 4096+192+2 = 0x10C2
        // Word 2: (8<<10)|(7<<5)|5 = 8192+224+5 = 0x20E5
        // Word 3: 0x8000|(5<<10)|(5<<5)|5 = 0x94A5
        var data = new byte[0x4C];
        data[0] = 2; // V2
        data[0x0E] = 0x00; data[0x0F] = 0x4C;
        data[0x40] = 0x10; data[0x41] = 0xC2;
        data[0x42] = 0x20; data[0x43] = 0xE5;
        data[0x44] = 0x94; data[0x45] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 2, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("A0B", text);
    }

    [Fact]
    public void V2_ShiftLock_PersistsAcrossCharacters()
    {
        // V2: z-char 4 = shift-lock to A1. Subsequent chars stay in A1.
        // Z-chars: [4, 6, 7, 8, 5, 5]
        //   4 → lock A1
        //   6 → A1[0]='A', revert to locked A1
        //   7 → A1[1]='B', revert to locked A1
        //   8 → A1[2]='C'
        //   pad
        // Word 1: (4<<10)|(6<<5)|7 = 0x10C7
        // Word 2: 0x8000|(8<<10)|(5<<5)|5 = 0xA0A5
        var data = new byte[0x48];
        data[0] = 2;
        data[0x0E] = 0x00; data[0x0F] = 0x48;
        data[0x40] = 0x10; data[0x41] = 0xC7;
        data[0x42] = 0xA0; data[0x43] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 2, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("ABC", text);
    }

    #endregion

    #region Custom Alphabet Tables (V5+)

    [Fact]
    public void CustomAlphabet_OverridesDefaults()
    {
        // V5 story with custom alphabet at address $40
        // Custom A0: all 'X' (just to test override works)
        var data = new byte[0xB0];
        data[0] = 5; // V5
        data[0x04] = 0x00; data[0x05] = 0xB0; // high base
        data[0x0E] = 0x00; data[0x0F] = 0xA0; // static base at $A0
        data[0x34] = 0x00; data[0x35] = 0x40; // alphabet table at $40

        // Custom A0: 26 bytes of 'Z' (0x5A) at $40
        for (int i = 0; i < 26; i++) data[0x40 + i] = (byte)'Z';
        // Custom A1: 26 bytes of 'z' at $5A
        for (int i = 0; i < 26; i++) data[0x5A + i] = (byte)'z';
        // Custom A2: 26 bytes of '!' at $74
        for (int i = 0; i < 26; i++) data[0x74 + i] = (byte)'!';

        // Z-string at $90: z-char 6 (A0[0]) = 'Z' with custom
        // Word: [6, 5, 5] + end = 0x8000|(6<<10)|(5<<5)|5 = 0x98A5
        data[0x90] = 0x98; data[0x91] = 0xA5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 5, 0, alphabetTableAddress: 0x40);

        var (text, _) = decoder.DecodeZString(0x90);
        Assert.Equal("Z", text);
    }

    [Fact]
    public void CustomAlphabet_A1ShiftWorks()
    {
        var data = new byte[0xB0];
        data[0] = 5;
        data[0x04] = 0x00; data[0x05] = 0xB0;
        data[0x0E] = 0x00; data[0x0F] = 0xA0;
        data[0x34] = 0x00; data[0x35] = 0x40;

        for (int i = 0; i < 26; i++) data[0x40 + i] = (byte)'a';
        for (int i = 0; i < 26; i++) data[0x5A + i] = (byte)'Q';
        for (int i = 0; i < 26; i++) data[0x74 + i] = (byte)'!';

        // Z-chars: [4, 6, 5] + end → shift-A1, A1[0]='Q', pad
        // Word: 0x8000|(4<<10)|(6<<5)|5 = 0x90C5
        data[0x90] = 0x90; data[0x91] = 0xC5;

        var mem = new Memory();
        mem.LoadStory(data);
        var decoder = new TextDecoder(mem, 5, 0, alphabetTableAddress: 0x40);

        var (text, _) = decoder.DecodeZString(0x90);
        Assert.Equal("Q", text);
    }

    #endregion

    #region Real Story File — With Abbreviations

    [Fact]
    public void Minizork_Object19_SandyBeach()
    {
        // "Sandy Beach" — uses A1 shifts
        var (_, decoder) = LoadMinizork();
        var (text, _) = decoder.DecodeZString(0x0BEE);

        Assert.Equal("Sandy Beach", text);
    }

    [Fact]
    public void Minizork_Object13_BraveAdventurer()
    {
        // "brave adventurer" — pure lowercase, no abbreviations
        var (_, decoder) = LoadMinizork();
        var (text, _) = decoder.DecodeZString(0x0B4C);

        Assert.Equal("brave adventurer", text);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyPaddedString()
    {
        // A Z-string that's just padding: [5, 5, 5] + end
        var mem = CreateSyntheticMemory(0x94, 0xA5);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, byteLen) = decoder.DecodeZString(0x40);
        Assert.Equal("", text);
        Assert.Equal(2, byteLen);
    }

    [Fact]
    public void AllSpaces()
    {
        // Z-chars [0, 0, 0] + end = 0x8000 | 0 = 0x8000
        var mem = CreateSyntheticMemory(0x80, 0x00);
        var decoder = new TextDecoder(mem, 3, 0);

        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("   ", text);
    }

    [Fact]
    public void ShiftAtEndOfString_NoOutputChar()
    {
        // Shift char followed by end — the shift has no character to apply to
        // Z-chars: [6, 4, 5] + end → 'a', shift-A1 (nothing follows for A1), shift-A2 pad
        // Actually the 5 is the last z-char. shift-A1(4) applies to the next z-char (5).
        // 5 in A1 context... 5 is a shift char (shift-A2), so it shifts again.
        // Actually after shift to A1 via z-char 4, the next z-char 5 would be
        // interpreted as shift-to-A2 (in V3+). So no output from either.
        var mem = CreateSyntheticMemory(0x98, 0xA5);
        var decoder = new TextDecoder(mem, 3, 0);

        // 0x98A5 = z-chars [6, 5, 5] → 'a', shift, shift → "a"
        var (text, _) = decoder.DecodeZString(0x40);
        Assert.Equal("a", text);
    }

    #endregion

    #region Helpers

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repository root (no .slnx file found).");
    }

    private static (Memory memory, TextDecoder decoder) LoadMinizork()
    {
        var memory = new Memory();
        memory.LoadStory(MinizorkPath);
        var header = new Header(memory);
        var decoder = new TextDecoder(
            memory, header.Version, header.AbbreviationTableAddress,
            header.AlphabetTableAddress);
        return (memory, decoder);
    }

    private static (Memory memory, TextDecoder decoder) LoadZork1()
    {
        var memory = new Memory();
        memory.LoadStory(Zork1Path);
        var header = new Header(memory);
        var decoder = new TextDecoder(
            memory, header.Version, header.AbbreviationTableAddress,
            header.AlphabetTableAddress);
        return (memory, decoder);
    }

    /// <summary>
    /// Creates a synthetic V3 memory with a single Z-string word at
    /// address $40 from the given two bytes.
    /// </summary>
    private static Memory CreateSyntheticMemory(byte hi, byte lo, int version = 3)
    {
        var data = new byte[0x48];
        data[0] = (byte)version;
        data[0x0E] = 0x00; data[0x0F] = 0x48; // static base
        data[0x40] = hi; data[0x41] = lo;

        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    #endregion
}
