namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for DictionaryViewer — verifies metadata extraction, entry
/// decoding, search, sorting, and word lookup against real story files
/// and synthetic memory images.
/// </summary>
public class DictionaryViewerTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";
    private const string MindPath = "stories/mind.z4";
    private const string SherlockPath = "stories/sherlock.z5";

    #region Metadata

    /// <summary>
    /// Verifies that Zork I dictionary metadata has reasonable values.
    /// </summary>
    [Fact]
    public void Zork1_Metadata_IsReasonable()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var meta = viewer.GetMetadata();

        Assert.True(meta.BaseAddress > 0);
        Assert.True(meta.EntryCount > 500);
        Assert.True(meta.EntryLength > 0);
        Assert.Equal(4, meta.EncodedTextLength);
        Assert.True(meta.IsSorted);
    }

    /// <summary>
    /// Verifies that Zork I has standard word separators (space is
    /// implicit; separators include period, comma, and double-quote).
    /// </summary>
    [Fact]
    public void Zork1_Separators_IncludeStandard()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var meta = viewer.GetMetadata();

        Assert.Contains('.', meta.Separators);
        Assert.Contains(',', meta.Separators);
    }

    /// <summary>
    /// Verifies that V5 dictionary uses 6-byte encoded text.
    /// </summary>
    [Fact]
    public void Czech_V5_EncodedLength_Is6()
    {
        if (!File.Exists(CzechPath)) return;
        var viewer = CreateViewer(CzechPath);
        var meta = viewer.GetMetadata();

        Assert.Equal(6, meta.EncodedTextLength);
    }

    /// <summary>
    /// Verifies metadata entries start address is after the header.
    /// </summary>
    [Fact]
    public void Zork1_Metadata_EntriesStartAfterHeader()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var meta = viewer.GetMetadata();

        Assert.True(meta.EntriesStartAddress > meta.BaseAddress);
    }

    #endregion

    #region Known Words

    /// <summary>
    /// Verifies that "mailbox" appears in the Zork I dictionary.
    /// </summary>
    [Fact]
    public void Zork1_KnownWord_Mailbox()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("mailbo");

        Assert.NotEmpty(results);
        Assert.Contains(results, e => e.DecodedText.StartsWith("mailbo"));
    }

    /// <summary>
    /// Verifies that "open" appears in the Zork I dictionary.
    /// </summary>
    [Fact]
    public void Zork1_KnownWord_Open()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entry = viewer.LookupWord("open");

        Assert.NotNull(entry);
        Assert.Contains("open", entry.DecodedText);
    }

    /// <summary>
    /// Verifies that "take" appears in the Zork I dictionary.
    /// </summary>
    [Fact]
    public void Zork1_KnownWord_Take()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entry = viewer.LookupWord("take");

        Assert.NotNull(entry);
        Assert.Contains("take", entry.DecodedText);
    }

    /// <summary>
    /// Verifies that a nonexistent word returns null from lookup.
    /// </summary>
    [Fact]
    public void Zork1_LookupWord_NotFound()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entry = viewer.LookupWord("xyzzy");

        Assert.Null(entry);
    }

    #endregion

    #region Entry Content

    /// <summary>
    /// Verifies that all entries have non-empty decoded text.
    /// </summary>
    [Fact]
    public void Zork1_AllEntries_HaveText()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entries = viewer.GetEntries();

        Assert.All(entries, e => Assert.NotEmpty(e.DecodedText));
    }

    /// <summary>
    /// Verifies that all entries have non-empty encoded hex.
    /// </summary>
    [Fact]
    public void Zork1_AllEntries_HaveEncodedHex()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entries = viewer.GetEntries();

        Assert.All(entries, e => Assert.NotEmpty(e.EncodedHex));
    }

    /// <summary>
    /// Verifies that entry count matches the number of entries returned.
    /// </summary>
    [Fact]
    public void Zork1_EntryCount_MatchesEntries()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var meta = viewer.GetMetadata();
        var entries = viewer.GetEntries();

        Assert.Equal(meta.EntryCount, entries.Count);
    }

    /// <summary>
    /// Verifies that entries have game-specific data bytes.
    /// </summary>
    [Fact]
    public void Zork1_Entries_HaveDataBytes()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entries = viewer.GetEntries();

        Assert.All(entries, e =>
        {
            Assert.True(e.DataBytes.Length > 0,
                $"Entry '{e.DecodedText}' should have data bytes");
            Assert.NotEmpty(e.DataHex);
        });
    }

    /// <summary>
    /// Verifies that entry addresses increase monotonically.
    /// </summary>
    [Fact]
    public void Zork1_Entries_AddressesIncreasing()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var entries = viewer.GetEntries();

        for (int i = 1; i < entries.Count; i++)
            Assert.True(entries[i].Address > entries[i - 1].Address,
                $"Entry {i} address ${entries[i].Address:X4} should be > " +
                $"entry {i - 1} address ${entries[i - 1].Address:X4}");
    }

    #endregion

    #region Sorting

    /// <summary>
    /// Verifies that sorting by decoded text produces alphabetical order.
    /// </summary>
    [Fact]
    public void Zork1_SortByText_IsAlphabetical()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var sorted = viewer.GetEntriesSorted(DictionarySortField.DecodedText);

        for (int i = 1; i < sorted.Count; i++)
            Assert.True(
                string.Compare(sorted[i].DecodedText, sorted[i - 1].DecodedText,
                    StringComparison.OrdinalIgnoreCase) >= 0,
                $"'{sorted[i].DecodedText}' should follow " +
                $"'{sorted[i - 1].DecodedText}'");
    }

    /// <summary>
    /// Verifies that sorting by address produces increasing addresses.
    /// </summary>
    [Fact]
    public void Zork1_SortByAddress_IsIncreasing()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var sorted = viewer.GetEntriesSorted(DictionarySortField.Address);

        for (int i = 1; i < sorted.Count; i++)
            Assert.True(sorted[i].Address >= sorted[i - 1].Address);
    }

    /// <summary>
    /// Verifies that descending sort reverses the order.
    /// </summary>
    [Fact]
    public void Zork1_SortDescending_ReversesOrder()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var asc = viewer.GetEntriesSorted(DictionarySortField.EntryNumber, true);
        var desc = viewer.GetEntriesSorted(DictionarySortField.EntryNumber, false);

        Assert.Equal(asc.Count, desc.Count);
        Assert.Equal(asc[0].EntryNumber, desc[^1].EntryNumber);
        Assert.Equal(asc[^1].EntryNumber, desc[0].EntryNumber);
    }

    #endregion

    #region Search

    /// <summary>
    /// Verifies that search is case-insensitive.
    /// </summary>
    [Fact]
    public void Zork1_Search_CaseInsensitive()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);

        var upper = viewer.Search("OPEN");
        var lower = viewer.Search("open");

        Assert.Equal(upper.Count, lower.Count);
    }

    /// <summary>
    /// Verifies that searching for a nonexistent word returns empty.
    /// </summary>
    [Fact]
    public void Zork1_Search_NotFound()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("xyzzy99999");

        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that search finds partial matches.
    /// </summary>
    [Fact]
    public void Zork1_Search_PartialMatch()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("ope");

        Assert.NotEmpty(results);
        Assert.All(results, e =>
            Assert.Contains("ope", e.DecodedText,
                StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies that a V4 story dictionary loads and has entries.
    /// </summary>
    [Fact]
    public void Mind_V4_HasEntries()
    {
        if (!File.Exists(MindPath)) return;
        var viewer = CreateViewer(MindPath);
        var meta = viewer.GetMetadata();

        Assert.True(meta.EntryCount > 0);

        var entries = viewer.GetEntries();
        Assert.Equal(meta.EntryCount, entries.Count);
    }

    /// <summary>
    /// Verifies that a V5 story dictionary loads and has entries.
    /// </summary>
    [Fact]
    public void Sherlock_V5_HasEntries()
    {
        if (!File.Exists(SherlockPath)) return;
        var viewer = CreateViewer(SherlockPath);
        var entries = viewer.GetEntries();

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotEmpty(e.DecodedText));
    }

    #endregion

    #region Synthetic Memory

    /// <summary>
    /// Verifies dictionary parsing with a synthetic V3 dictionary
    /// containing two known words.
    /// </summary>
    [Fact]
    public void Synthetic_V3_TwoEntries()
    {
        var data = BuildSyntheticV3Dictionary();
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new DictionaryViewer(memory);

        var meta = viewer.GetMetadata();
        Assert.Equal(2, meta.EntryCount);
        Assert.Equal(4, meta.EncodedTextLength);
        Assert.True(meta.IsSorted);

        var entries = viewer.GetEntries();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.NotEmpty(e.EncodedHex));
    }

    /// <summary>
    /// Verifies that synthetic dictionary metadata reflects correct
    /// separator characters.
    /// </summary>
    [Fact]
    public void Synthetic_V3_Separators()
    {
        var data = BuildSyntheticV3Dictionary();
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new DictionaryViewer(memory);

        var meta = viewer.GetMetadata();
        Assert.Equal(".,\"", meta.Separators);
    }

    /// <summary>
    /// Verifies that synthetic dictionary search returns the right
    /// entry count.
    /// </summary>
    [Fact]
    public void Synthetic_V3_SearchAll()
    {
        var data = BuildSyntheticV3Dictionary();
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new DictionaryViewer(memory);

        var all = viewer.GetEntries();
        Assert.Equal(2, all.Count);
    }

    #endregion

    #region Helpers

    /// <summary>Creates a DictionaryViewer from a story file path.</summary>
    private static DictionaryViewer CreateViewer(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return new DictionaryViewer(memory);
    }

    /// <summary>
    /// Builds a synthetic V3 story with a minimal dictionary at $0200
    /// containing 2 entries with 3 separators (., ,, ").
    /// </summary>
    private static byte[] BuildSyntheticV3Dictionary()
    {
        var data = new byte[0x10000];
        data[0x00] = 3; // version
        data[0x04] = 0x80; data[0x05] = 0x00; // high base
        data[0x0E] = 0x80; data[0x0F] = 0x00; // static base

        // Abbreviation table at $0100
        data[0x18] = 0x01; data[0x19] = 0x00;

        // Object table at $0180 (minimal — just defaults)
        data[0x0A] = 0x01; data[0x0B] = 0x80;

        // Dictionary at $0200
        int dictAddr = 0x0200;
        data[0x08] = (byte)(dictAddr >> 8);
        data[0x09] = (byte)(dictAddr & 0xFF);

        int pos = dictAddr;

        // 3 separators: . , "
        data[pos++] = 3;
        data[pos++] = (byte)'.';
        data[pos++] = (byte)',';
        data[pos++] = (byte)'"';

        // Entry length: 7 (4 encoded + 3 data)
        data[pos++] = 7;

        // Entry count: 2 (sorted, positive)
        data[pos++] = 0x00;
        data[pos++] = 0x02;

        // Entry 1: encoded Z-string "a" (pad with 5s, set high bit on last word)
        // Z-char 'a' = 6 in A0. Word 1: 0_00110_00101_00101 = $18A5
        // Word 2 (last): 1_00101_00101_00101 = $94A5
        data[pos++] = 0x18; data[pos++] = 0xA5;
        data[pos++] = 0x94; data[pos++] = 0xA5;
        data[pos++] = 0x01; data[pos++] = 0x02; data[pos++] = 0x03; // data

        // Entry 2: encoded Z-string "b"
        // Z-char 'b' = 7. Word 1: 0_00111_00101_00101 = $1CA5
        // Word 2 (last): 1_00101_00101_00101 = $94A5
        data[pos++] = 0x1C; data[pos++] = 0xA5;
        data[pos++] = 0x94; data[pos++] = 0xA5;
        data[pos++] = 0x04; data[pos++] = 0x05; data[pos++] = 0x06; // data

        return data;
    }

    #endregion
}
