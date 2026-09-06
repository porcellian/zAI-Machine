namespace ZMachine.Core;

/// <summary>
/// Parses the optional header extension table (V5+). The table starts
/// with a word count, followed by that many additional words containing
/// Unicode translation table address, Flags 3, and (Standard 1.1)
/// true default colors.
/// </summary>
/// <remarks>
/// ZSpec S11 — Header extension table format.
/// ZSpec11 "Header Extension" — Words 4–6 (Flags 3, true colors).
/// </remarks>
public class HeaderExtension
{
    /// <summary>Byte address of the extension table in memory.</summary>
    public int TableAddress { get; }

    /// <summary>Number of additional words in the table (word 0).</summary>
    public int WordCount { get; }

    /// <summary>
    /// Address of the Unicode translation table (word 1).
    /// Zero if no custom table is present.
    /// </summary>
    public ushort UnicodeTableAddress { get; }

    /// <summary>
    /// Flags 3 bitfield (word 2, but at table index 4 = extension word 4
    /// in the spec's numbering from the base header extension table).
    /// Bit 0: game wants transparency (V6).
    /// All reserved bits must be cleared by the interpreter.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Header Extension" — Word 4 contains Flags 3.
    /// The spec numbers extension words starting from the header extension
    /// table base; word 0 = count, words 1+ are the data.
    /// However, the ZSpec11 table labels them as "Word 4", "Word 5", "Word 6"
    /// counting from a conceptual extended header. In the actual table,
    /// these are at offsets 1, 2, 3 (after the count word), corresponding
    /// to extension table words 4, 5, 6 in the spec numbering.
    /// We store them by their table offset for clarity.
    /// </remarks>
    public ushort Flags3 { get; private set; }

    /// <summary>
    /// True default foreground color in 15-bit sRGB format (word 3).
    /// Zero if not specified. ZSpec11 "Header Extension" word 5.
    /// </summary>
    public ushort TrueDefaultForeground { get; }

    /// <summary>
    /// True default background color in 15-bit sRGB format (word 4).
    /// Zero if not specified. ZSpec11 "Header Extension" word 6.
    /// </summary>
    public ushort TrueDefaultBackground { get; }

    /// <summary>
    /// Parses the header extension table from memory at the given address.
    /// </summary>
    public HeaderExtension(Memory memory, int address)
    {
        TableAddress = address;
        WordCount = memory.ReadWord(address);

        // Each word beyond word 0 is at address + 2*(index)
        // Only read words that actually exist in the table.
        UnicodeTableAddress = ReadExtensionWord(memory, 1);

        // ZSpec11 numbering: extension word 4 = Flags 3, at table offset 2
        // The extension table word numbering in ZSpec11 starts at 4 because
        // words 0-3 are conceptually the base extension words. In the actual
        // table: word 0 = count, data words start at offset 1.
        // Word 4 in spec = table data offset 1? No...
        //
        // Actually: the "header extension table" in ZSpec S11 has this layout:
        //   Word 0: number of further words
        //   Word 1: address of Unicode translation table
        //   Word 2: Flags 3
        //   Word 3: true default foreground
        //   Word 4: true default background
        // The ZSpec11 amendments label these as "Word 4", "Word 5", "Word 6"
        // because they count from the START of the conceptual header
        // (header bytes 0-63 are words 0-31, extension words continue from
        // there). But in the actual table data, they are at indices 2, 3, 4.
        Flags3 = ReadExtensionWord(memory, 2);
        TrueDefaultForeground = ReadExtensionWord(memory, 3);
        TrueDefaultBackground = ReadExtensionWord(memory, 4);
    }

    /// <summary>
    /// Clears all reserved bits in Flags 3, keeping only defined bits.
    /// ZSpec11 requires interpreters to clear all reserved bits.
    /// </summary>
    public void ClearReservedFlags3Bits(Memory memory)
    {
        if (WordCount < 2)
            return;

        // Only bit 0 (transparency) is defined in Standard 1.1
        const ushort definedBitsMask = 0x0001;
        ushort cleaned = (ushort)(Flags3 & definedBitsMask);

        memory.WriteWord(TableAddress + 2 * 2, cleaned);
        Flags3 = cleaned;
    }

    /// <summary>
    /// Reads a word from the extension table at the given 1-based index,
    /// returning 0 if the table is shorter than that index.
    /// </summary>
    private ushort ReadExtensionWord(Memory memory, int index)
    {
        if (index > WordCount)
            return 0;
        return memory.ReadWord(TableAddress + index * 2);
    }
}
