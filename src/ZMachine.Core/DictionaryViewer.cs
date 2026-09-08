namespace ZMachine.Core;

/// <summary>
/// Read-only viewer for the Z-Machine dictionary. Extracts all entries
/// with their encoded form, decoded text, and game-specific data bytes.
/// Provides metadata, search, and sorting.
/// </summary>
/// <remarks>
/// ZSpec S13 — Dictionary layout: separators, entry length, entry count,
/// then entry_count × entry_length bytes of entries. Each entry starts
/// with encoded text (4 bytes V1-3, 6 bytes V4+), followed by
/// game-specific data.
/// </remarks>
public class DictionaryViewer
{
    private readonly Memory _memory;
    private readonly Dictionary _dictionary;
    private readonly TextDecoder _textDecoder;
    private readonly int _version;
    private readonly int _dictionaryAddress;

    /// <summary>
    /// Creates a viewer for the dictionary in the given memory image.
    /// </summary>
    public DictionaryViewer(Memory memory)
    {
        _memory = memory;
        _version = memory.Version;
        _dictionaryAddress = memory.ReadWord(0x08);

        int abbrAddr = memory.ReadWord(0x18);
        int alphabetAddr = _version >= 5 ? memory.ReadWord(0x34) : 0;

        _textDecoder = new TextDecoder(memory, _version, abbrAddr, alphabetAddr);

        var encoder = new TextEncoder(_version, alphabetAddr,
            alphabetAddr > 0 ? memory : null);
        _dictionary = new Dictionary(memory, _version, encoder);
        _dictionary.Parse(_dictionaryAddress);
    }

    /// <summary>
    /// Returns dictionary metadata: base address, separator characters,
    /// entry length, entry count, encoded text length, and sort status.
    /// </summary>
    public DictionaryMetadata GetMetadata()
    {
        return new DictionaryMetadata(
            _dictionaryAddress,
            new string(_dictionary.Separators),
            _dictionary.EntryLength,
            _dictionary.EntryCount,
            _dictionary.EncodedLength,
            _dictionary.IsSorted,
            _dictionary.EntriesStart);
    }

    /// <summary>
    /// Returns all dictionary entries with their index, address, encoded
    /// hex, decoded text, and game-specific data bytes.
    /// </summary>
    public List<DictionaryEntry> GetEntries()
    {
        var entries = new List<DictionaryEntry>(_dictionary.EntryCount);

        for (int i = 0; i < _dictionary.EntryCount; i++)
        {
            int addr = _dictionary.GetEntryAddress(i);
            entries.Add(ReadEntry(i, addr));
        }

        return entries;
    }

    /// <summary>
    /// Returns entries whose decoded text contains the query substring
    /// (case-insensitive).
    /// </summary>
    public List<DictionaryEntry> Search(string query)
    {
        var results = new List<DictionaryEntry>();

        for (int i = 0; i < _dictionary.EntryCount; i++)
        {
            int addr = _dictionary.GetEntryAddress(i);
            var entry = ReadEntry(i, addr);

            if (entry.DecodedText.Contains(query,
                StringComparison.OrdinalIgnoreCase))
                results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Returns all entries sorted by the specified field.
    /// </summary>
    public List<DictionaryEntry> GetEntriesSorted(DictionarySortField field,
        bool ascending = true)
    {
        var entries = GetEntries();

        entries.Sort((a, b) =>
        {
            int cmp = field switch
            {
                DictionarySortField.Address =>
                    a.Address.CompareTo(b.Address),
                DictionarySortField.DecodedText =>
                    string.Compare(a.DecodedText, b.DecodedText,
                        StringComparison.OrdinalIgnoreCase),
                DictionarySortField.EntryNumber =>
                    a.EntryNumber.CompareTo(b.EntryNumber),
                _ => 0
            };
            return ascending ? cmp : -cmp;
        });

        return entries;
    }

    /// <summary>
    /// Looks up a specific word and returns its entry, or null if not found.
    /// </summary>
    public DictionaryEntry? LookupWord(string word)
    {
        int addr = _dictionary.Lookup(word);
        if (addr == 0)
            return null;

        int index = (addr - _dictionary.EntriesStart) / _dictionary.EntryLength;
        return ReadEntry(index, addr);
    }

    #region Entry Reading

    /// <summary>
    /// Reads a single dictionary entry at the given address.
    /// </summary>
    private DictionaryEntry ReadEntry(int index, int addr)
    {
        int encLen = _dictionary.EncodedLength;

        // Read encoded bytes
        var encodedBytes = new byte[encLen];
        for (int j = 0; j < encLen; j++)
            encodedBytes[j] = _memory.ReadByte(addr + j);
        string encodedHex = BitConverter.ToString(encodedBytes)
            .Replace("-", " ");

        // Decode the Z-string at the entry address
        var (text, _) = _textDecoder.DecodeZString(addr);

        // Read game-specific data bytes after the encoded text
        int dataLen = _dictionary.EntryLength - encLen;
        var dataBytes = new byte[dataLen];
        for (int j = 0; j < dataLen; j++)
            dataBytes[j] = _memory.ReadByte(addr + encLen + j);
        string dataHex = dataLen > 0
            ? BitConverter.ToString(dataBytes).Replace("-", " ")
            : "";

        return new DictionaryEntry(index, addr, encodedHex, text,
            dataHex, dataBytes);
    }

    #endregion
}

/// <summary>
/// Dictionary metadata: base address, separators, entry layout, and
/// sort status.
/// </summary>
public record DictionaryMetadata(
    int BaseAddress,
    string Separators,
    int EntryLength,
    int EntryCount,
    int EncodedTextLength,
    bool IsSorted,
    int EntriesStartAddress);

/// <summary>
/// A single dictionary entry with its index, address, encoded form,
/// decoded text, and game-specific data.
/// </summary>
public record DictionaryEntry(
    int EntryNumber,
    int Address,
    string EncodedHex,
    string DecodedText,
    string DataHex,
    byte[] DataBytes);

/// <summary>
/// Fields by which dictionary entries can be sorted.
/// </summary>
public enum DictionarySortField
{
    /// <summary>Sort by byte address.</summary>
    Address,
    /// <summary>Sort by decoded text (alphabetical).</summary>
    DecodedText,
    /// <summary>Sort by entry number (original order).</summary>
    EntryNumber
}
