namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for StoryInspector — verifies header field extraction, memory
/// map computation, header extension parsing, and interpreter info
/// against real story files and synthetic memory images.
/// </summary>
public class StoryInspectorTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";
    private const string BallyhooPath = "stories/ballyhoo.z3";
    private const string MindPath = "stories/mind.z4";
    private const string SherlockPath = "stories/sherlock.z5";
    private const string JourneyPath = "stories/Journey/STORY.DATA.z6";

    #region Header Fields — Zork I (V3)

    /// <summary>
    /// Verifies that Zork I is identified as version 3.
    /// </summary>
    [Fact]
    public void Zork1_Version_Is3()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        var version = fields.First(f => f.Name == "Version");
        Assert.Contains("3", version.DecodedValue);
    }

    /// <summary>
    /// Verifies that Zork I Flags 1 indicates a score game (not time).
    /// </summary>
    [Fact]
    public void Zork1_Flags1_IsScoreGame()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        var flags1 = fields.First(f => f.Name == "Flags 1");
        Assert.Contains("Score game", flags1.DecodedValue);
        Assert.DoesNotContain("Time game", flags1.DecodedValue);
    }

    /// <summary>
    /// Verifies that Zork I's checksum passes verification.
    /// </summary>
    [Fact]
    public void Zork1_Checksum_Verified()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        var checksum = fields.First(f => f.Name == "Checksum");
        Assert.Contains("verified", checksum.DecodedValue);
        Assert.DoesNotContain("MISMATCH", checksum.DecodedValue);
    }

    /// <summary>
    /// Verifies that Zork I has a six-character serial number.
    /// </summary>
    [Fact]
    public void Zork1_Serial_IsSixChars()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        var serial = fields.First(f => f.Name == "Serial number");
        Assert.Equal(6, serial.DecodedValue.Length);
    }

    /// <summary>
    /// Verifies that Zork I has nonzero addresses for dictionary,
    /// object table, globals, and abbreviations.
    /// </summary>
    [Fact]
    public void Zork1_TableAddresses_AreNonzero()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        foreach (var name in new[]
        {
            "Dictionary address", "Object table address",
            "Global variables address", "Abbreviations table address"
        })
        {
            var field = fields.First(f => f.Name == name);
            Assert.NotEqual("$0000", field.DecodedValue);
        }
    }

    /// <summary>
    /// Verifies that V3 header has no interpreter number/version fields.
    /// </summary>
    [Fact]
    public void Zork1_V3_NoInterpreterFields()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        Assert.DoesNotContain(fields, f => f.Name == "Interpreter number");
        Assert.DoesNotContain(fields, f => f.Name == "Interpreter version");
    }

    /// <summary>
    /// Verifies that Zork I's file length field decodes to a
    /// reasonable byte count.
    /// </summary>
    [Fact]
    public void Zork1_FileLength_IsReasonable()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var fields = inspector.GetHeaderFields();

        var fileLen = fields.First(f => f.Name == "File length");
        Assert.Contains("bytes", fileLen.DecodedValue);
    }

    #endregion

    #region Header Fields — Czech (V5)

    /// <summary>
    /// Verifies that Czech is identified as version 5.
    /// </summary>
    [Fact]
    public void Czech_Version_Is5()
    {
        if (!File.Exists(CzechPath)) return;
        var inspector = LoadInspector(CzechPath);
        var fields = inspector.GetHeaderFields();

        var version = fields.First(f => f.Name == "Version");
        Assert.Contains("5", version.DecodedValue);
    }

    /// <summary>
    /// Verifies that V5 header includes colour fields.
    /// </summary>
    [Fact]
    public void Czech_V5_HasColourFields()
    {
        if (!File.Exists(CzechPath)) return;
        var inspector = LoadInspector(CzechPath);
        var fields = inspector.GetHeaderFields();

        Assert.Contains(fields, f => f.Name == "Default background colour");
        Assert.Contains(fields, f => f.Name == "Default foreground colour");
    }

    /// <summary>
    /// Verifies that V5 header includes alphabet table field.
    /// </summary>
    [Fact]
    public void Czech_V5_HasAlphabetField()
    {
        if (!File.Exists(CzechPath)) return;
        var inspector = LoadInspector(CzechPath);
        var fields = inspector.GetHeaderFields();

        Assert.Contains(fields, f => f.Name == "Alphabet table address");
    }

    #endregion

    #region Header Fields — Multi-version coverage

    /// <summary>
    /// Verifies that a V4 story file includes interpreter number/version.
    /// </summary>
    [Fact]
    public void Mind_V4_HasInterpreterFields()
    {
        if (!File.Exists(MindPath)) return;
        var inspector = LoadInspector(MindPath);
        var fields = inspector.GetHeaderFields();

        Assert.Contains(fields, f => f.Name == "Interpreter number");
        Assert.Contains(fields, f => f.Name == "Interpreter version");
        Assert.Contains(fields, f => f.Name == "Screen height (lines)");
        Assert.Contains(fields, f => f.Name == "Screen width (chars)");
    }

    /// <summary>
    /// Verifies that a V5 story has a standard revision field.
    /// </summary>
    [Fact]
    public void Sherlock_V5_HasStandardRevision()
    {
        if (!File.Exists(SherlockPath)) return;
        var inspector = LoadInspector(SherlockPath);
        var fields = inspector.GetHeaderFields();

        var revision = fields.First(f => f.Name == "Standard revision");
        Assert.NotNull(revision.DecodedValue);
    }

    /// <summary>
    /// Verifies that a V6 story has routines and strings offset fields.
    /// </summary>
    [Fact]
    public void Journey_V6_HasOffsetFields()
    {
        if (!File.Exists(JourneyPath)) return;
        var inspector = LoadInspector(JourneyPath);
        var fields = inspector.GetHeaderFields();

        Assert.Contains(fields, f => f.Name == "Routines offset (÷8)");
        Assert.Contains(fields, f => f.Name == "Strings offset (÷8)");
    }

    /// <summary>
    /// Verifies that a V6 story's initial PC is decoded as a packed
    /// routine address.
    /// </summary>
    [Fact]
    public void Journey_V6_InitialPC_IsPacked()
    {
        if (!File.Exists(JourneyPath)) return;
        var inspector = LoadInspector(JourneyPath);
        var fields = inspector.GetHeaderFields();

        var pc = fields.First(f => f.Name == "Initial PC");
        Assert.Contains("Packed routine", pc.DecodedValue);
    }

    #endregion

    #region Memory Map

    /// <summary>
    /// Verifies that the memory map includes all required regions.
    /// </summary>
    [Fact]
    public void Zork1_MemoryMap_HasAllRegions()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var regions = inspector.GetMemoryMap();

        Assert.Contains(regions, r => r.Name == "Header");
        Assert.Contains(regions, r => r.Name == "Dynamic memory");
        Assert.Contains(regions, r => r.Name == "Static memory");
        Assert.Contains(regions, r => r.Name == "High memory");
        Assert.Contains(regions, r => r.Name == "Abbreviation table");
        Assert.Contains(regions, r => r.Name == "Object table");
        Assert.Contains(regions, r => r.Name == "Global variables");
        Assert.Contains(regions, r => r.Name == "Dictionary");
    }

    /// <summary>
    /// Verifies that the header region is always $00–$3F.
    /// </summary>
    [Fact]
    public void Zork1_MemoryMap_HeaderIs64Bytes()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var regions = inspector.GetMemoryMap();

        var header = regions.First(r => r.Name == "Header");
        Assert.Equal(0x00, header.Start);
        Assert.Equal(0x3F, header.End);
        Assert.Equal(64, header.Size);
    }

    /// <summary>
    /// Verifies that memory regions are sorted by start address.
    /// </summary>
    [Fact]
    public void Zork1_MemoryMap_SortedByAddress()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var regions = inspector.GetMemoryMap();

        for (int i = 1; i < regions.Count; i++)
            Assert.True(regions[i].Start >= regions[i - 1].Start,
                $"Region '{regions[i].Name}' at ${regions[i].Start:X4} is before " +
                $"'{regions[i - 1].Name}' at ${regions[i - 1].Start:X4}");
    }

    /// <summary>
    /// Verifies that dynamic memory ends just before static base.
    /// </summary>
    [Fact]
    public void Zork1_MemoryMap_DynamicEndsAtStaticBase()
    {
        if (!File.Exists(Zork1Path)) return;
        var memory = LoadMemory(Zork1Path);
        var inspector = new StoryInspector(memory);
        var regions = inspector.GetMemoryMap();

        var dynamic = regions.First(r => r.Name == "Dynamic memory");
        Assert.Equal(memory.StaticBase - 1, dynamic.End);
    }

    /// <summary>
    /// Verifies that global variables region is 240 words (480 bytes).
    /// </summary>
    [Fact]
    public void Zork1_MemoryMap_GlobalsIs480Bytes()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var regions = inspector.GetMemoryMap();

        var globals = regions.First(r => r.Name == "Global variables");
        Assert.Equal(480, globals.Size);
    }

    #endregion

    #region Header Extension

    /// <summary>
    /// Verifies that V3 stories have no header extension.
    /// </summary>
    [Fact]
    public void Zork1_V3_NoHeaderExtension()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var ext = inspector.GetHeaderExtension();

        Assert.Empty(ext);
    }

    /// <summary>
    /// Verifies header extension parsing for a V5 story that may
    /// have an extension table.
    /// </summary>
    [Fact]
    public void Czech_V5_HeaderExtension()
    {
        if (!File.Exists(CzechPath)) return;
        var memory = LoadMemory(CzechPath);
        var inspector = new StoryInspector(memory);

        int extAddr = memory.ReadWord(0x36);
        var ext = inspector.GetHeaderExtension();

        if (extAddr == 0)
            Assert.Empty(ext);
        else
            Assert.NotEmpty(ext);
    }

    #endregion

    #region Interpreter Info

    /// <summary>
    /// Verifies interpreter info includes version, standard revision,
    /// and checksum verification.
    /// </summary>
    [Fact]
    public void Zork1_InterpreterInfo_HasEssentialFields()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var info = inspector.GetInterpreterInfo();

        Assert.Contains(info, f => f.Name == "Z-Machine version");
        Assert.Contains(info, f => f.Name == "Standard revision");
        Assert.Contains(info, f => f.Name == "Checksum verification");
    }

    /// <summary>
    /// Verifies that Zork I checksum passes in interpreter info.
    /// </summary>
    [Fact]
    public void Zork1_InterpreterInfo_ChecksumPasses()
    {
        if (!File.Exists(Zork1Path)) return;
        var inspector = LoadInspector(Zork1Path);
        var info = inspector.GetInterpreterInfo();

        var checksum = info.First(f => f.Name == "Checksum verification");
        Assert.Contains("PASS", checksum.DecodedValue);
    }

    #endregion

    #region Synthetic Memory

    /// <summary>
    /// Verifies header decoding with a synthetic V5 memory image
    /// that has known field values.
    /// </summary>
    [Fact]
    public void Synthetic_V5_AllFieldsDecoded()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;                        // Version
        data[0x01] = 0x01;                     // Flags 1: colours
        data[0x02] = 0x00; data[0x03] = 42;   // Release 42
        data[0x04] = 0x80; data[0x05] = 0x00;  // High base $8000
        data[0x06] = 0x10; data[0x07] = 0x00;  // Initial PC $1000
        data[0x08] = 0x20; data[0x09] = 0x00;  // Dict $2000
        data[0x0A] = 0x01; data[0x0B] = 0x00;  // Obj table $0100
        data[0x0C] = 0x30; data[0x0D] = 0x00;  // Globals $3000
        data[0x0E] = 0x80; data[0x0F] = 0x00;  // Static base $8000
        data[0x12] = (byte)'2'; data[0x13] = (byte)'6';
        data[0x14] = (byte)'0'; data[0x15] = (byte)'9';
        data[0x16] = (byte)'0'; data[0x17] = (byte)'7';
        data[0x18] = 0x00; data[0x19] = 0x40;  // Abbr $0040
        data[0x1E] = 6;                        // Interpreter: IBM PC
        data[0x1F] = (byte)'A';                // Interpreter version
        data[0x20] = 25;                       // Screen height
        data[0x21] = 80;                       // Screen width
        data[0x2C] = 9;                        // BG: white
        data[0x2D] = 2;                        // FG: black
        data[0x32] = 1; data[0x33] = 1;       // Standard 1.1

        var memory = new Memory();
        memory.LoadStory(data);
        var inspector = new StoryInspector(memory);
        var fields = inspector.GetHeaderFields();

        Assert.Contains(fields, f => f.Name == "Version"
            && f.DecodedValue.Contains("5"));
        Assert.Contains(fields, f => f.Name == "Serial number"
            && f.DecodedValue == "260907");
        Assert.Contains(fields, f => f.Name == "Interpreter number"
            && f.DecodedValue.Contains("IBM PC"));
        Assert.Contains(fields, f => f.Name == "Default background colour"
            && f.DecodedValue.Contains("white"));
        Assert.Contains(fields, f => f.Name == "Default foreground colour"
            && f.DecodedValue.Contains("black"));
        Assert.Contains(fields, f => f.Name == "Standard revision"
            && f.DecodedValue == "1.1");
    }

    /// <summary>
    /// Verifies header extension decoding with synthetic memory that
    /// has a Flags 3 entry.
    /// </summary>
    [Fact]
    public void Synthetic_V5_HeaderExtension_Flags3()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;
        data[0x04] = 0x80; data[0x05] = 0x00;
        data[0x0E] = 0x80; data[0x0F] = 0x00;

        // Header extension at $0100
        data[0x36] = 0x01; data[0x37] = 0x00; // ext table at $0100
        data[0x0100] = 0x00; data[0x0101] = 0x04; // 4 words
        // Word 4 (Flags 3) at $0108
        data[0x0108] = 0x00; data[0x0109] = 0x01; // transparency

        var memory = new Memory();
        memory.LoadStory(data);
        var inspector = new StoryInspector(memory);
        var ext = inspector.GetHeaderExtension();

        Assert.NotEmpty(ext);
        var flags3 = ext.FirstOrDefault(f => f.Name == "Flags 3");
        Assert.NotNull(flags3);
        Assert.Contains("Transparency", flags3.DecodedValue);
    }

    /// <summary>
    /// Verifies checksum mismatch detection in interpreter info.
    /// </summary>
    [Fact]
    public void Synthetic_ChecksumMismatch_Detected()
    {
        var data = new byte[0x10000];
        data[0x00] = 5;
        data[0x04] = 0x80; data[0x05] = 0x00;
        data[0x0E] = 0x80; data[0x0F] = 0x00;
        data[0x1C] = 0xFF; data[0x1D] = 0xFF; // bogus checksum

        var memory = new Memory();
        memory.LoadStory(data);
        var inspector = new StoryInspector(memory);
        var fields = inspector.GetHeaderFields();

        var checksum = fields.First(f => f.Name == "Checksum");
        Assert.Contains("MISMATCH", checksum.DecodedValue);
    }

    #endregion

    #region Helpers

    /// <summary>Loads a Memory instance from a story file.</summary>
    private static Memory LoadMemory(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return memory;
    }

    /// <summary>Loads a StoryInspector for the given story file.</summary>
    private static StoryInspector LoadInspector(string path)
    {
        return new StoryInspector(LoadMemory(path));
    }

    #endregion
}
