namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests for Blorb story loading — extracting Z-code from Blorb files,
/// metadata parsing, IFhd validation, and extension detection.
/// Blorb "Executable Resource Chunks", "The Game Identifier Chunk".
/// </summary>
public class BlorbStoryLoadingTests
{
    private static readonly string MinizorkPath = ResolvePath("stories/minizork.z3");

    /// <summary>
    /// Resolves a relative path from the solution root by walking up
    /// from the test assembly's output directory.
    /// </summary>
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

    #region ZCOD Extraction

    /// <summary>
    /// Verifies that a Z-code story embedded in a Blorb file loads
    /// and produces identical memory to loading the raw .z file.
    /// </summary>
    [Fact]
    public void LoadBlorb_ZcodExec_IdenticalToRawStory()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);

        // Build a Blorb containing the story as ZCOD
        var blorb = BuildBlorbWithZcod(storyData);

        var rawMachine = new Interpreter();
        rawMachine.Load(storyData,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        var blorbMachine = new Interpreter();
        blorbMachine.Load(blorb,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        // Memory should be identical
        Assert.Equal(rawMachine.Memory.Size, blorbMachine.Memory.Size);
        Assert.Equal(rawMachine.Memory.Version, blorbMachine.Memory.Version);
        Assert.Equal(rawMachine.Memory.StaticBase, blorbMachine.Memory.StaticBase);
        Assert.Equal(
            rawMachine.Memory.OriginalBytes,
            blorbMachine.Memory.OriginalBytes);
    }

    /// <summary>
    /// Verifies that loading a Blorb with ZCOD sets the Blorb property.
    /// </summary>
    [Fact]
    public void LoadBlorb_ZcodExec_BlorbPropertySet()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        var blorb = BuildBlorbWithZcod(storyData);

        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        Assert.NotNull(machine.Blorb);
        Assert.True(machine.Blorb.HasResource(BlorbUsage.Executable, 0));
    }

    /// <summary>
    /// Verifies that a Blorb-loaded story can execute and produce output.
    /// </summary>
    [Fact]
    public void LoadBlorb_ZcodExec_CanExecute()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        var blorb = BuildBlorbWithZcod(storyData);

        var screen = new CaptureScreen();
        var machine = new Interpreter();
        machine.Load(blorb,
            new ScriptedInputStream(["quit", "y"]),
            screen);

        int limit = 1_000_000;
        while (machine.Running && --limit > 0)
            machine.Step();

        Assert.True(screen.Output.Length > 0);
    }

    #endregion

    #region Resource-Only Blorb (Standalone Story + Blorb)

    /// <summary>
    /// Verifies that a resource-only Blorb (no exec) paired with a
    /// standalone story file loads correctly.
    /// </summary>
    [Fact]
    public void LoadBlorb_ResourceOnly_WithStandaloneStory_Works()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);

        // Build a Blorb with no exec, just a picture
        var blorb = BuildResourceOnlyBlorb();

        var machine = new Interpreter();
        machine.Load(blorb, storyData,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        Assert.NotNull(machine.Blorb);
        Assert.Equal(storyData, machine.Memory.OriginalBytes);
    }

    /// <summary>
    /// Helper: loads a resource-only Blorb with standalone story bytes.
    /// Uses the internal LoadFromBlorb path via the BlorbReader overload.
    /// </summary>
    private static void LoadWithStandaloneStory(Interpreter machine,
        BlorbReader blorb, byte[] storyData,
        IInputStream input, IScreen screen)
    {
        // Use reflection-free approach: the Load(storyPath, blorbPath) overload
        // reads from files. For testing, we need the BlorbReader + byte[] path.
        // We'll test via the file-based overload indirectly, and via the
        // BlorbReader overload for the exec-only case.
        // For this test, we add an overload test helper.
    }

    #endregion

    #region Conflicting Executable Validation

    /// <summary>
    /// Verifies that providing both a Blorb with an executable AND a
    /// standalone story throws.
    /// Blorb "Executable Resource Chunks" — conflicting executable.
    /// </summary>
    [Fact]
    public void LoadBlorb_ExecPlusStandaloneStory_Throws()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        var blorb = BuildBlorbWithZcod(storyData);

        var machine = new Interpreter();
        Assert.Throws<InvalidOperationException>(() =>
            machine.Load(blorb, storyData,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen()));
    }

    /// <summary>
    /// Verifies that a Blorb without an executable AND no standalone
    /// story throws.
    /// </summary>
    [Fact]
    public void LoadBlorb_NoExecNoStory_Throws()
    {
        var blorb = BuildResourceOnlyBlorb();

        var machine = new Interpreter();
        Assert.Throws<InvalidOperationException>(() =>
            machine.Load(blorb,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen()));
    }

    /// <summary>
    /// Verifies that a non-ZCOD executable type throws.
    /// </summary>
    [Fact]
    public void LoadBlorb_NonZcodExec_Throws()
    {
        byte[] glulxData = new byte[64];
        glulxData[0] = 0x47; // 'G' — not a valid Z-Machine version
        var blorb = BuildBlorbWithExec(glulxData, "GLUL");

        var machine = new Interpreter();
        Assert.Throws<InvalidOperationException>(() =>
            machine.Load(blorb,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen()));
    }

    #endregion

    #region IFhd Validation

    /// <summary>
    /// Verifies that a resource-only Blorb with matching IFhd loads
    /// successfully with a standalone story.
    /// </summary>
    [Fact]
    public void LoadBlorb_MatchingIFhd_LoadsSuccessfully()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] ifhd = BuildIFhd(storyData);
        var blorb = BuildResourceOnlyBlorbWithIFhd(ifhd);

        var machine = new Interpreter();
        machine.Load(blorb, storyData,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        Assert.NotNull(machine.Blorb);
    }

    /// <summary>
    /// Verifies that a resource-only Blorb with mismatching release
    /// number throws during loading.
    /// </summary>
    [Fact]
    public void LoadBlorb_MismatchedIFhdRelease_Throws()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] ifhd = BuildIFhd(storyData);
        // Corrupt the release number
        ifhd[0] = 0xFF;
        ifhd[1] = 0xFF;
        var blorb = BuildResourceOnlyBlorbWithIFhd(ifhd);

        var machine = new Interpreter();
        Assert.Throws<InvalidOperationException>(() =>
            machine.Load(blorb, storyData,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen()));
    }

    /// <summary>
    /// Verifies that a resource-only Blorb with mismatching serial throws.
    /// </summary>
    [Fact]
    public void LoadBlorb_MismatchedIFhdSerial_Throws()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] ifhd = BuildIFhd(storyData);
        ifhd[2] = (byte)'X'; // corrupt serial
        var blorb = BuildResourceOnlyBlorbWithIFhd(ifhd);

        var machine = new Interpreter();
        Assert.Throws<InvalidOperationException>(() =>
            machine.Load(blorb, storyData,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen()));
    }

    #endregion

    #region Extension Detection

    /// <summary>
    /// Verifies that .zblorb extension triggers Blorb loading.
    /// </summary>
    [Fact]
    public void Load_ZblorbExtension_LoadsAsBlorb()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] blorbBytes = BuildBlorbBytesWithZcod(storyData);

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"test_{Guid.NewGuid()}.zblorb");
        try
        {
            File.WriteAllBytes(tempPath, blorbBytes);

            var machine = new Interpreter();
            machine.Load(tempPath,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen());

            Assert.NotNull(machine.Blorb);
            Assert.Equal(storyData, machine.Memory.OriginalBytes);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that .blorb extension triggers Blorb loading.
    /// </summary>
    [Fact]
    public void Load_BlorbExtension_LoadsAsBlorb()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] blorbBytes = BuildBlorbBytesWithZcod(storyData);

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"test_{Guid.NewGuid()}.blorb");
        try
        {
            File.WriteAllBytes(tempPath, blorbBytes);

            var machine = new Interpreter();
            machine.Load(tempPath,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen());

            Assert.NotNull(machine.Blorb);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that .zlb extension triggers Blorb loading.
    /// </summary>
    [Fact]
    public void Load_ZlbExtension_LoadsAsBlorb()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] blorbBytes = BuildBlorbBytesWithZcod(storyData);

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"test_{Guid.NewGuid()}.zlb");
        try
        {
            File.WriteAllBytes(tempPath, blorbBytes);

            var machine = new Interpreter();
            machine.Load(tempPath,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen());

            Assert.NotNull(machine.Blorb);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that .blb extension triggers Blorb loading.
    /// </summary>
    [Fact]
    public void Load_BlbExtension_LoadsAsBlorb()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] blorbBytes = BuildBlorbBytesWithZcod(storyData);

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"test_{Guid.NewGuid()}.blb");
        try
        {
            File.WriteAllBytes(tempPath, blorbBytes);

            var machine = new Interpreter();
            machine.Load(tempPath,
                new ScriptedInputStream(["quit", "y"]),
                new CaptureScreen());

            Assert.NotNull(machine.Blorb);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that a .z3 extension loads as a raw story file (no Blorb).
    /// </summary>
    [Fact]
    public void Load_Z3Extension_LoadsAsRawStory()
    {
        if (!File.Exists(MinizorkPath)) return;
        var machine = new Interpreter();
        machine.Load(MinizorkPath,
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        Assert.Null(machine.Blorb);
        Assert.True(machine.Running);
    }

    #endregion

    #region Metadata Parsing

    /// <summary>
    /// Verifies that RelN (release number) is parsed from the Blorb.
    /// Blorb "The Release Number Chunk".
    /// </summary>
    [Fact]
    public void Metadata_ReleaseNumber_Parsed()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] reln = [0x00, 0x2A]; // release 42
        var blorb = BuildBlorbWithZcodAndChunks(storyData,
            [new IffChunk("RelN", reln)]);

        Assert.Equal(42, blorb.ReleaseNumber);
    }

    /// <summary>
    /// Verifies that Fspc (frontispiece) is parsed.
    /// Blorb "The Frontispiece Chunk".
    /// </summary>
    [Fact]
    public void Metadata_Frontispiece_Parsed()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        byte[] fspc = [0x00, 0x00, 0x00, 0x03]; // picture 3
        var blorb = BuildBlorbWithZcodAndChunks(storyData,
            [new IffChunk("Fspc", fspc)]);

        Assert.Equal(3, blorb.FrontispiecePicture);
    }

    /// <summary>
    /// Verifies that IFmd (metadata XML) is parsed.
    /// Blorb "Metadata".
    /// </summary>
    [Fact]
    public void Metadata_Xml_Parsed()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        string xml = "<ifindex><story><title>Test</title></story></ifindex>";
        var blorb = BuildBlorbWithZcodAndChunks(storyData,
            [new IffChunk("IFmd", System.Text.Encoding.UTF8.GetBytes(xml))]);

        Assert.Equal(xml, blorb.MetadataXml);
    }

    /// <summary>
    /// Verifies that AUTH, (c), and ANNO chunks are parsed.
    /// </summary>
    [Fact]
    public void Metadata_AuthCopyrightAnnotation_Parsed()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        var blorb = BuildBlorbWithZcodAndChunks(storyData,
        [
            new IffChunk("AUTH", System.Text.Encoding.ASCII.GetBytes("Test Author")),
            new IffChunk("(c) ", System.Text.Encoding.ASCII.GetBytes("2026 Test")),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("Note 1")),
            new IffChunk("ANNO", System.Text.Encoding.ASCII.GetBytes("Note 2"))
        ]);

        Assert.Equal("Test Author", blorb.Author);
        Assert.Equal("2026 Test", blorb.Copyright);
        Assert.Equal(2, blorb.Annotations.Count);
        Assert.Equal("Note 1", blorb.Annotations[0]);
        Assert.Equal("Note 2", blorb.Annotations[1]);
    }

    /// <summary>
    /// Verifies that RDes (resource descriptions) is parsed.
    /// Blorb "The Resource Description Chunk".
    /// </summary>
    [Fact]
    public void Metadata_ResourceDescriptions_Parsed()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        string desc = "A beautiful sunset";
        byte[] rdes = BuildRDesChunk(("Pict", 1, desc));

        var blorb = BuildBlorbWithZcodAndChunks(storyData,
            [new IffChunk("RDes", rdes)]);

        Assert.True(blorb.ResourceDescriptions.ContainsKey(("Pict", 1)));
        Assert.Equal(desc, blorb.ResourceDescriptions[("Pict", 1)]);
    }

    /// <summary>
    /// Verifies that absent metadata properties have sensible defaults.
    /// </summary>
    [Fact]
    public void Metadata_Absent_DefaultValues()
    {
        if (!File.Exists(MinizorkPath)) return;
        byte[] storyData = File.ReadAllBytes(MinizorkPath);
        var blorb = BuildBlorbWithZcod(storyData);

        Assert.Equal(0, blorb.ReleaseNumber);
        Assert.Equal(-1, blorb.FrontispiecePicture);
        Assert.Null(blorb.GameIdentifier);
        Assert.Null(blorb.MetadataXml);
        Assert.Null(blorb.Author);
        Assert.Null(blorb.Copyright);
        Assert.Empty(blorb.Annotations);
        Assert.Empty(blorb.ResourceDescriptions);
    }

    #endregion

    #region Interpreter Blorb Property

    /// <summary>
    /// Verifies that the Blorb property is null when loading a raw story.
    /// </summary>
    [Fact]
    public void Interpreter_LoadRawStory_BlorbIsNull()
    {
        if (!File.Exists(MinizorkPath)) return;
        var machine = new Interpreter();
        machine.Load(File.ReadAllBytes(MinizorkPath),
            new ScriptedInputStream(["quit", "y"]),
            new CaptureScreen());

        Assert.Null(machine.Blorb);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds a BlorbReader containing the given Z-code as Exec/0 ZCOD.
    /// </summary>
    private static BlorbReader BuildBlorbWithZcod(byte[] storyData)
    {
        byte[] blorbBytes = BuildBlorbBytesWithZcod(storyData);
        return BlorbReader.Load(blorbBytes);
    }

    /// <summary>
    /// Builds raw Blorb bytes containing the given Z-code as Exec/0 ZCOD.
    /// </summary>
    private static byte[] BuildBlorbBytesWithZcod(byte[] storyData)
    {
        return BuildBlorbBytesWithZcod(storyData, []);
    }

    private static byte[] BuildBlorbBytesWithZcod(byte[] storyData, IffChunk[] extraChunks)
    {
        var zcodChunk = new IffChunk("ZCOD", storyData);
        return BuildBlorbBytes(
            [(BlorbUsage.Executable, 0, 0)],
            [zcodChunk],
            extraChunks);
    }

    /// <summary>
    /// Builds a BlorbReader with ZCOD exec and additional metadata chunks.
    /// </summary>
    private static BlorbReader BuildBlorbWithZcodAndChunks(byte[] storyData,
        IffChunk[] extraChunks)
    {
        byte[] bytes = BuildBlorbBytesWithZcod(storyData, extraChunks);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a BlorbReader with a non-ZCOD executable.
    /// </summary>
    private static BlorbReader BuildBlorbWithExec(byte[] execData, string chunkType)
    {
        var execChunk = new IffChunk(chunkType, execData);
        byte[] bytes = BuildBlorbBytes(
            [(BlorbUsage.Executable, 0, 0)],
            [execChunk]);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a resource-only Blorb (no executable).
    /// </summary>
    private static BlorbReader BuildResourceOnlyBlorb()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        byte[] bytes = BuildBlorbBytes(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", png)]);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a resource-only Blorb with an IFhd chunk for validation.
    /// </summary>
    private static BlorbReader BuildResourceOnlyBlorbWithIFhd(byte[] ifhd)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        byte[] bytes = BuildBlorbBytes(
            [(BlorbUsage.Picture, 1, 0)],
            [new IffChunk("PNG ", png)],
            [new IffChunk("IFhd", ifhd)]);
        return BlorbReader.Load(bytes);
    }

    /// <summary>
    /// Builds a 13-byte IFhd chunk from story data (same format as Quetzal S5).
    /// Blorb "The Game Identifier Chunk" — PC field set to zero.
    /// </summary>
    private static byte[] BuildIFhd(byte[] storyData)
    {
        byte[] ifhd = new byte[13];
        // Release number (header $02)
        ifhd[0] = storyData[0x02];
        ifhd[1] = storyData[0x03];
        // Serial number (header $12, 6 bytes)
        Array.Copy(storyData, 0x12, ifhd, 2, 6);
        // Checksum (header $1C)
        ifhd[8] = storyData[0x1C];
        ifhd[9] = storyData[0x1D];
        // PC = 0 for resource files
        ifhd[10] = 0;
        ifhd[11] = 0;
        ifhd[12] = 0;
        return ifhd;
    }

    /// <summary>
    /// Builds an RDes chunk with the given resource descriptions.
    /// </summary>
    private static byte[] BuildRDesChunk(params (string Usage, int Number, string Text)[] entries)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        WriteInt32BE(w, entries.Length);
        foreach (var (usage, number, text) in entries)
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes(usage));
            WriteInt32BE(w, number);
            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            WriteInt32BE(w, textBytes.Length);
            w.Write(textBytes);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Core Blorb builder: RIdx + resource chunks + optional extra chunks.
    /// </summary>
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

    private static void WriteInt32BE(BinaryWriter w, int value)
    {
        w.Write((byte)(value >> 24));
        w.Write((byte)(value >> 16));
        w.Write((byte)(value >> 8));
        w.Write((byte)value);
    }

    private static void WriteAscii(byte[] buf, int offset, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, 0, buf, offset, 4);
    }

    #endregion
}
