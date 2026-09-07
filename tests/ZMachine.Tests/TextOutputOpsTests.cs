namespace ZMachine.Tests;

using System.Text;
using ZMachine.Core;

/// <summary>
/// Tests for TextOutputOps — Z-Machine text output opcodes. Covers inline
/// Z-string decoding with PC advancement, signed number printing, rectangular
/// table output, Unicode printing, ZSCII encoding, and control code rejection.
/// Uses zork1.z3 for Z-string decoding tests and synthetic memory for table/encode tests.
/// </summary>
public class TextOutputOpsTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region @print — inline Z-string

    [Fact]
    public void Print_DecodesZStringAndReturnsByteLength()
    {
        var (ops, output, memory) = CreateZorkOps();

        // Find a known Z-string address in zork1 — the game's title string
        // is part of the story; use an object short name address instead.
        // Object 180 (West of House) — get its short name address.
        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objTable = new ObjectTable(memory, version, objTableAddr);
        int nameAddr = objTable.GetShortNameAddress(180);

        int byteLen = ops.Print(nameAddr);

        Assert.True(byteLen > 0);
        Assert.Contains("West of House", output.ToString());
    }

    [Fact]
    public void Print_ReturnsByteLengthForPCAdvancement()
    {
        var (ops, _, memory) = CreateZorkOps();

        // Decode two adjacent strings and verify byte lengths are consistent.
        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objTable = new ObjectTable(memory, version, objTableAddr);

        int addr1 = objTable.GetShortNameAddress(180);
        int byteLen1 = ops.Print(addr1);

        // Byte length should be even (Z-strings are word-aligned).
        Assert.Equal(0, byteLen1 % 2);
    }

    #endregion

    #region @print_ret

    [Fact]
    public void PrintRet_PrintsStringAndNewline()
    {
        var (ops, output, memory) = CreateZorkOps();

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objTable = new ObjectTable(memory, version, objTableAddr);
        int nameAddr = objTable.GetShortNameAddress(180);

        int byteLen = ops.PrintRet(nameAddr);

        string result = output.ToString();
        Assert.Contains("West of House", result);
        Assert.EndsWith("\n", result);
        Assert.True(byteLen > 0);
    }

    #endregion

    #region @print_addr / @print_paddr

    [Fact]
    public void PrintAddr_PrintsZStringAtByteAddress()
    {
        var (ops, output, memory) = CreateZorkOps();

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objTable = new ObjectTable(memory, version, objTableAddr);
        int nameAddr = objTable.GetShortNameAddress(160);

        ops.PrintAddr((ushort)nameAddr);

        Assert.Contains("small mailbox", output.ToString());
    }

    [Fact]
    public void PrintPAddr_PrintsZStringAtUnpackedAddress()
    {
        var (ops, output, memory) = CreateZorkOps();

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objTable = new ObjectTable(memory, version, objTableAddr);
        int nameAddr = objTable.GetShortNameAddress(180);

        ops.PrintPAddr(nameAddr);

        Assert.Contains("West of House", output.ToString());
    }

    #endregion

    #region @print_char

    [Fact]
    public void PrintChar_PrintsAsciiCharacter()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintChar((ushort)'A');

        Assert.Equal("A", output.ToString());
    }

    [Fact]
    public void PrintChar_PrintsSpace()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintChar(32);

        Assert.Equal(" ", output.ToString());
    }

    #endregion

    #region @print_num

    [Fact]
    public void PrintNum_PositiveNumber()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintNum(42);

        Assert.Equal("42", output.ToString());
    }

    [Fact]
    public void PrintNum_Zero()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintNum(0);

        Assert.Equal("0", output.ToString());
    }

    [Fact]
    public void PrintNum_NegativeNumber()
    {
        var (ops, output) = CreateSyntheticOps();

        // 0xFFD6 = -42 as signed 16-bit
        ops.PrintNum(unchecked((ushort)(short)-42));

        Assert.Equal("-42", output.ToString());
    }

    [Fact]
    public void PrintNum_MinSignedValue()
    {
        var (ops, output) = CreateSyntheticOps();

        // 0x8000 = -32768
        ops.PrintNum(0x8000);

        Assert.Equal("-32768", output.ToString());
    }

    [Fact]
    public void PrintNum_MaxPositiveValue()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintNum(32767);

        Assert.Equal("32767", output.ToString());
    }

    #endregion

    #region @new_line

    [Fact]
    public void NewLine_PrintsNewline()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.NewLine();

        Assert.Equal("\n", output.ToString());
    }

    #endregion

    #region @print_table

    [Fact]
    public void PrintTable_SingleRow()
    {
        var (ops, output, memory) = CreateSyntheticOpsWithMemory();

        // Write "Hello" at address 0x1000.
        ushort table = 0x1000;
        memory.WriteByte(table, (byte)'H');
        memory.WriteByte(table + 1, (byte)'e');
        memory.WriteByte(table + 2, (byte)'l');
        memory.WriteByte(table + 3, (byte)'l');
        memory.WriteByte(table + 4, (byte)'o');

        ops.PrintTable(table, 5, 1, 0);

        Assert.Equal("Hello", output.ToString());
    }

    [Fact]
    public void PrintTable_MultipleRows()
    {
        var (ops, output, memory) = CreateSyntheticOpsWithMemory();

        ushort table = 0x1000;
        // Row 1: "AB"
        memory.WriteByte(table, (byte)'A');
        memory.WriteByte(table + 1, (byte)'B');
        // Row 2: "CD"
        memory.WriteByte(table + 2, (byte)'C');
        memory.WriteByte(table + 3, (byte)'D');
        // Row 3: "EF"
        memory.WriteByte(table + 4, (byte)'E');
        memory.WriteByte(table + 5, (byte)'F');

        ops.PrintTable(table, 2, 3, 0);

        Assert.Equal("AB\nCD\nEF", output.ToString());
    }

    [Fact]
    public void PrintTable_WithSkipBytes()
    {
        var (ops, output, memory) = CreateSyntheticOpsWithMemory();

        ushort table = 0x1000;
        // Row 1 at 0x1000: "XY"
        memory.WriteByte(table, (byte)'X');
        memory.WriteByte(table + 1, (byte)'Y');
        // Skip 2 bytes (0x1002, 0x1003)
        // Row 2 at 0x1004: "ZW"
        memory.WriteByte(table + 4, (byte)'Z');
        memory.WriteByte(table + 5, (byte)'W');

        ops.PrintTable(table, 2, 2, 2);

        Assert.Equal("XY\nZW", output.ToString());
    }

    [Fact]
    public void PrintTable_DefaultHeight1_NoNewline()
    {
        var (ops, output, memory) = CreateSyntheticOpsWithMemory();

        ushort table = 0x1000;
        memory.WriteByte(table, (byte)'!');

        ops.PrintTable(table, 1, 1, 0);

        // Single row, no trailing newline.
        Assert.Equal("!", output.ToString());
        Assert.DoesNotContain("\n", output.ToString());
    }

    #endregion

    #region @print_unicode

    [Fact]
    public void PrintUnicode_BmpCharacter()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(0x00E9); // é

        Assert.Equal("é", output.ToString());
    }

    [Fact]
    public void PrintUnicode_GreekLetter()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(0x03B1); // α

        Assert.Equal("α", output.ToString());
    }

    [Fact]
    public void PrintUnicode_RejectsControlCode_Low()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(0x0010); // Control character

        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void PrintUnicode_RejectsControlCode_Delete()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(127); // DEL

        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void PrintUnicode_RejectsControlCode_C1Range()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(0x009F); // Last C1 control code

        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void PrintUnicode_AcceptsSpaceAndAbove()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintUnicode(32); // Space

        Assert.Equal(" ", output.ToString());
    }

    [Fact]
    public void PrintUnicode_Accepts160()
    {
        var (ops, output) = CreateSyntheticOps();

        // 160 (0xA0) is non-breaking space — first valid char after C1 range.
        ops.PrintUnicode(160);

        Assert.Equal(" ", output.ToString());
    }

    #endregion

    #region @encode_text

    [Fact]
    public void EncodeText_WritesEncodedBytesToMemory()
    {
        var (ops, _, memory) = CreateSyntheticOpsWithMemory();

        // Write "open" at address 0x2000.
        ushort zsciiText = 0x2000;
        memory.WriteByte(zsciiText, (byte)'o');
        memory.WriteByte(zsciiText + 1, (byte)'p');
        memory.WriteByte(zsciiText + 2, (byte)'e');
        memory.WriteByte(zsciiText + 3, (byte)'n');

        ushort codedText = 0x3000;
        ops.EncodeText(zsciiText, 4, 0, codedText);

        // Read the encoded bytes — should be 6 bytes for V5.
        byte[] encoded = new byte[6];
        for (int i = 0; i < 6; i++)
            encoded[i] = memory.ReadByte(codedText + i);

        // Verify it's not all zeros (encoding happened).
        bool allZero = true;
        for (int i = 0; i < encoded.Length; i++)
            if (encoded[i] != 0) allZero = false;
        Assert.False(allZero);
    }

    [Fact]
    public void EncodeText_WithFromOffset()
    {
        var (ops, _, memory) = CreateSyntheticOpsWithMemory();

        // Write "xxhello" at address 0x2000; encode "hello" with from=2.
        ushort zsciiText = 0x2000;
        memory.WriteByte(zsciiText, (byte)'x');
        memory.WriteByte(zsciiText + 1, (byte)'x');
        memory.WriteByte(zsciiText + 2, (byte)'h');
        memory.WriteByte(zsciiText + 3, (byte)'e');
        memory.WriteByte(zsciiText + 4, (byte)'l');
        memory.WriteByte(zsciiText + 5, (byte)'l');
        memory.WriteByte(zsciiText + 6, (byte)'o');

        ushort codedText = 0x3000;
        ops.EncodeText(zsciiText, 5, 2, codedText);

        // Encode "hello" directly for comparison.
        ushort zsciiDirect = 0x2100;
        memory.WriteByte(zsciiDirect, (byte)'h');
        memory.WriteByte(zsciiDirect + 1, (byte)'e');
        memory.WriteByte(zsciiDirect + 2, (byte)'l');
        memory.WriteByte(zsciiDirect + 3, (byte)'l');
        memory.WriteByte(zsciiDirect + 4, (byte)'o');

        ushort codedDirect = 0x3100;
        ops.EncodeText(zsciiDirect, 5, 0, codedDirect);

        // Both should produce the same encoded output.
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(
                memory.ReadByte(codedDirect + i),
                memory.ReadByte(codedText + i));
        }
    }

    [Fact]
    public void EncodeText_RoundTripsWithTextEncoder()
    {
        // Encode "open" via TextEncoder directly and compare with EncodeText.
        var encoder = new TextEncoder(5);
        byte[] directEncoded = encoder.EncodeForDictionary("open");

        var (ops, _, memory) = CreateSyntheticOpsWithMemory();

        ushort zsciiText = 0x2000;
        memory.WriteByte(zsciiText, (byte)'o');
        memory.WriteByte(zsciiText + 1, (byte)'p');
        memory.WriteByte(zsciiText + 2, (byte)'e');
        memory.WriteByte(zsciiText + 3, (byte)'n');

        ushort codedText = 0x3000;
        ops.EncodeText(zsciiText, 4, 0, codedText);

        for (int i = 0; i < 6; i++)
            Assert.Equal(directEncoded[i], memory.ReadByte(codedText + i));
    }

    #endregion

    #region Combined output

    [Fact]
    public void CombinedOutput_PrintCharAndNewLine()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintChar((ushort)'H');
        ops.PrintChar((ushort)'i');
        ops.NewLine();

        Assert.Equal("Hi\n", output.ToString());
    }

    [Fact]
    public void CombinedOutput_PrintNumBetweenText()
    {
        var (ops, output) = CreateSyntheticOps();

        ops.PrintChar((ushort)'$');
        ops.PrintNum(100);
        ops.NewLine();

        Assert.Equal("$100\n", output.ToString());
    }

    #endregion

    #region Helpers

    private static (TextOutputOps Ops, StringBuilder Output, Memory Memory) CreateZorkOps()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int abbrAddr = memory.ReadWord(0x18);

        var textDecoder = new TextDecoder(memory, version, abbrAddr);
        var textEncoder = new TextEncoder(version);
        var outputStreams = new OutputStreamManager(memory);

        var output = new StringBuilder();
        outputStreams.ScreenPrint = s => output.Append(s);

        var ops = new TextOutputOps(memory, textDecoder, textEncoder, outputStreams, version);
        return (ops, output, memory);
    }

    private static (TextOutputOps Ops, StringBuilder Output) CreateSyntheticOps()
    {
        var (ops, output, _) = CreateSyntheticOpsWithMemory();
        return (ops, output);
    }

    private static (TextOutputOps Ops, StringBuilder Output, Memory Memory) CreateSyntheticOpsWithMemory()
    {
        var memory = new Memory();
        byte[] story = new byte[0x10000];
        story[0x00] = 5; // Version 5
        // Header $04: high memory base
        story[0x04] = 0x80; story[0x05] = 0x00;
        // Header $0E: static memory base — set high to allow writes to test addresses.
        story[0x0E] = 0x80; story[0x0F] = 0x00;
        memory.LoadStory(story);

        int version = 5;
        var textDecoder = new TextDecoder(memory, version, 0);
        var textEncoder = new TextEncoder(version);
        var outputStreams = new OutputStreamManager(memory);

        var output = new StringBuilder();
        outputStreams.ScreenPrint = s => output.Append(s);

        var ops = new TextOutputOps(memory, textDecoder, textEncoder, outputStreams, version);
        return (ops, output, memory);
    }

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
