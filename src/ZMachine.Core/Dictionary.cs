namespace ZMachine.Core;

/// <summary>
/// Reads and searches a Z-Machine dictionary. The dictionary contains
/// word separators, an entry length, and a sorted list of encoded words
/// that can be searched via binary search.
/// </summary>
/// <remarks>
/// ZSpec S13 — Dictionary layout:
///   byte: number of word separators
///   n bytes: the separator characters (ZSCII codes)
///   byte: entry length (total bytes per entry, including encoded text)
///   word: entry count (signed — negative means unsorted)
///   entries: each entry_length bytes, starting with encoded text
///     (4 bytes V1-3, 6 bytes V4+), followed by game-specific data.
/// </remarks>
public class Dictionary
{
    private readonly Memory _memory;
    private readonly TextEncoder _encoder;
    private readonly int _version;

    /// <summary>Word separator characters (e.g. comma, period, quote).</summary>
    public char[] Separators { get; private set; } = [];

    /// <summary>Total bytes per dictionary entry.</summary>
    public int EntryLength { get; private set; }

    /// <summary>Number of entries in the dictionary.</summary>
    public int EntryCount { get; private set; }

    /// <summary>Whether entries are sorted (positive count = sorted).</summary>
    public bool IsSorted { get; private set; }

    /// <summary>Byte address of the first entry.</summary>
    public int EntriesStart { get; private set; }

    /// <summary>Number of encoded text bytes per entry (4 for V1-3, 6 for V4+).</summary>
    public int EncodedLength { get; private set; }

    public Dictionary(Memory memory, int version, TextEncoder encoder)
    {
        _memory = memory;
        _version = version;
        _encoder = encoder;
        EncodedLength = version <= 3 ? 4 : 6;
    }

    /// <summary>
    /// Parses the dictionary header at the given address, reading
    /// separators, entry length, entry count, and computing the
    /// entries start address.
    /// </summary>
    public void Parse(int dictionaryAddress)
    {
        int addr = dictionaryAddress;

        int nSeps = _memory.ReadByte(addr++);
        Separators = new char[nSeps];
        for (int i = 0; i < nSeps; i++)
            Separators[i] = (char)_memory.ReadByte(addr++);

        EntryLength = _memory.ReadByte(addr++);

        // ZSpec S13 — Entry count is a signed word. Negative means unsorted.
        int rawCount = _memory.ReadWord(addr);
        short signedCount = (short)rawCount;
        if (signedCount < 0)
        {
            EntryCount = -signedCount;
            IsSorted = false;
        }
        else
        {
            EntryCount = signedCount;
            IsSorted = true;
        }
        addr += 2;

        EntriesStart = addr;
    }

    /// <summary>
    /// Looks up a word in the dictionary, returning its byte address
    /// or 0 if not found. Uses binary search for sorted dictionaries,
    /// linear scan for unsorted.
    /// </summary>
    /// <remarks>
    /// ZSpec S13 — The encoded text at the start of each entry matches
    /// the output of TextEncoder.EncodeForDictionary.
    /// </remarks>
    public int Lookup(string word)
    {
        byte[] encoded = _encoder.EncodeForDictionary(word);
        return IsSorted
            ? BinarySearch(encoded)
            : LinearSearch(encoded);
    }

    /// <summary>
    /// Looks up pre-encoded bytes in the dictionary.
    /// </summary>
    public int LookupEncoded(byte[] encoded)
    {
        return IsSorted
            ? BinarySearch(encoded)
            : LinearSearch(encoded);
    }

    private int BinarySearch(byte[] encoded)
    {
        int lo = 0, hi = EntryCount - 1;

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int entryAddr = EntriesStart + mid * EntryLength;
            int cmp = CompareEntry(entryAddr, encoded);

            if (cmp == 0)
                return entryAddr;
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return 0;
    }

    private int LinearSearch(byte[] encoded)
    {
        for (int i = 0; i < EntryCount; i++)
        {
            int entryAddr = EntriesStart + i * EntryLength;
            if (CompareEntry(entryAddr, encoded) == 0)
                return entryAddr;
        }
        return 0;
    }

    /// <summary>
    /// Compares the encoded text at entryAddr with the given encoded bytes.
    /// Returns negative if entry &lt; encoded, 0 if equal, positive if entry &gt; encoded.
    /// </summary>
    private int CompareEntry(int entryAddr, byte[] encoded)
    {
        for (int i = 0; i < EncodedLength; i++)
        {
            int entryByte = _memory.ReadByte(entryAddr + i);
            int searchByte = i < encoded.Length ? encoded[i] : 0;
            if (entryByte != searchByte)
                return entryByte - searchByte;
        }
        return 0;
    }

    /// <summary>
    /// Returns the byte address of the entry at the given index.
    /// </summary>
    public int GetEntryAddress(int index)
    {
        return EntriesStart + index * EntryLength;
    }
}
