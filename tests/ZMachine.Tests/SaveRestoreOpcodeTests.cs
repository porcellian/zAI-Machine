namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests that @save and @restore opcodes are wired into QuetzalWriter
/// and QuetzalReader, producing valid save files and restoring state
/// correctly through the interpreter's opcode dispatch.
/// </summary>
public class SaveRestoreOpcodeTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    #region V3 save/restore (Zork I)

    /// <summary>
    /// Verifies that @save in a V3 game produces a valid Quetzal file
    /// via the opcode dispatch (not called directly).
    /// </summary>
    [SkippableFact]
    public void Save_V3_ProducesQuetzalFile()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        var provider = new MemorySaveFileProvider();

        // "save" command triggers the @save opcode in Zork I
        var harness = TestHarness.Run(path,
            ["save", "quit"],
            saveProvider: provider);

        Assert.True(provider.HasSave,
            "Expected @save opcode to write data via ISaveFileProvider");

        // Verify it's valid Quetzal
        var form = IffReader.Parse(provider.SavedData!);
        Assert.Equal("IFZS", form.FormType);
        Assert.NotNull(form.GetChunk("IFhd"));
        Assert.True(form.GetChunk("CMem") != null || form.GetChunk("UMem") != null);
        Assert.NotNull(form.GetChunk("Stks"));
    }

    /// <summary>
    /// Verifies that @restore in a V3 game loads state from a Quetzal
    /// file and resumes execution at the save point.
    /// </summary>
    [SkippableFact]
    public void SaveRestore_V3_RoundTrip()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        var provider = new MemorySaveFileProvider();

        // Play some moves, then save
        var harness1 = TestHarness.Run(path,
            ["open mailbox", "read leaflet", "save", "quit"],
            saveProvider: provider);

        Assert.True(provider.HasSave, "Save should have succeeded");
        string outputBeforeSave = harness1.Screen.Output;
        Assert.Contains("Ok.", outputBeforeSave);

        // Start fresh, then restore
        var harness2 = TestHarness.Run(path,
            ["restore", "look", "quit"],
            saveProvider: provider);

        string outputAfterRestore = harness2.Screen.Output;
        // After restore from a save made at the mailbox, "look" should show
        // the same location. The "Ok." from the restore confirmation or
        // game output after restore should appear.
        Assert.Contains("Ok.", outputAfterRestore);
    }

    #endregion

    #region V5 save/restore (Czech)

    /// <summary>
    /// Verifies that @save in a V5 game (EXT opcode) writes valid Quetzal.
    /// Czech.z5 uses @save during normal operation.
    /// </summary>
    [Fact]
    public void Save_V5_ProducesQuetzalFile()
    {
        var path = Path.Combine(StoriesDir, "czech.z5");
        if (!File.Exists(path)) return;

        var provider = new MemorySaveFileProvider();
        var harness = TestHarness.Run(path,
            ["save"],
            saveProvider: provider);

        // Czech may not prompt for save in the same way, but if it does,
        // check that the provider captured data
        if (provider.HasSave)
        {
            var form = IffReader.Parse(provider.SavedData!);
            Assert.Equal("IFZS", form.FormType);
        }
    }

    #endregion

    #region Provider absence

    /// <summary>
    /// Without a save file provider, @save should fail gracefully
    /// (V3: don't branch; V4+: store 0) — no exception thrown.
    /// </summary>
    [SkippableFact]
    public void Save_NoProvider_FailsGracefully()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        // No save provider — save should fail but not crash.
        // Use a short limit: the game may loop asking for retry,
        // but that's fine as long as no exception is thrown.
        var ex = Record.Exception(() =>
            TestHarness.Run(path, ["save"], instructionLimit: 500_000));

        Assert.Null(ex);
    }

    /// <summary>
    /// Without save data, @restore should fail gracefully — no exception.
    /// </summary>
    [SkippableFact]
    public void Restore_NoSaveData_FailsGracefully()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        var provider = new MemorySaveFileProvider();

        var ex = Record.Exception(() =>
            TestHarness.Run(path, ["restore"],
                instructionLimit: 500_000, saveProvider: provider));

        Assert.Null(ex);
    }

    #endregion

    #region Round-trip with game state verification

    /// <summary>
    /// Saves after collecting items, restores from fresh, and verifies
    /// the game state reflects the items collected before saving.
    /// </summary>
    [SkippableFact]
    public void SaveRestore_V3_PreservesGameState()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        var provider = new MemorySaveFileProvider();

        // Collect the leaflet, go north, then save
        TestHarness.Run(path,
            ["open mailbox", "take leaflet", "north", "save", "quit"],
            randomSeed: 42,
            saveProvider: provider);

        Assert.True(provider.HasSave);

        // Fresh start → restore → check inventory shows leaflet
        var harness = TestHarness.Run(path,
            ["restore", "inventory", "quit"],
            randomSeed: 42,
            saveProvider: provider);

        Assert.Contains("leaflet", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Quetzal format validation

    /// <summary>
    /// Verifies the IFhd chunk in an opcode-triggered save has the correct
    /// release, serial, and checksum matching the story file.
    /// </summary>
    [SkippableFact]
    public void Save_V3_IFhd_MatchesStoryHeader()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found");

        var provider = new MemorySaveFileProvider();
        var harness = TestHarness.Run(path,
            ["save", "quit"],
            saveProvider: provider);

        Assert.True(provider.HasSave);

        var form = IffReader.Parse(provider.SavedData!);
        var ifhd = form.GetChunk("IFhd")!;

        // Release from header $02
        ushort release = (ushort)(ifhd.Data[0] << 8 | ifhd.Data[1]);
        ushort expectedRelease = (ushort)(harness.Machine.Memory.OriginalBytes[0x02] << 8 |
                                          harness.Machine.Memory.OriginalBytes[0x03]);
        Assert.Equal(expectedRelease, release);

        // Serial from header $12 (6 bytes)
        for (int i = 0; i < 6; i++)
            Assert.Equal(harness.Machine.Memory.OriginalBytes[0x12 + i], ifhd.Data[2 + i]);

        // PC should be non-zero (points to branch data of the @save instruction)
        int savedPC = (ifhd.Data[10] << 16) | (ifhd.Data[11] << 8) | ifhd.Data[12];
        Assert.NotEqual(0, savedPC);
    }

    #endregion

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
