namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for the Header and HeaderExtension classes — field parsing,
/// capability negotiation, and standard revision stamping. Tested
/// against minizork.z3 (V3) and czech.z5 (V5, has extension table).
/// </summary>
public class HeaderTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MinizorkPath = Path.Combine(RepoRoot, "stories/minizork.z3");
    private static readonly string CzechPath = Path.Combine(RepoRoot, "stories/czech.z5");

    #region Minizork V3 Header Parsing

    [Fact]
    public void Minizork_Version()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(3, header.Version);
    }

    [Fact]
    public void Minizork_ReleaseNumber()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x0022, header.ReleaseNumber);
    }

    [Fact]
    public void Minizork_HighMemoryBase()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x3709, header.HighMemoryBase);
    }

    [Fact]
    public void Minizork_InitialPC()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x37D9, header.InitialPC);
    }

    [Fact]
    public void Minizork_DictionaryAddress()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x285A, header.DictionaryAddress);
    }

    [Fact]
    public void Minizork_ObjectTableAddress()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x03C6, header.ObjectTableAddress);
    }

    [Fact]
    public void Minizork_GlobalVariablesAddress()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x02B4, header.GlobalVariablesAddress);
    }

    [Fact]
    public void Minizork_StaticMemoryBase()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x2187, header.StaticMemoryBase);
    }

    [Fact]
    public void Minizork_SerialNumber()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal("871124", header.SerialNumber);
    }

    [Fact]
    public void Minizork_AbbreviationTableAddress()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0x01F4, header.AbbreviationTableAddress);
    }

    [Fact]
    public void Minizork_FileLength()
    {
        var header = LoadHeader(MinizorkPath);
        // Raw packed value
        Assert.Equal(0x65FC, header.FileLength);
    }

    [Fact]
    public void Minizork_Checksum()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0xD870, header.Checksum);
    }

    [Fact]
    public void Minizork_NoHeaderExtension()
    {
        var header = LoadHeader(MinizorkPath);
        Assert.Equal(0, header.HeaderExtensionAddress);
        Assert.Null(header.Extension);
    }

    #endregion

    #region Czech V5 Header Parsing

    [Fact]
    public void Czech_Version()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(5, header.Version);
    }

    [Fact]
    public void Czech_ReleaseNumber()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x0001, header.ReleaseNumber);
    }

    [Fact]
    public void Czech_InitialPC()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x07DD, header.InitialPC);
    }

    [Fact]
    public void Czech_DictionaryAddress()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x07D3, header.DictionaryAddress);
    }

    [Fact]
    public void Czech_ObjectTableAddress()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x010E, header.ObjectTableAddress);
    }

    [Fact]
    public void Czech_GlobalVariablesAddress()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x04F0, header.GlobalVariablesAddress);
    }

    [Fact]
    public void Czech_SerialNumber()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal("031102", header.SerialNumber);
    }

    [Fact]
    public void Czech_TerminatingCharsAddress()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x07D0, header.TerminatingCharsAddress);
    }

    [Fact]
    public void Czech_HasHeaderExtension()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0x0106, header.HeaderExtensionAddress);
        Assert.NotNull(header.Extension);
    }

    #endregion

    #region Header Extension

    [Fact]
    public void Czech_ExtensionWordCount()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(3, header.Extension!.WordCount);
    }

    [Fact]
    public void Czech_ExtensionUnicodeTable()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0, header.Extension!.UnicodeTableAddress);
    }

    [Fact]
    public void Czech_ExtensionFlags3()
    {
        var header = LoadHeader(CzechPath);
        Assert.Equal(0, header.Extension!.Flags3);
    }

    [Fact]
    public void Czech_ExtensionTrueDefaultForeground()
    {
        var header = LoadHeader(CzechPath);
        // Word count is 3, so word 3 (true fg) is present
        Assert.Equal(0, header.Extension!.TrueDefaultForeground);
    }

    [Fact]
    public void Czech_ExtensionTrueDefaultBackground_BeyondTableLength()
    {
        var header = LoadHeader(CzechPath);
        // Word count is 3, so word 4 (true bg) is beyond the table — returns 0
        Assert.Equal(0, header.Extension!.TrueDefaultBackground);
    }

    #endregion

    #region Capability Negotiation

    [Fact]
    public void ConfigureInterpreter_SetsStandardRevision()
    {
        var (memory, header) = LoadMemoryAndHeader(MinizorkPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.ScreenSplitting);

        // ZSpec S11 — Standard 1.1 = bytes $32=1, $33=1
        Assert.Equal(1, memory.ReadByte(0x32));
        Assert.Equal(1, memory.ReadByte(0x33));
    }

    [Fact]
    public void ConfigureInterpreter_SetsInterpreterNumber()
    {
        var (memory, header) = LoadMemoryAndHeader(MinizorkPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.ScreenSplitting);

        Assert.Equal(6, memory.ReadByte(0x1E));
        Assert.Equal((byte)'A', memory.ReadByte(0x1F));
    }

    [Fact]
    public void ConfigureInterpreter_SetsScreenDimensions()
    {
        var (memory, header) = LoadMemoryAndHeader(MinizorkPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.ScreenSplitting);

        Assert.Equal(25, memory.ReadByte(0x20));
        Assert.Equal(80, memory.ReadByte(0x21));
    }

    [Fact]
    public void ConfigureInterpreter_V3_SetsScreenSplittingBit()
    {
        var (memory, header) = LoadMemoryAndHeader(MinizorkPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.ScreenSplitting);

        // V3 Flags 1 bit 5 = screen splitting available
        byte flags1 = memory.ReadByte(0x01);
        Assert.True((flags1 & (1 << 5)) != 0, "Screen splitting bit should be set");
    }

    [Fact]
    public void ConfigureInterpreter_V3_ClearsStatusLineNotAvailable()
    {
        var (memory, header) = LoadMemoryAndHeader(MinizorkPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.ScreenSplitting);

        // V3 Flags 1 bit 4 = status line NOT available (should be clear)
        byte flags1 = memory.ReadByte(0x01);
        Assert.True((flags1 & (1 << 4)) == 0, "Status line not-available bit should be clear");
    }

    [Fact]
    public void ConfigureInterpreter_V5_SetsColorsBoldItalicFixed()
    {
        var (memory, header) = LoadMemoryAndHeader(CzechPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.Colors |
            InterpreterCapabilities.Bold |
            InterpreterCapabilities.Italic |
            InterpreterCapabilities.FixedSpace);

        byte flags1 = memory.ReadByte(0x01);
        Assert.True((flags1 & (1 << 0)) != 0, "Colors bit should be set");
        Assert.True((flags1 & (1 << 2)) != 0, "Bold bit should be set");
        Assert.True((flags1 & (1 << 3)) != 0, "Italic bit should be set");
        Assert.True((flags1 & (1 << 4)) != 0, "Fixed-space bit should be set");
    }

    [Fact]
    public void ConfigureInterpreter_V5_SetsScreenUnits()
    {
        var (memory, header) = LoadMemoryAndHeader(CzechPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.Colors);

        Assert.Equal(80, memory.ReadWord(0x22));
        Assert.Equal(25, memory.ReadWord(0x24));
    }

    [Fact]
    public void ConfigureInterpreter_V5_ClearsFlags3Reserved()
    {
        var (memory, header) = LoadMemoryAndHeader(CzechPath);

        // Set some reserved bits in Flags 3 to verify they get cleared
        // Flags 3 is at extension table + 4 bytes (word index 2)
        int flags3Addr = header.HeaderExtensionAddress + 4;
        memory.WriteWord(flags3Addr, 0xFFFE); // all bits set except bit 0

        // Re-parse to pick up the modified flags
        var header2 = new Header(memory);
        header2.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.Colors);

        // All reserved bits should be cleared, only bit 0 preserved if set
        ushort flags3 = memory.ReadWord(flags3Addr);
        Assert.Equal(0x0000, flags3);
    }

    [Fact]
    public void ConfigureInterpreter_NoCapabilities_ClearsAllFlags1Bits()
    {
        var (memory, header) = LoadMemoryAndHeader(CzechPath);

        header.ConfigureInterpreter(6, (byte)'A', 80, 25,
            InterpreterCapabilities.None);

        byte flags1 = memory.ReadByte(0x01);
        // All capability bits should be clear
        Assert.Equal(0, flags1 & 0x9F); // bits 0,1,2,3,4,7 mask
    }

    #endregion

    #region Synthetic Header Tests

    [Fact]
    public void Header_V6_RoutinesAndStringsOffset()
    {
        var memory = CreateSyntheticV6Memory(routinesOffset: 0x1234, stringsOffset: 0x5678);
        var header = new Header(memory);

        Assert.Equal(6, header.Version);
        Assert.Equal(0x1234, header.RoutinesOffset);
        Assert.Equal(0x5678, header.StringsOffset);
    }

    [Fact]
    public void Header_DefaultColors()
    {
        var data = CreateMinimalV5Data();
        data[0x2C] = 2; // bg = black
        data[0x2D] = 9; // fg = white

        var memory = new Memory();
        memory.LoadStory(data);
        var header = new Header(memory);

        Assert.Equal(2, header.DefaultBackgroundColor);
        Assert.Equal(9, header.DefaultForegroundColor);
    }

    #endregion

    #region Helpers

    private static Header LoadHeader(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return new Header(memory);
    }

    private static (Memory memory, Header header) LoadMemoryAndHeader(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        var header = new Header(memory);
        return (memory, header);
    }

    private static Memory CreateSyntheticV6Memory(ushort routinesOffset = 0, ushort stringsOffset = 0)
    {
        var data = new byte[512];
        data[0] = 6; // V6
        data[0x04] = 0x00; data[0x05] = 0x80; // high base
        data[0x0E] = 0x00; data[0x0F] = 0x40; // static base
        data[0x28] = (byte)(routinesOffset >> 8);
        data[0x29] = (byte)(routinesOffset & 0xFF);
        data[0x2A] = (byte)(stringsOffset >> 8);
        data[0x2B] = (byte)(stringsOffset & 0xFF);

        var memory = new Memory();
        memory.LoadStory(data);
        return memory;
    }

    private static byte[] CreateMinimalV5Data()
    {
        var data = new byte[256];
        data[0] = 5; // V5
        data[0x04] = 0x00; data[0x05] = 0x80; // high base
        data[0x0E] = 0x00; data[0x0F] = 0x40; // static base
        return data;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
