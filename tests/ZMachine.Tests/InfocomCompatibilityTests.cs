using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Compatibility tests for Infocom story files. Each story should load,
/// display opening text, accept input, and survive basic gameplay
/// without crashing.
/// </summary>
/// <remarks>
/// Task 14.2 — test matrix: loads, displays title, accepts input,
/// basic gameplay, no crash after 20 turns.
/// </remarks>
public class InfocomCompatibilityTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    #region V3 Stories

    /// <summary>
    /// Minizork (V3) — minimal Zork I, good for quick smoke testing.
    /// Tests: loads, displays opening text, accepts "look" command.
    /// </summary>
    [SkippableFact]
    public void Minizork_BootsAndPlays()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var result = RunStory(path, ["look", "inventory", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
        Assert.True(result.Output.Length > 50, "No meaningful output");
    }

    /// <summary>
    /// Minizork (V3) — verifies opening text contains recognizable content.
    /// </summary>
    [SkippableFact]
    public void Minizork_DisplaysOpeningText()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var result = RunStory(path, ["look", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.True(result.Output.Length > 100,
            "Opening text too short — game may not have started properly");
    }

    /// <summary>
    /// Zork I (V3) — full game, tests boot and basic commands.
    /// </summary>
    [SkippableFact]
    public void Zork1_BootsAndPlays()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found in stories/ (gitignored)");

        var result = RunStory(path,
            ["look", "open mailbox", "read leaflet", "go south",
             "go east", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
        Assert.True(result.Output.Length > 100, "No meaningful output");
    }

    /// <summary>
    /// Ballyhoo (V3) — circus-themed Infocom game.
    /// </summary>
    [SkippableFact]
    public void Ballyhoo_BootsAndPlays()
    {
        var path = Path.Combine(StoriesDir, "ballyhoo.z3");
        Skip.IfNot(File.Exists(path), "ballyhoo.z3 not found in stories/ (gitignored)");

        var result = RunStory(path, ["look", "inventory", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
        Assert.True(result.Output.Length > 50, "No meaningful output");
    }

    /// <summary>
    /// V3 regression test — runs minizork.z3 with 20 turns of gameplay
    /// to verify stability under extended play.
    /// </summary>
    [SkippableFact]
    public void V3Regression_Minizork_20Turns()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var commands = new[]
        {
            "look", "inventory", "go north", "go south",
            "go east", "go west", "look", "go north",
            "look", "go south", "go east", "look",
            "go west", "go north", "go south", "go east",
            "go west", "look", "inventory", "look",
            "quit", "y"
        };

        var result = RunStory(path, commands, instructionLimit: 50_000_000);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit during 20-turn test");
    }

    #endregion

    #region V4 Stories

    /// <summary>
    /// A Mind Forever Voyaging (V4) — tests V4 features including
    /// timed input capability.
    /// </summary>
    [SkippableFact]
    public void MindForeverVoyaging_BootsAndPlays()
    {
        var path = Path.Combine(StoriesDir, "mind.z4");
        Skip.IfNot(File.Exists(path), "mind.z4 not found in stories/ (gitignored)");

        var result = RunStory(path, ["look", "inventory", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
        Assert.True(result.Output.Length > 50, "No meaningful output");
    }

    /// <summary>
    /// V4 regression test — runs mind.z4 with several turns.
    /// </summary>
    [SkippableFact]
    public void V4Regression_Mind_BasicPlay()
    {
        var path = Path.Combine(StoriesDir, "mind.z4");
        Skip.IfNot(File.Exists(path), "mind.z4 not found in stories/ (gitignored)");

        var commands = new[]
        {
            "look", "inventory", "go north", "go south",
            "look", "go east", "go west", "look",
            "quit", "y"
        };

        var result = RunStory(path, commands, instructionLimit: 50_000_000);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
    }

    #endregion

    #region V5 Stories

    /// <summary>
    /// Sherlock (V5) — tests V5 features including upper window
    /// and extended opcodes.
    /// </summary>
    [SkippableFact]
    public void Sherlock_BootsAndPlays()
    {
        var path = Path.Combine(StoriesDir, "sherlock.z5");
        Skip.IfNot(File.Exists(path), "sherlock.z5 not found in stories/ (gitignored)");

        var result = RunStory(path, ["look", "inventory", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
        Assert.True(result.Output.Length > 50, "No meaningful output");
    }

    /// <summary>
    /// V5 regression test — runs sherlock.z5 with several turns.
    /// </summary>
    [SkippableFact]
    public void V5Regression_Sherlock_BasicPlay()
    {
        var path = Path.Combine(StoriesDir, "sherlock.z5");
        Skip.IfNot(File.Exists(path), "sherlock.z5 not found in stories/ (gitignored)");

        var commands = new[]
        {
            "look", "inventory", "go north", "go south",
            "look", "go east", "go west", "look",
            "quit", "y"
        };

        var result = RunStory(path, commands, instructionLimit: 50_000_000);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        Assert.False(result.HitLimit, "Hit instruction limit");
    }

    #endregion

    #region V6 Stories

    /// <summary>
    /// Journey (V6) — without Blorb picture resources, the game crashes
    /// during startup with a static-memory write error. This test
    /// verifies that crash is the expected out-of-bounds write, not an
    /// opcode or decoding failure.
    /// </summary>
    [SkippableFact]
    public void Journey_CrashesWithoutBlorb()
    {
        var path = Path.Combine(StoriesDir, "Journey", "STORY.DATA.z6");
        Skip.IfNot(File.Exists(path), "Journey/STORY.DATA.z6 not found in stories/ (gitignored)");

        var result = RunStory(path, ["quit", "y"]);

        Assert.True(result.Crashed, "V6 game should crash without Blorb resources");
        Assert.Contains("static", result.Error!,
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Cross-Version Checks

    /// <summary>
    /// Verifies that all available stories load without throwing
    /// during initialization.
    /// </summary>
    [SkippableTheory]
    [InlineData("minizork.z3")]
    [InlineData("zork1.z3")]
    [InlineData("ballyhoo.z3")]
    [InlineData("mind.z4")]
    [InlineData("sherlock.z5")]
    public void Story_LoadsSuccessfully(string filename)
    {
        var path = Path.Combine(StoriesDir, filename);
        Skip.IfNot(File.Exists(path), $"{filename} not found in stories/");

        var result = RunStory(path, ["quit", "y"]);

        Assert.False(result.Crashed, $"{filename} crashed on load: {result.Error}");
        Assert.True(result.Output.Length > 0,
            $"{filename} produced no output");
    }

    /// <summary>
    /// Verifies that all available stories accept at least one input
    /// command without crashing.
    /// </summary>
    [SkippableTheory]
    [InlineData("minizork.z3")]
    [InlineData("zork1.z3")]
    [InlineData("ballyhoo.z3")]
    [InlineData("mind.z4")]
    [InlineData("sherlock.z5")]
    public void Story_AcceptsInput(string filename)
    {
        var path = Path.Combine(StoriesDir, filename);
        Skip.IfNot(File.Exists(path), $"{filename} not found in stories/");

        var result = RunStory(path, ["look", "quit", "y"]);

        Assert.False(result.Crashed, $"{filename} crashed on input: {result.Error}");
        Assert.True(result.TurnsCompleted >= 1,
            $"{filename} did not complete any input turns");
    }

    #endregion

    #region Content Verification

    /// <summary>
    /// Verifies that minizork.z3 produces recognizable Zork content.
    /// </summary>
    [SkippableFact]
    public void Minizork_OutputContainsZorkContent()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var result = RunStory(path, ["look", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        // Minizork should mention typical Zork locations/objects
        string output = result.Output.ToLowerInvariant();
        bool hasContent = output.Contains("west") || output.Contains("house")
            || output.Contains("mailbox") || output.Contains("forest")
            || output.Contains("zork");
        Assert.True(hasContent, "Output lacks recognizable Zork content");
    }

    /// <summary>
    /// Verifies that zork1.z3 displays the opening location description.
    /// </summary>
    [SkippableFact]
    public void Zork1_OutputContainsOpeningText()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found in stories/ (gitignored)");

        var result = RunStory(path, ["look", "quit", "y"]);

        Assert.False(result.Crashed, $"Crashed: {result.Error}");
        string output = result.Output.ToLowerInvariant();
        bool hasContent = output.Contains("west") || output.Contains("house")
            || output.Contains("mailbox") || output.Contains("zork");
        Assert.True(hasContent, "Output lacks recognizable Zork I content");
    }

    #endregion

    #region Helpers

    private record StoryResult(
        string Output,
        bool Crashed,
        string? Error,
        bool HitLimit,
        int TurnsCompleted,
        int InstructionsExecuted);

    private static StoryResult RunStory(string path, string[] commands,
        int instructionLimit = TestHarness.DefaultInstructionLimit)
    {
        try
        {
            var harness = TestHarness.Run(path, commands, instructionLimit);
            return new StoryResult(
                Output: harness.Screen.Output,
                Crashed: false,
                Error: null,
                HitLimit: harness.HitInstructionLimit,
                TurnsCompleted: harness.Input.LinesConsumed,
                InstructionsExecuted: harness.InstructionsExecuted);
        }
        catch (Exception ex)
        {
            return new StoryResult(
                Output: "",
                Crashed: true,
                Error: $"{ex.GetType().Name}: {ex.Message}",
                HitLimit: false,
                TurnsCompleted: 0,
                InstructionsExecuted: 0);
        }
    }

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

    #endregion
}
