namespace ZMachine.Core;

/// <summary>
/// Extracts and formats metadata from a loaded story file: header fields,
/// memory map, header extension table, and interpreter capabilities. All
/// data is read-only — no live memory is modified.
/// </summary>
/// <remarks>
/// ZSpec S11 — Header layout ($00–$3F).
/// ZSpec11 "Header Extension" — Extension table at address in $36.
/// </remarks>
public class StoryInspector
{
    private readonly Memory _memory;

    /// <summary>
    /// Creates an inspector for the given loaded memory image.
    /// </summary>
    public StoryInspector(Memory memory)
    {
        _memory = memory;
    }

    #region Header Fields

    /// <summary>
    /// Returns all standard header fields as a list of (name, address,
    /// raw hex, decoded value) tuples.
    /// </summary>
    public List<HeaderField> GetHeaderFields()
    {
        var fields = new List<HeaderField>();
        int v = _memory.Version;

        fields.Add(Field("Version", 0x00, 1, $"Z-Machine Version {v}"));
        fields.Add(Field("Flags 1", 0x01, 1, DecodeFlags1(v)));
        fields.Add(Field("Release number", 0x02, 2));
        fields.Add(Field("High memory base", 0x04, 2, $"${W(0x04):X4}"));
        fields.Add(Field("Initial PC", 0x06, 2,
            v == 6 ? $"Packed routine ${W(0x06):X4}" : $"${W(0x06):X4}"));
        fields.Add(Field("Dictionary address", 0x08, 2, $"${W(0x08):X4}"));
        fields.Add(Field("Object table address", 0x0A, 2, $"${W(0x0A):X4}"));
        fields.Add(Field("Global variables address", 0x0C, 2, $"${W(0x0C):X4}"));
        fields.Add(Field("Static memory base", 0x0E, 2, $"${W(0x0E):X4}"));
        fields.Add(Field("Flags 2", 0x10, 2, DecodeFlags2(v)));
        fields.Add(Field("Serial number", 0x12, 6, DecodeSerial()));

        fields.Add(Field("Abbreviations table address", 0x18, 2, $"${W(0x18):X4}"));
        fields.Add(Field("File length", 0x1A, 2, DecodeFileLength(v)));
        fields.Add(Field("Checksum", 0x1C, 2, DecodeChecksum()));

        if (v >= 4)
        {
            fields.Add(Field("Interpreter number", 0x1E, 1,
                DecodeInterpreterNumber(B(0x1E))));
            fields.Add(Field("Interpreter version", 0x1F, 1, $"{B(0x1F)}"));
        }

        if (v >= 4)
        {
            fields.Add(Field("Screen height (lines)", 0x20, 1));
            fields.Add(Field("Screen width (chars)", 0x21, 1));
        }

        if (v >= 5)
        {
            fields.Add(Field("Screen width (units)", 0x22, 2));
            fields.Add(Field("Screen height (units)", 0x24, 2));
            fields.Add(Field("Font width (V5) / height (V6)", 0x26, 1));
            fields.Add(Field("Font height (V5) / width (V6)", 0x27, 1));
        }

        if (v >= 6)
        {
            fields.Add(Field("Routines offset (÷8)", 0x28, 2));
            fields.Add(Field("Strings offset (÷8)", 0x2A, 2));
        }

        if (v >= 5)
        {
            fields.Add(Field("Default background colour", 0x2C, 1,
                DecodeColour(B(0x2C))));
            fields.Add(Field("Default foreground colour", 0x2D, 1,
                DecodeColour(B(0x2D))));
        }

        if (v >= 5)
        {
            fields.Add(Field("Terminating chars table address", 0x2E, 2,
                W(0x2E) == 0 ? "None" : $"${W(0x2E):X4}"));
        }

        if (v >= 6)
        {
            fields.Add(Field("Output stream 3 width (pixels)", 0x30, 2));
        }

        fields.Add(Field("Standard revision", 0x32, 2,
            $"{B(0x32)}.{B(0x33)}"));

        if (v >= 5)
        {
            fields.Add(Field("Alphabet table address", 0x34, 2,
                W(0x34) == 0 ? "Default" : $"${W(0x34):X4}"));
            fields.Add(Field("Header extension table address", 0x36, 2,
                W(0x36) == 0 ? "None" : $"${W(0x36):X4}"));
        }

        return fields;
    }

    #endregion

    #region Header Extension

    /// <summary>
    /// Returns header extension fields, or an empty list if no extension
    /// table is present.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Header Extension" — The extension table is at the address
    /// in header word $36. Word 0 is the number of further words. Words
    /// 1–4 are defined by Standard 1.1.
    /// </remarks>
    public List<HeaderField> GetHeaderExtension()
    {
        var fields = new List<HeaderField>();
        int addr = W(0x36);
        if (addr == 0)
            return fields;

        int wordCount = _memory.ReadWord(addr);
        fields.Add(new HeaderField("Extension word count", addr, 2,
            $"{FormatHex(addr, 2)}", $"{wordCount}"));

        if (wordCount >= 1)
        {
            int mouseX = _memory.ReadWord(addr + 2);
            int mouseY = _memory.ReadWord(addr + 4);
            fields.Add(new HeaderField("Mouse click X coordinate", addr + 2, 2,
                $"{FormatHex(addr + 2, 2)}", $"{mouseX}"));
            if (wordCount >= 2)
                fields.Add(new HeaderField("Mouse click Y coordinate", addr + 4, 2,
                    $"{FormatHex(addr + 4, 2)}", $"{mouseY}"));
        }

        if (wordCount >= 3)
        {
            int unicodeAddr = _memory.ReadWord(addr + 6);
            fields.Add(new HeaderField("Unicode translation table", addr + 6, 2,
                $"{FormatHex(addr + 6, 2)}",
                unicodeAddr == 0 ? "None" : $"${unicodeAddr:X4}"));
        }

        if (wordCount >= 4)
        {
            int flags3 = _memory.ReadWord(addr + 8);
            fields.Add(new HeaderField("Flags 3", addr + 8, 2,
                $"{FormatHex(addr + 8, 2)}", DecodeFlags3(flags3)));
        }

        if (wordCount >= 5)
        {
            int trueFg = _memory.ReadWord(addr + 10);
            fields.Add(new HeaderField("True default foreground", addr + 10, 2,
                $"{FormatHex(addr + 10, 2)}", DecodeTrueColour(trueFg)));
        }

        if (wordCount >= 6)
        {
            int trueBg = _memory.ReadWord(addr + 12);
            fields.Add(new HeaderField("True default background", addr + 12, 2,
                $"{FormatHex(addr + 12, 2)}", DecodeTrueColour(trueBg)));
        }

        return fields;
    }

    #endregion

    #region Memory Map

    /// <summary>
    /// Returns the memory map as a list of named regions with start
    /// address, end address, and size.
    /// </summary>
    public List<MemoryRegion> GetMemoryMap()
    {
        var regions = new List<MemoryRegion>();
        int fileLen = _memory.FileLength > 0
            ? _memory.FileLength
            : _memory.OriginalBytes.Length;

        regions.Add(new MemoryRegion("Header", 0x00, 0x3F));
        regions.Add(new MemoryRegion("Dynamic memory", 0x00,
            _memory.StaticBase - 1));
        regions.Add(new MemoryRegion("Static memory", _memory.StaticBase,
            Math.Min(_memory.HighBase - 1, fileLen - 1)));
        regions.Add(new MemoryRegion("High memory", _memory.HighBase,
            fileLen - 1));

        int abbrAddr = W(0x18);
        if (abbrAddr != 0)
            regions.Add(new MemoryRegion("Abbreviation table", abbrAddr,
                EstimateAbbrEnd(abbrAddr)));

        int objAddr = W(0x0A);
        if (objAddr != 0)
            regions.Add(new MemoryRegion("Object table", objAddr,
                EstimateObjEnd(objAddr)));

        int globAddr = W(0x0C);
        if (globAddr != 0)
            regions.Add(new MemoryRegion("Global variables", globAddr,
                globAddr + 480 - 1));

        int dictAddr = W(0x08);
        if (dictAddr != 0)
            regions.Add(new MemoryRegion("Dictionary", dictAddr,
                EstimateDictEnd(dictAddr)));

        int initPC = W(0x06);
        if (_memory.Version != 6 && initPC > 0)
            regions.Add(new MemoryRegion("Code start (initial PC)", initPC,
                initPC));

        regions.Sort((a, b) => a.Start.CompareTo(b.Start));
        return regions;
    }

    #endregion

    #region Interpreter Info

    /// <summary>
    /// Returns a summary of interpreter capabilities and settings
    /// as reported via the header.
    /// </summary>
    public List<HeaderField> GetInterpreterInfo()
    {
        var fields = new List<HeaderField>();
        int v = _memory.Version;

        fields.Add(new HeaderField("Z-Machine version", 0x00, 1, "", $"{v}"));
        fields.Add(new HeaderField("Standard revision", 0x32, 2, "",
            $"{B(0x32)}.{B(0x33)}"));

        if (v >= 4)
        {
            fields.Add(new HeaderField("Interpreter number", 0x1E, 1, "",
                DecodeInterpreterNumber(B(0x1E))));
            fields.Add(new HeaderField("Interpreter version", 0x1F, 1, "",
                $"{B(0x1F)} ({(char)(B(0x1F) >= (byte)'A' && B(0x1F) <= (byte)'Z' ? (char)B(0x1F) : '?')})"));
        }

        fields.Add(new HeaderField("Checksum verification", 0x1C, 2, "",
            _memory.ComputeChecksum() == _memory.HeaderChecksum
                ? "PASS (matches)" : "FAIL (mismatch)"));

        if (v >= 4)
        {
            fields.Add(new HeaderField("Screen size", 0x20, 2, "",
                $"{B(0x21)} x {B(0x20)} chars"));
        }

        if (v >= 5)
        {
            byte flags2Low = B(0x11);
            fields.Add(new HeaderField("Transcription requested", 0x11, 1, "",
                (flags2Low & 0x01) != 0 ? "Yes" : "No"));
            fields.Add(new HeaderField("Fixed-pitch requested", 0x11, 1, "",
                (flags2Low & 0x08) != 0 ? "Yes" : "No"));
        }

        return fields;
    }

    #endregion

    #region Formatting Helpers

    /// <summary>Reads a byte from the story header.</summary>
    private byte B(int addr) => _memory.ReadByte(addr);

    /// <summary>Reads a word from the story header.</summary>
    private ushort W(int addr) => _memory.ReadWord(addr);

    private HeaderField Field(string name, int addr, int size,
        string? decoded = null)
    {
        string hex = FormatHex(addr, size);
        return new HeaderField(name, addr, size, hex,
            decoded ?? (size == 1 ? $"{B(addr)}" : $"{W(addr)}"));
    }

    private string FormatHex(int addr, int size)
    {
        if (size == 1)
            return $"${B(addr):X2}";
        if (size == 2)
            return $"${W(addr):X4}";
        if (size == 6)
        {
            var chars = new char[6];
            for (int i = 0; i < 6; i++)
                chars[i] = (char)B(addr + i);
            return new string(chars);
        }
        return "";
    }

    private string DecodeSerial()
    {
        var chars = new char[6];
        for (int i = 0; i < 6; i++)
        {
            byte b = B(0x12 + i);
            chars[i] = b is >= 0x20 and <= 0x7E ? (char)b : '?';
        }
        return new string(chars);
    }

    private string DecodeFileLength(int version)
    {
        ushort packed = W(0x1A);
        int actual = _memory.FileLength;
        return $"${packed:X4} (packed) = {actual} bytes";
    }

    private string DecodeChecksum()
    {
        ushort stored = _memory.HeaderChecksum;
        ushort computed = _memory.ComputeChecksum();
        return stored == computed
            ? $"${stored:X4} (verified)"
            : $"${stored:X4} (MISMATCH: computed ${computed:X4})";
    }

    private string DecodeFlags1(int version)
    {
        byte f = B(0x01);
        var bits = new List<string>();

        if (version <= 3)
        {
            if ((f & 0x02) != 0) bits.Add("Time game");
            else bits.Add("Score game");
            if ((f & 0x04) != 0) bits.Add("Story file split");
            if ((f & 0x08) != 0) bits.Add("Tandy bit");
            if ((f & 0x10) != 0) bits.Add("Status unavailable");
            if ((f & 0x20) != 0) bits.Add("Screen split available");
            if ((f & 0x40) != 0) bits.Add("Default variable-pitch");
        }
        else
        {
            if ((f & 0x01) != 0) bits.Add("Colours available");
            if ((f & 0x02) != 0) bits.Add("Picture display available");
            if ((f & 0x04) != 0) bits.Add("Bold available");
            if ((f & 0x08) != 0) bits.Add("Italic available");
            if ((f & 0x10) != 0) bits.Add("Fixed-pitch available");
            if ((f & 0x20) != 0) bits.Add("Sound available");
            if ((f & 0x80) != 0) bits.Add("Timed input available");
        }

        return bits.Count > 0
            ? $"${f:X2} ({string.Join(", ", bits)})"
            : $"${f:X2} (none)";
    }

    private string DecodeFlags2(int version)
    {
        ushort f = W(0x10);
        byte fLow = B(0x11);
        var bits = new List<string>();

        if ((fLow & 0x01) != 0) bits.Add("Transcripting on");
        if ((fLow & 0x02) != 0) bits.Add("Fixed-pitch forced");
        if ((fLow & 0x04) != 0) bits.Add("Screen redraw needed");
        if ((fLow & 0x08) != 0) bits.Add("Fixed-pitch requested");

        if (version >= 5)
        {
            byte fHigh = B(0x10);
            if ((fHigh & 0x01) != 0) bits.Add("Pictures requested");
            if ((fHigh & 0x02) != 0) bits.Add("Undo requested");
            if ((fHigh & 0x04) != 0) bits.Add("Mouse requested");
            if ((fHigh & 0x08) != 0) bits.Add("Colours requested");
            if ((fHigh & 0x10) != 0) bits.Add("Sound requested");
            if ((fHigh & 0x20) != 0) bits.Add("Menus requested");
        }

        return bits.Count > 0
            ? $"${f:X4} ({string.Join(", ", bits)})"
            : $"${f:X4} (none)";
    }

    private static string DecodeFlags3(int flags3)
    {
        var bits = new List<string>();
        if ((flags3 & 0x01) != 0) bits.Add("Transparency available");
        return bits.Count > 0
            ? $"${flags3:X4} ({string.Join(", ", bits)})"
            : $"${flags3:X4} (none)";
    }

    private static string DecodeInterpreterNumber(int num) => num switch
    {
        1 => "1 (DECSystem-20)",
        2 => "2 (Apple IIe)",
        3 => "3 (Macintosh)",
        4 => "4 (Amiga)",
        5 => "5 (Atari ST)",
        6 => "6 (IBM PC)",
        7 => "7 (Commodore 128)",
        8 => "8 (Commodore 64)",
        9 => "9 (Apple IIc)",
        10 => "10 (Apple IIgs)",
        11 => "11 (Tandy Color)",
        _ => $"{num} (unknown)"
    };

    private static string DecodeColour(int colour) => colour switch
    {
        0 => "0 (current)",
        1 => "1 (default)",
        2 => "2 (black)",
        3 => "3 (red)",
        4 => "4 (green)",
        5 => "5 (yellow)",
        6 => "6 (blue)",
        7 => "7 (magenta)",
        8 => "8 (cyan)",
        9 => "9 (white)",
        10 => "10 (light grey)",
        11 => "11 (medium grey)",
        12 => "12 (dark grey)",
        _ => $"{colour}"
    };

    private static string DecodeTrueColour(int rgb)
    {
        if (rgb == 0xFFFE) return "$FFFE (default)";
        if (rgb == 0xFFFF) return "$FFFF (current)";
        int b = (rgb >> 10) & 0x1F;
        int g = (rgb >> 5) & 0x1F;
        int r = rgb & 0x1F;
        return $"${rgb:X4} (R={r} G={g} B={b})";
    }

    #endregion

    #region Size Estimators

    /// <summary>
    /// Estimates the end of the abbreviation table. The table has
    /// 32 (V1-2) or 96 (V3+) word-sized pointers.
    /// </summary>
    private int EstimateAbbrEnd(int addr)
    {
        int count = _memory.Version <= 2 ? 32 : 96;
        return addr + count * 2 - 1;
    }

    /// <summary>
    /// Estimates the end of the object table by scanning objects until
    /// the property data would overlap the next structure.
    /// </summary>
    private int EstimateObjEnd(int addr)
    {
        int entrySize = _memory.Version <= 3 ? 9 : 14;
        int propDefaultsSize = _memory.Version <= 3 ? 31 * 2 : 63 * 2;
        int firstObjAddr = addr + propDefaultsSize;

        // Read the first object's property table address to estimate count.
        int propPtr = _memory.Version <= 3
            ? _memory.ReadWord(firstObjAddr + 7)
            : _memory.ReadWord(firstObjAddr + 12);

        if (propPtr <= firstObjAddr)
            return firstObjAddr + entrySize * 10;

        int objCount = (propPtr - firstObjAddr) / entrySize;
        return firstObjAddr + objCount * entrySize - 1;
    }

    /// <summary>
    /// Estimates the end of the dictionary by reading its structure.
    /// </summary>
    private int EstimateDictEnd(int addr)
    {
        int pos = addr;
        int sepCount = _memory.ReadByte(pos++);
        pos += sepCount;
        int entryLen = _memory.ReadByte(pos++);
        int entryCount = (short)_memory.ReadWord(pos);
        pos += 2;
        if (entryCount < 0) entryCount = -entryCount;
        return pos + entryCount * entryLen - 1;
    }

    #endregion
}

/// <summary>
/// A single decoded header field with its name, memory address, raw
/// hex representation, and human-readable decoded value.
/// </summary>
public record HeaderField(
    string Name,
    int Address,
    int Size,
    string RawHex,
    string DecodedValue);

/// <summary>
/// A named memory region with start address, end address, and computed size.
/// </summary>
public record MemoryRegion(string Name, int Start, int End)
{
    /// <summary>Size of the region in bytes.</summary>
    public int Size => End - Start + 1;
}
