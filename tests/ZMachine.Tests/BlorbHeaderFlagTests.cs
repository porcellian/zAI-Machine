namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests for Task 10.3 — Blorb resource discovery and header flag integration.
/// Verifies that the interpreter sets/clears Flags 1 capability bits and
/// Flags 2 request bits based on Blorb resource availability.
/// ZSpec11 "Header capabilities bits".
/// </summary>
public class BlorbHeaderFlagTests
{
    private static readonly string CzechPath = ResolvePath("stories/czech.z5");

    private static string ResolvePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return relativePath;
    }

    #region PictureCount / SoundCount

    /// <summary>
    /// BlorbReader with picture resources reports correct PictureCount.
    /// </summary>
    [Fact]
    public void BlorbReader_WithPictures_PictureCountCorrect()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        var blorb = BuildResourceBlorb(pictures: 3, sounds: 0);

        Assert.Equal(3, blorb.PictureCount);
        Assert.Equal(0, blorb.SoundCount);
    }

    /// <summary>
    /// BlorbReader with sound resources reports correct SoundCount.
    /// </summary>
    [Fact]
    public void BlorbReader_WithSounds_SoundCountCorrect()
    {
        var blorb = BuildResourceBlorb(pictures: 0, sounds: 2);

        Assert.Equal(0, blorb.PictureCount);
        Assert.Equal(2, blorb.SoundCount);
    }

    /// <summary>
    /// BlorbReader with both picture and sound resources reports both counts.
    /// </summary>
    [Fact]
    public void BlorbReader_WithBoth_CountsCorrect()
    {
        var blorb = BuildResourceBlorb(pictures: 2, sounds: 3);

        Assert.Equal(2, blorb.PictureCount);
        Assert.Equal(3, blorb.SoundCount);
    }

    /// <summary>
    /// BlorbReader with no picture/sound resources reports zero counts.
    /// </summary>
    [Fact]
    public void BlorbReader_NoResources_ZeroCounts()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        var blorb = BuildBlorbWithZcod(storyData);

        Assert.Equal(0, blorb.PictureCount);
        Assert.Equal(0, blorb.SoundCount);
    }

    #endregion

    #region Flags 1 — Interpreter Capability Bits

    /// <summary>
    /// V5 story with Blorb containing pictures: Flags 1 bit 1 (0x02) set.
    /// ZSpec11 — Flags 1 bit 1 = picture display available.
    /// </summary>
    [Fact]
    public void Flags1_BlorbWithPictures_PictureBitSet()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 2, sounds: 0);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.True((flags1 & 0x02) != 0, "Flags 1 bit 1 (pictures) should be set");
    }

    /// <summary>
    /// V5 story with Blorb containing sounds: Flags 1 bit 5 (0x20) set.
    /// ZSpec11 — Flags 1 bit 5 = sound effects available.
    /// </summary>
    [Fact]
    public void Flags1_BlorbWithSounds_SoundBitSet()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 0, sounds: 3);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.True((flags1 & 0x20) != 0, "Flags 1 bit 5 (sound) should be set");
    }

    /// <summary>
    /// V5 story with no Blorb: Flags 1 picture and sound bits cleared.
    /// </summary>
    [Fact]
    public void Flags1_NoBlorb_BitsCleared()
    {
        if (!File.Exists(CzechPath)) return;
        var machine = new Interpreter();
        machine.Load(File.ReadAllBytes(CzechPath),
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.True((flags1 & 0x02) == 0, "Flags 1 bit 1 (pictures) should be cleared");
        Assert.True((flags1 & 0x20) == 0, "Flags 1 bit 5 (sound) should be cleared");
    }

    /// <summary>
    /// V5 story with Blorb containing no picture resources: Flags 1 bit 1 cleared.
    /// </summary>
    [Fact]
    public void Flags1_BlorbNoPictures_PictureBitCleared()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 0, sounds: 1);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.True((flags1 & 0x02) == 0, "Flags 1 bit 1 (pictures) should be cleared");
        Assert.True((flags1 & 0x20) != 0, "Flags 1 bit 5 (sound) should be set");
    }

    /// <summary>
    /// V5 story with Blorb containing both: both Flags 1 bits set.
    /// </summary>
    [Fact]
    public void Flags1_BlorbWithBoth_BothBitsSet()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 1, sounds: 1);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.True((flags1 & 0x02) != 0, "Flags 1 bit 1 (pictures) should be set");
        Assert.True((flags1 & 0x20) != 0, "Flags 1 bit 5 (sound) should be set");
    }

    #endregion

    #region Flags 2 — Game Request Bits

    /// <summary>
    /// V5 story requesting pictures (Flags 2 $10 bit 0) but no Blorb pictures:
    /// the request bit is cleared. ZSpec11 — "clear if no resources loaded".
    /// </summary>
    [Fact]
    public void Flags2_RequestsPictures_NoPictures_BitCleared()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);

        // Set game request bit for pictures in story data
        storyData[0x10] |= 0x01;

        var blorb = BuildBlorbWithZcod(storyData);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags2High = machine.Memory.ReadByte(0x10);
        Assert.True((flags2High & 0x01) == 0,
            "Flags 2 $10 bit 0 (pictures requested) should be cleared");
    }

    /// <summary>
    /// V5 story requesting sound (Flags 2 $10 bit 4) but no Blorb sounds:
    /// the request bit is cleared.
    /// </summary>
    [Fact]
    public void Flags2_RequestsSound_NoSound_BitCleared()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);

        // Set game request bit for sound
        storyData[0x10] |= 0x10;

        var blorb = BuildBlorbWithZcod(storyData);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags2High = machine.Memory.ReadByte(0x10);
        Assert.True((flags2High & 0x10) == 0,
            "Flags 2 $10 bit 4 (sound requested) should be cleared");
    }

    /// <summary>
    /// V5 story requesting pictures with Blorb that has pictures:
    /// the request bit is preserved (not cleared).
    /// </summary>
    [Fact]
    public void Flags2_RequestsPictures_HasPictures_BitPreserved()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);

        storyData[0x10] |= 0x01;

        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 2, sounds: 0);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags2High = machine.Memory.ReadByte(0x10);
        Assert.True((flags2High & 0x01) != 0,
            "Flags 2 $10 bit 0 (pictures requested) should be preserved");
    }

    /// <summary>
    /// V5 story requesting sound with Blorb that has sounds:
    /// the request bit is preserved.
    /// </summary>
    [Fact]
    public void Flags2_RequestsSound_HasSound_BitPreserved()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);

        storyData[0x10] |= 0x10;

        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 0, sounds: 1);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        byte flags2High = machine.Memory.ReadByte(0x10);
        Assert.True((flags2High & 0x10) != 0,
            "Flags 2 $10 bit 4 (sound requested) should be preserved");
    }

    #endregion

    #region Blorb Warnings

    /// <summary>
    /// V5 story requesting pictures but no Blorb loaded: warning generated.
    /// ZSpec11 — interpreter should prompt for Blorb on startup.
    /// </summary>
    [Fact]
    public void Warning_RequestsPictures_NoBlorb()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        storyData[0x10] |= 0x01;

        var machine = new Interpreter();
        machine.Load(storyData,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        Assert.Contains(machine.BlorbWarnings,
            w => w.Contains("pictures") && w.Contains("no Blorb"));
    }

    /// <summary>
    /// V5 story requesting sound but no Blorb loaded: warning generated.
    /// </summary>
    [Fact]
    public void Warning_RequestsSound_NoBlorb()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        storyData[0x10] |= 0x10;

        var machine = new Interpreter();
        machine.Load(storyData,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        Assert.Contains(machine.BlorbWarnings,
            w => w.Contains("sound") && w.Contains("no Blorb"));
    }

    /// <summary>
    /// V5 story requesting both pictures and sound but no Blorb:
    /// single warning mentioning both.
    /// </summary>
    [Fact]
    public void Warning_RequestsBoth_NoBlorb()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        storyData[0x10] |= 0x11;

        var machine = new Interpreter();
        machine.Load(storyData,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        Assert.Contains(machine.BlorbWarnings,
            w => w.Contains("pictures and sound") && w.Contains("no Blorb"));
    }

    /// <summary>
    /// V5 story with Blorb that has resources: no warnings.
    /// </summary>
    [Fact]
    public void Warning_BlorbPresent_NoWarnings()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        storyData[0x10] |= 0x11;

        var blorb = BuildBlorbWithZcodAndResources(storyData, pictures: 1, sounds: 1);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        Assert.Empty(machine.BlorbWarnings);
    }

    /// <summary>
    /// V5 story with no resource request bits set: no warnings even without Blorb.
    /// </summary>
    [Fact]
    public void Warning_NoRequests_NoWarnings()
    {
        if (!File.Exists(CzechPath)) return;
        byte[] storyData = File.ReadAllBytes(CzechPath);
        // Ensure request bits are clear
        storyData[0x10] &= unchecked((byte)~0x11);

        var machine = new Interpreter();
        machine.Load(storyData,
            new ScriptedInputStream(["quit"]),
            new CaptureScreen());

        Assert.Empty(machine.BlorbWarnings);
    }

    #endregion

    #region Version Guards

    /// <summary>
    /// V3 story: header flags are not modified (V4+ only).
    /// </summary>
    [Fact]
    public void Flags_V3Story_NotModified()
    {
        var minizorkPath = ResolvePath("stories/minizork.z3");
        if (!File.Exists(minizorkPath)) return;

        byte[] storyData = File.ReadAllBytes(minizorkPath);
        byte originalFlags1 = storyData[0x01];

        var machine = new Interpreter();
        machine.Load(storyData,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        byte flags1 = machine.Memory.ReadByte(0x01);
        Assert.Equal(originalFlags1, flags1);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds a BlorbReader with N picture and M sound resource chunks
    /// (no executable). Used for PictureCount/SoundCount tests.
    /// </summary>
    private static BlorbReader BuildResourceBlorb(int pictures, int sounds)
    {
        var entries = new List<(string Usage, int Number, int ChunkIndex)>();
        var chunks = new List<IffChunk>();

        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        for (int i = 0; i < pictures; i++)
        {
            entries.Add((BlorbUsage.Picture, i + 1, chunks.Count));
            chunks.Add(new IffChunk("PNG ", png));
        }

        byte[] ogg = [0x4F, 0x67, 0x67, 0x53];
        for (int i = 0; i < sounds; i++)
        {
            entries.Add((BlorbUsage.Sound, i + 1, chunks.Count));
            chunks.Add(new IffChunk("OGGV", ogg));
        }

        byte[] bytes = BuildBlorbBytes(
            [.. entries],
            [.. chunks]);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a BlorbReader containing a ZCOD executable from story data.
    /// </summary>
    private static BlorbReader BuildBlorbWithZcod(byte[] storyData)
    {
        byte[] bytes = BuildBlorbBytesWithZcod(storyData, []);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a BlorbReader with ZCOD executable plus picture/sound resources.
    /// </summary>
    private static BlorbReader BuildBlorbWithZcodAndResources(
        byte[] storyData, int pictures, int sounds)
    {
        var entries = new List<(string Usage, int Number, int ChunkIndex)>();
        var chunks = new List<IffChunk>();

        // Exec resource first
        entries.Add((BlorbUsage.Executable, 0, 0));
        chunks.Add(new IffChunk("ZCOD", storyData));

        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        for (int i = 0; i < pictures; i++)
        {
            entries.Add((BlorbUsage.Picture, i + 1, chunks.Count));
            chunks.Add(new IffChunk("PNG ", png));
        }

        byte[] ogg = [0x4F, 0x67, 0x67, 0x53];
        for (int i = 0; i < sounds; i++)
        {
            entries.Add((BlorbUsage.Sound, i + 1, chunks.Count));
            chunks.Add(new IffChunk("OGGV", ogg));
        }

        byte[] bytes = BuildBlorbBytes(
            [.. entries],
            [.. chunks]);
        return BlorbReader.Load(bytes);
    }

    private static byte[] BuildBlorbBytesWithZcod(byte[] storyData, IffChunk[] extraChunks)
    {
        var zcodChunk = new IffChunk("ZCOD", storyData);
        return BuildBlorbBytes(
            [(BlorbUsage.Executable, 0, 0)],
            [zcodChunk],
            extraChunks);
    }

    private static byte[] BuildBlorbBytes(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks,
        IffChunk[]? extraChunks = null)
    {
        int ridxDataLen = 4 + entries.Length * 12;
        int offset = 12 + 8 + ridxDataLen;

        int[] chunkOffsets = new int[resourceChunks.Length];
        for (int i = 0; i < resourceChunks.Length; i++)
        {
            chunkOffsets[i] = offset;
            uint chunkLen = resourceChunks[i].Length;
            offset += 8 + (int)chunkLen;
            if (chunkLen % 2 != 0)
                offset++;
        }

        byte[] ridxData = new byte[ridxDataLen];
        WriteInt32BE(ridxData, 0, entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            int entryOff = 4 + i * 12;
            WriteAscii(ridxData, entryOff, entries[i].Usage);
            WriteInt32BE(ridxData, entryOff + 4, entries[i].Number);
            WriteInt32BE(ridxData, entryOff + 8, chunkOffsets[entries[i].ChunkIndex]);
        }

        var allChunks = new List<IffChunk> { new("RIdx", ridxData) };
        allChunks.AddRange(resourceChunks);
        if (extraChunks != null)
            allChunks.AddRange(extraChunks);

        return IffWriter.WriteToArray("IFRS", allChunks);
    }

    private static void WriteInt32BE(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteAscii(byte[] buf, int offset, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, 0, buf, offset, 4);
    }

    #endregion
}
