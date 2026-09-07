namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for Dictionary and Tokenizer — dictionary parsing, word lookup
/// via binary search, tokenization, and parse buffer writing. Uses
/// zork1.z3 for real-world verification and synthetic data for edge cases.
/// </summary>
public class DictionaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region Dictionary Parsing — Real Story

    [Fact]
    public void Zork1_ParseDictionary_Separators()
    {
        var (dict, _) = LoadZork1Dictionary();

        Assert.Equal(3, dict.Separators.Length);
        Assert.Contains(',', dict.Separators);
        Assert.Contains('.', dict.Separators);
        Assert.Contains('"', dict.Separators);
    }

    [Fact]
    public void Zork1_ParseDictionary_EntryLength()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(7, dict.EntryLength);
    }

    [Fact]
    public void Zork1_ParseDictionary_EntryCount()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(697, dict.EntryCount);
        Assert.True(dict.IsSorted);
    }

    [Fact]
    public void Zork1_ParseDictionary_EntriesStart()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x3B28, dict.EntriesStart);
    }

    #endregion

    #region Dictionary Lookup — Real Story

    [Fact]
    public void Zork1_Lookup_Open()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x4688, dict.Lookup("open"));
    }

    [Fact]
    public void Zork1_Lookup_Mailbox()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x453F, dict.Lookup("mailbox"));
    }

    [Fact]
    public void Zork1_Lookup_North()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x461F, dict.Lookup("north"));
    }

    [Fact]
    public void Zork1_Lookup_Take()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x4B7B, dict.Lookup("take"));
    }

    [Fact]
    public void Zork1_Lookup_Look()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x44F2, dict.Lookup("look"));
    }

    [Fact]
    public void Zork1_Lookup_West()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0x4D88, dict.Lookup("west"));
    }

    [Fact]
    public void Zork1_Lookup_NotFound()
    {
        var (dict, _) = LoadZork1Dictionary();
        Assert.Equal(0, dict.Lookup("qqqqq"));
    }

    [Fact]
    public void Zork1_Lookup_Truncation()
    {
        // V3 truncates to 6 characters — "mailbox" and "mailbo" should match.
        var (dict, _) = LoadZork1Dictionary();
        int mailboxAddr = dict.Lookup("mailbox");
        int mailboAddr = dict.Lookup("mailbo");
        Assert.Equal(mailboxAddr, mailboAddr);
    }

    #endregion

    #region Tokenizer — SplitIntoTokens

    [Fact]
    public void SplitIntoTokens_SimpleWords()
    {
        var tokens = Tokenizer.SplitIntoTokens("open mailbox", [',', '.', '"']);

        Assert.Equal(2, tokens.Count);
        Assert.Equal("open", tokens[0].Text);
        Assert.Equal(0, tokens[0].Position);
        Assert.Equal(4, tokens[0].Length);
        Assert.Equal("mailbox", tokens[1].Text);
        Assert.Equal(5, tokens[1].Position);
        Assert.Equal(7, tokens[1].Length);
    }

    [Fact]
    public void SplitIntoTokens_WithSeparator()
    {
        var tokens = Tokenizer.SplitIntoTokens("take all,drop all", [',', '.', '"']);

        Assert.Equal(5, tokens.Count);
        Assert.Equal("take", tokens[0].Text);
        Assert.Equal("all", tokens[1].Text);
        Assert.Equal(",", tokens[2].Text);
        Assert.Equal(8, tokens[2].Position);
        Assert.Equal(1, tokens[2].Length);
        Assert.Equal("drop", tokens[3].Text);
        Assert.Equal("all", tokens[4].Text);
    }

    [Fact]
    public void SplitIntoTokens_LeadingSpaces()
    {
        var tokens = Tokenizer.SplitIntoTokens("  look  ", [',', '.', '"']);

        Assert.Single(tokens);
        Assert.Equal("look", tokens[0].Text);
        Assert.Equal(2, tokens[0].Position);
    }

    [Fact]
    public void SplitIntoTokens_MultipleSpaces()
    {
        var tokens = Tokenizer.SplitIntoTokens("go   north", [',', '.', '"']);

        Assert.Equal(2, tokens.Count);
        Assert.Equal("go", tokens[0].Text);
        Assert.Equal("north", tokens[1].Text);
        Assert.Equal(5, tokens[1].Position);
    }

    [Fact]
    public void SplitIntoTokens_SeparatorOnly()
    {
        var tokens = Tokenizer.SplitIntoTokens(",", [',', '.', '"']);

        Assert.Single(tokens);
        Assert.Equal(",", tokens[0].Text);
    }

    [Fact]
    public void SplitIntoTokens_EmptyInput()
    {
        var tokens = Tokenizer.SplitIntoTokens("", [',', '.', '"']);
        Assert.Empty(tokens);
    }

    [Fact]
    public void SplitIntoTokens_AdjacentSeparators()
    {
        var tokens = Tokenizer.SplitIntoTokens("a,.b", [',', '.', '"']);

        Assert.Equal(4, tokens.Count);
        Assert.Equal("a", tokens[0].Text);
        Assert.Equal(",", tokens[1].Text);
        Assert.Equal(".", tokens[2].Text);
        Assert.Equal("b", tokens[3].Text);
    }

    #endregion

    #region Tokenizer — Full Tokenization with Zork1

    [Fact]
    public void Zork1_Tokenize_OpenMailbox()
    {
        var (dict, memory) = LoadZork1Dictionary();
        var tokenizer = new Tokenizer(3, new TextEncoder(3));

        // Set up parse buffer at a writable address.
        int parseAddr = 0x0040;
        memory.WriteByte(parseAddr, 10); // max 10 words

        tokenizer.Tokenize("open mailbox", dict, memory, parseAddr,
            textBufferOffset: 1);

        // Word count = 2.
        Assert.Equal(2, memory.ReadByte(parseAddr + 1));

        // Word 1: "open" at dict 0x4688, length 4, position 1.
        Assert.Equal(0x4688, memory.ReadWord(parseAddr + 2));
        Assert.Equal(4, memory.ReadByte(parseAddr + 4));
        Assert.Equal(1, memory.ReadByte(parseAddr + 5));

        // Word 2: "mailbox" at dict 0x453F, length 7, position 6.
        Assert.Equal(0x453F, memory.ReadWord(parseAddr + 6));
        Assert.Equal(7, memory.ReadByte(parseAddr + 8));
        Assert.Equal(6, memory.ReadByte(parseAddr + 9));
    }

    [Fact]
    public void Zork1_Tokenize_UnknownWord()
    {
        var (dict, memory) = LoadZork1Dictionary();
        var tokenizer = new Tokenizer(3, new TextEncoder(3));

        int parseAddr = 0x0040;
        memory.WriteByte(parseAddr, 10);

        tokenizer.Tokenize("qqqqq", dict, memory, parseAddr,
            textBufferOffset: 1);

        Assert.Equal(1, memory.ReadByte(parseAddr + 1));
        // Dict address = 0 for unknown word.
        Assert.Equal(0, memory.ReadWord(parseAddr + 2));
        Assert.Equal(5, memory.ReadByte(parseAddr + 4));
    }

    [Fact]
    public void Zork1_Tokenize_WithSeparator()
    {
        var (dict, memory) = LoadZork1Dictionary();
        var tokenizer = new Tokenizer(3, new TextEncoder(3));

        int parseAddr = 0x0040;
        memory.WriteByte(parseAddr, 10);

        tokenizer.Tokenize("look,north", dict, memory, parseAddr,
            textBufferOffset: 1);

        // 3 tokens: look, comma, north.
        Assert.Equal(3, memory.ReadByte(parseAddr + 1));

        // "look" at 0x44F2
        Assert.Equal(0x44F2, memory.ReadWord(parseAddr + 2));
        // "," — comma is a separator, looked up in dict
        int commaAddr = memory.ReadWord(parseAddr + 6);
        // The comma may or may not be in the dictionary.
        Assert.Equal(1, memory.ReadByte(parseAddr + 8)); // length = 1
        // "north" at 0x461F
        Assert.Equal(0x461F, memory.ReadWord(parseAddr + 10));
    }

    [Fact]
    public void Zork1_Tokenize_MaxWords_Truncates()
    {
        var (dict, memory) = LoadZork1Dictionary();
        var tokenizer = new Tokenizer(3, new TextEncoder(3));

        int parseAddr = 0x0040;
        memory.WriteByte(parseAddr, 2); // max 2 words

        tokenizer.Tokenize("take the mailbox", dict, memory, parseAddr,
            textBufferOffset: 1);

        // Only 2 words written.
        Assert.Equal(2, memory.ReadByte(parseAddr + 1));
    }

    [Fact]
    public void Tokenize_SkipUnrecognized()
    {
        var (dict, memory) = LoadZork1Dictionary();
        var tokenizer = new Tokenizer(3, new TextEncoder(3));

        int parseAddr = 0x0040;
        memory.WriteByte(parseAddr, 10);

        tokenizer.Tokenize("qqqqq mailbox", dict, memory, parseAddr,
            textBufferOffset: 1, skipUnrecognized: true);

        // Only "mailbox" should be in the parse buffer (qqqqq skipped).
        Assert.Equal(1, memory.ReadByte(parseAddr + 1));
        Assert.Equal(0x453F, memory.ReadWord(parseAddr + 2));
    }

    #endregion

    #region Synthetic — Unsorted Dictionary

    [Fact]
    public void UnsortedDictionary_LinearSearch()
    {
        var (dict, _) = CreateSyntheticUnsorted();

        // "abc" should be found via linear scan.
        int addr = dict.Lookup("abc");
        Assert.NotEqual(0, addr);
        Assert.False(dict.IsSorted);
    }

    #endregion

    #region Helpers

    private static (Dictionary, Memory) LoadZork1Dictionary()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int dictAddr = memory.ReadWord(0x08);
        var encoder = new TextEncoder(3);
        var dict = new Dictionary(memory, 3, encoder);
        dict.Parse(dictAddr);

        return (dict, memory);
    }

    private static (Dictionary, Memory) CreateSyntheticUnsorted()
    {
        int dataSize = 0x0200;
        byte[] data = new byte[dataSize];
        data[0] = 3;
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);

        // Dictionary at 0x0080.
        int dictAddr = 0x0080;
        int p = dictAddr;

        // 0 separators.
        data[p++] = 0;

        // Entry length = 7 (4 encoded + 3 data).
        data[p++] = 7;

        // Entry count = -2 (unsorted, 2 entries).
        // -2 as signed 16-bit = 0xFFFE
        data[p++] = 0xFF;
        data[p++] = 0xFE;

        // Entry 1: encode "xyz" manually.
        var encoder = new TextEncoder(3);
        byte[] xyzEncoded = encoder.EncodeForDictionary("xyz");
        Array.Copy(xyzEncoded, 0, data, p, 4);
        p += 7;

        // Entry 2: encode "abc".
        byte[] abcEncoded = encoder.EncodeForDictionary("abc");
        Array.Copy(abcEncoded, 0, data, p, 4);
        p += 7;

        var memory = new Memory();
        memory.LoadStory(data);
        var dict = new Dictionary(memory, 3, encoder);
        dict.Parse(dictAddr);

        return (dict, memory);
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
