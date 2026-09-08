namespace ZMachine.Tests;

using ZMachine.Tests.Harness;

/// <summary>
/// Regression tests using the TestHarness to run scripted scenarios
/// against real story files and verify expected output substrings.
/// </summary>
public class RegressionTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";
    private const string MinizorkPath = "stories/minizork.z3";

    #region Zork I — V3

    /// <summary>
    /// Verifies that Zork I boots and prints opening text mentioning
    /// ZORK and "West of House".
    /// </summary>
    [Fact]
    public void Zork1_Boots_PrintsOpeningText()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path, ["quit", "y"]);

        Assert.False(harness.HitInstructionLimit);
        Assert.Contains("ZORK", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("West of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that "open mailbox" produces output mentioning a leaflet.
    /// </summary>
    [Fact]
    public void Zork1_OpenMailbox_FindsLeaflet()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["open mailbox", "quit", "y"]);

        Assert.Contains("leaflet", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that "read leaflet" after opening the mailbox prints
    /// the leaflet text ("ZORK is a game").
    /// </summary>
    [Fact]
    public void Zork1_ReadLeaflet_PrintsText()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["open mailbox", "read leaflet", "quit", "y"]);

        Assert.Contains("ZORK is a game", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs the mailbox script file and verifies leaflet output.
    /// </summary>
    [Fact]
    public void Zork1_ScriptFile_MailboxSequence()
    {
        if (!File.Exists(Zork1Path))
            return;

        string scriptPath = "tests/scripts/zork1_mailbox.txt";
        if (!File.Exists(scriptPath))
            return;

        var harness = TestHarness.RunScript(Zork1Path, scriptPath);

        Assert.False(harness.HitInstructionLimit);
        Assert.Contains("leaflet", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs the five-move exploration script and verifies multi-turn
    /// output including movement to North of House.
    /// </summary>
    [Fact]
    public void Zork1_ScriptFile_ExplorationSequence()
    {
        if (!File.Exists(Zork1Path))
            return;

        string scriptPath = "tests/scripts/zork1_exploration.txt";
        if (!File.Exists(scriptPath))
            return;

        var harness = TestHarness.RunScript(Zork1Path, scriptPath);

        Assert.False(harness.HitInstructionLimit);
        Assert.Contains("West of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mailbox", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("North of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that "inventory" at game start produces a response
    /// about carrying nothing.
    /// </summary>
    [Fact]
    public void Zork1_Inventory_AtStart()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["inventory", "quit", "y"]);

        string output = harness.Screen.Output;
        bool hasResponse = output.Contains("carrying", StringComparison.OrdinalIgnoreCase)
                        || output.Contains("empty-handed", StringComparison.OrdinalIgnoreCase)
                        || output.Contains("nothing", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasResponse, "Expected inventory response in output");
    }

    /// <summary>
    /// Verifies that "go north" from the starting location reaches
    /// North of House.
    /// </summary>
    [Fact]
    public void Zork1_GoNorth_ReachesNorthOfHouse()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["go north", "quit", "y"]);

        Assert.Contains("North of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sanity-checks that boot + quit takes a reasonable number of
    /// instructions (more than 100, fewer than 1M).
    /// </summary>
    [Fact]
    public void Zork1_InstructionCount_IsReasonable()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path, ["quit", "y"]);

        Assert.True(harness.InstructionsExecuted > 100,
            "Expected more than 100 instructions for boot");
        Assert.True(harness.InstructionsExecuted < 1_000_000,
            "Expected fewer than 1M instructions for boot+quit");
    }

    #endregion

    #region Minizork — V3

    /// <summary>
    /// Verifies that Minizork boots and prints "West of House".
    /// </summary>
    [Fact]
    public void Minizork_Boots()
    {
        if (!File.Exists(MinizorkPath))
            return;

        var harness = TestHarness.Run(MinizorkPath, ["quit", "y"]);

        Assert.False(harness.HitInstructionLimit);
        Assert.Contains("West of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Czech — V5 Conformance

    /// <summary>
    /// Verifies that the Czech V5 conformance test boots and prints
    /// its header text.
    /// </summary>
    [Fact]
    public void Czech_Boots_PrintsHeader()
    {
        if (!File.Exists(CzechPath))
            return;

        var harness = TestHarness.Run(CzechPath, [""]);

        Assert.False(harness.HitInstructionLimit);
        Assert.Contains("Czech", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Harness Infrastructure

    /// <summary>
    /// Verifies that a very small instruction limit aborts the run
    /// before any input is consumed.
    /// </summary>
    [Fact]
    public void TestHarness_InstructionLimit_Aborts()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path, ["quit", "y"],
            instructionLimit: 100);

        Assert.True(harness.HitInstructionLimit);
        Assert.Equal(100, harness.InstructionsExecuted);
    }

    /// <summary>
    /// Verifies that ScriptedInputStream.FromFile skips comment lines
    /// and blank lines.
    /// </summary>
    [Fact]
    public void ScriptedInputStream_FromFile_SkipsCommentsAndBlanks()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "# comment\n\nlook\n\n# another\nquit\n");
            var stream = ScriptedInputStream.FromFile(tempFile);

            var (text1, _) = stream.ReadLine(80);
            Assert.Equal("look", text1);

            var (text2, _) = stream.ReadLine(80);
            Assert.Equal("quit", text2);

            Assert.False(stream.HasMore);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that an exhausted ScriptedInputStream returns "quit"
    /// as a safety fallback.
    /// </summary>
    [Fact]
    public void ScriptedInputStream_Exhausted_ReturnsQuit()
    {
        var stream = new ScriptedInputStream(["look"]);

        var (text1, _) = stream.ReadLine(80);
        Assert.Equal("look", text1);

        var (text2, _) = stream.ReadLine(80);
        Assert.Equal("quit", text2);
    }

    /// <summary>
    /// Verifies that LinesConsumed tracks the number of ReadLine calls
    /// that consumed scripted input.
    /// </summary>
    [Fact]
    public void ScriptedInputStream_TracksLinesConsumed()
    {
        var stream = new ScriptedInputStream(["a", "b", "c"]);

        stream.ReadLine(80);
        Assert.Equal(1, stream.LinesConsumed);

        stream.ReadLine(80);
        stream.ReadLine(80);
        Assert.Equal(3, stream.LinesConsumed);
    }

    /// <summary>
    /// Verifies that CaptureScreen records Print, PrintChar, and
    /// NewLine output into a single string.
    /// </summary>
    [Fact]
    public void CaptureScreen_CapturesOutput()
    {
        var screen = new CaptureScreen();

        screen.Print("Hello ");
        screen.PrintChar('W');
        screen.NewLine();

        Assert.Equal("Hello W" + Environment.NewLine, screen.Output);
    }

    /// <summary>
    /// Verifies that CaptureScreen records status lines separately
    /// from main output.
    /// </summary>
    [Fact]
    public void CaptureScreen_CapturesStatusLines()
    {
        var screen = new CaptureScreen();

        screen.ShowStatusLine("West of House", "0/0");
        screen.ShowStatusLine("Kitchen", "10/5");

        Assert.Equal(2, screen.StatusLines.Count);
        Assert.Contains("West of House", screen.StatusLines[0]);
        Assert.Contains("Kitchen", screen.StatusLines[1]);
    }

    #endregion
}
