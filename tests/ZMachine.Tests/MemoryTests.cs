namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for the Memory class — story file loading, memory region
/// boundaries, read/write operations, and static write protection.
/// Uses minizork.z3 and czech.z5 as real-world story files.
/// </summary>
public class MemoryTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MinizorkPath = Path.Combine(RepoRoot, "stories/minizork.z3");
    private static readonly string CzechPath = Path.Combine(RepoRoot, "stories/czech.z5");

    /// <summary>
    /// Walk up from the test assembly directory to find the solution root
    /// (identified by the .slnx file).
    /// </summary>
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

    #region Loading and Header Parsing

    [Fact]
    public void LoadStory_Minizork_ParsesVersionCorrectly()
    {
        var mem = LoadMinizork();
        Assert.Equal(3, mem.Version);
    }

    [Fact]
    public void LoadStory_Czech_ParsesVersionCorrectly()
    {
        var mem = LoadCzech();
        Assert.Equal(5, mem.Version);
    }

    [Fact]
    public void LoadStory_Minizork_ParsesStaticBase()
    {
        var mem = LoadMinizork();
        Assert.Equal(0x2187, mem.StaticBase);
    }

    [Fact]
    public void LoadStory_Czech_ParsesStaticBase()
    {
        var mem = LoadCzech();
        Assert.Equal(0x07D1, mem.StaticBase);
    }

    [Fact]
    public void LoadStory_Minizork_ParsesHighBase()
    {
        var mem = LoadMinizork();
        Assert.Equal(0x3709, mem.HighBase);
    }

    [Fact]
    public void LoadStory_Minizork_ParsesFileLength()
    {
        var mem = LoadMinizork();
        // V3: packed value 0x65FC × 2 = 52216
        Assert.Equal(52216, mem.FileLength);
    }

    [Fact]
    public void LoadStory_Czech_ParsesFileLength()
    {
        var mem = LoadCzech();
        // V5: packed value 0x0CCF × 4 = 13116
        Assert.Equal(13116, mem.FileLength);
    }

    [Fact]
    public void LoadStory_Minizork_ParsesChecksum()
    {
        var mem = LoadMinizork();
        Assert.Equal((ushort)0xD870, mem.HeaderChecksum);
    }

    [Fact]
    public void LoadStory_Minizork_SizeMatchesFile()
    {
        var mem = LoadMinizork();
        Assert.Equal(52216, mem.Size);
    }

    [Fact]
    public void LoadStory_DynamicBaseIsAlwaysZero()
    {
        var mem = LoadMinizork();
        Assert.Equal(0, mem.DynamicBase);
    }

    #endregion

    #region Checksum

    [Fact]
    public void ComputeChecksum_Minizork_MatchesHeader()
    {
        var mem = LoadMinizork();
        Assert.Equal(mem.HeaderChecksum, mem.ComputeChecksum());
    }

    [Fact]
    public void ComputeChecksum_Czech_MatchesHeader()
    {
        var mem = LoadCzech();
        Assert.Equal(mem.HeaderChecksum, mem.ComputeChecksum());
    }

    #endregion

    #region Read Operations

    [Fact]
    public void ReadByte_ReturnsCorrectValue()
    {
        var mem = LoadMinizork();
        // Byte 0 is the version number
        Assert.Equal(3, mem.ReadByte(0));
    }

    [Fact]
    public void ReadWord_BigEndian_ReturnsCorrectValue()
    {
        var mem = LoadMinizork();
        // Word at $04 is the high memory base = 0x3709
        Assert.Equal((ushort)0x3709, mem.ReadWord(0x04));
    }

    [Fact]
    public void ReadByte_OutOfBounds_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<ArgumentOutOfRangeException>(() => mem.ReadByte(mem.Size));
    }

    [Fact]
    public void ReadWord_OutOfBounds_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<ArgumentOutOfRangeException>(() => mem.ReadWord(mem.Size - 1));
    }

    [Fact]
    public void ReadByte_NegativeAddress_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<ArgumentOutOfRangeException>(() => mem.ReadByte(-1));
    }

    #endregion

    #region Write Operations — Dynamic Memory

    [Fact]
    public void WriteByte_InDynamicMemory_Succeeds()
    {
        var mem = LoadMinizork();
        byte original = mem.ReadByte(0x10);
        mem.WriteByte(0x10, 0xAB);
        Assert.Equal(0xAB, mem.ReadByte(0x10));

        // Restore for good measure
        mem.WriteByte(0x10, original);
    }

    [Fact]
    public void WriteWord_InDynamicMemory_Succeeds()
    {
        var mem = LoadMinizork();
        mem.WriteWord(0x10, 0x1234);
        Assert.Equal((ushort)0x1234, mem.ReadWord(0x10));
    }

    [Fact]
    public void WriteWord_BigEndian_StoresCorrectByteOrder()
    {
        var mem = LoadMinizork();
        mem.WriteWord(0x10, 0xABCD);
        Assert.Equal(0xAB, mem.ReadByte(0x10));
        Assert.Equal(0xCD, mem.ReadByte(0x11));
    }

    #endregion

    #region Static Memory Write Protection

    [Fact]
    public void WriteByte_AtStaticBase_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<InvalidOperationException>(
            () => mem.WriteByte(mem.StaticBase, 0xFF));
    }

    [Fact]
    public void WriteByte_AboveStaticBase_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<InvalidOperationException>(
            () => mem.WriteByte(mem.StaticBase + 1, 0xFF));
    }

    [Fact]
    public void WriteWord_AtStaticBase_Throws()
    {
        var mem = LoadMinizork();
        Assert.Throws<InvalidOperationException>(
            () => mem.WriteWord(mem.StaticBase, 0xFFFF));
    }

    [Fact]
    public void WriteWord_SpanningStaticBoundary_Throws()
    {
        var mem = LoadMinizork();
        // Write a word at StaticBase-1: first byte is dynamic, second is static
        Assert.Throws<InvalidOperationException>(
            () => mem.WriteWord(mem.StaticBase - 1, 0xFFFF));
    }

    [Fact]
    public void WriteByte_JustBelowStaticBase_Succeeds()
    {
        var mem = LoadMinizork();
        mem.WriteByte(mem.StaticBase - 1, 0xFF);
        Assert.Equal(0xFF, mem.ReadByte(mem.StaticBase - 1));
    }

    #endregion

    #region Original Bytes and Restart

    [Fact]
    public void OriginalBytes_PreservesInitialState()
    {
        var mem = LoadMinizork();
        byte original = mem.OriginalBytes[0x10];

        mem.WriteByte(0x10, (byte)(original ^ 0xFF));
        Assert.NotEqual(original, mem.ReadByte(0x10));
        Assert.Equal(original, mem.OriginalBytes[0x10]);
    }

    [Fact]
    public void RestoreDynamicMemory_RevertsChanges()
    {
        var mem = LoadMinizork();
        byte original = mem.ReadByte(0x10);

        mem.WriteByte(0x10, (byte)(original ^ 0xFF));
        Assert.NotEqual(original, mem.ReadByte(0x10));

        mem.RestoreDynamicMemory();
        Assert.Equal(original, mem.ReadByte(0x10));
    }

    [Fact]
    public void OriginalBytes_IsIndependentCopy()
    {
        var mem = LoadMinizork();
        byte original = mem.OriginalBytes[0x10];
        byte flipped = (byte)(original ^ 0xFF);

        mem.WriteByte(0x10, flipped);
        Assert.Equal(flipped, mem.ReadByte(0x10));
        Assert.Equal(original, mem.OriginalBytes[0x10]);
    }

    #endregion

    #region Validation

    [Fact]
    public void LoadStory_TooSmall_Throws()
    {
        var mem = new Memory();
        Assert.Throws<InvalidOperationException>(
            () => mem.LoadStory(new byte[63]));
    }

    [Fact]
    public void LoadStory_InvalidVersion0_Throws()
    {
        var data = new byte[64];
        data[0] = 0; // version 0
        data[0x0E] = 0x00; data[0x0F] = 0x20; // static base at 0x20
        var mem = new Memory();
        Assert.Throws<InvalidOperationException>(() => mem.LoadStory(data));
    }

    [Fact]
    public void LoadStory_InvalidVersion9_Throws()
    {
        var data = new byte[64];
        data[0] = 9; // version 9
        data[0x0E] = 0x00; data[0x0F] = 0x20; // static base at 0x20
        var mem = new Memory();
        Assert.Throws<InvalidOperationException>(() => mem.LoadStory(data));
    }

    [Fact]
    public void LoadStory_ZeroStaticBase_Throws()
    {
        var data = new byte[128];
        data[0] = 3; // V3
        data[0x0E] = 0x00; data[0x0F] = 0x00; // static base = 0
        var mem = new Memory();
        Assert.Throws<InvalidOperationException>(() => mem.LoadStory(data));
    }

    [Fact]
    public void LoadStory_StaticBaseBeyondFile_Throws()
    {
        var data = new byte[128];
        data[0] = 3; // V3
        data[0x0E] = 0xFF; data[0x0F] = 0xFF; // static base = 65535, beyond file
        var mem = new Memory();
        Assert.Throws<InvalidOperationException>(() => mem.LoadStory(data));
    }

    [Fact]
    public void LoadStory_SyntheticV3_LoadsCorrectly()
    {
        var data = new byte[256];
        data[0] = 3; // V3
        data[0x04] = 0x00; data[0x05] = 0x80; // high base at 0x80
        data[0x0E] = 0x00; data[0x0F] = 0x40; // static base at 0x40
        // File length: 256 / 2 = 128 packed
        data[0x1A] = 0x00; data[0x1B] = 0x80;

        var mem = new Memory();
        mem.LoadStory(data);

        Assert.Equal(3, mem.Version);
        Assert.Equal(0x40, mem.StaticBase);
        Assert.Equal(0x80, mem.HighBase);
        Assert.Equal(256, mem.FileLength);
        Assert.Equal(256, mem.Size);
    }

    [Fact]
    public void LoadStory_FromPath_LoadsSuccessfully()
    {
        var mem = new Memory();
        mem.LoadStory(MinizorkPath);
        Assert.Equal(3, mem.Version);
        Assert.Equal(52216, mem.Size);
    }

    [Fact]
    public void LoadStory_NonexistentPath_Throws()
    {
        var mem = new Memory();
        Assert.Throws<FileNotFoundException>(
            () => mem.LoadStory(Path.Combine(RepoRoot, "stories/nonexistent.z3")));
    }

    #endregion

    #region RawBytes

    [Fact]
    public void RawBytes_ReflectsCurrentState()
    {
        var mem = LoadMinizork();
        mem.WriteByte(0x10, 0xAB);
        Assert.Equal(0xAB, mem.RawBytes[0x10]);
    }

    [Fact]
    public void RawBytes_SpanCoversEntireFile()
    {
        var mem = LoadMinizork();
        Assert.Equal(mem.Size, mem.RawBytes.Length);
    }

    #endregion

    #region Helpers

    private static Memory LoadMinizork()
    {
        var mem = new Memory();
        mem.LoadStory(MinizorkPath);
        return mem;
    }

    private static Memory LoadCzech()
    {
        var mem = new Memory();
        mem.LoadStory(CzechPath);
        return mem;
    }

    #endregion
}
