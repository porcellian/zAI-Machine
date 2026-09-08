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

    [Fact]
    public void Zork1_OpenMailbox_FindsLeaflet()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["open mailbox", "quit", "y"]);

        Assert.Contains("leaflet", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zork1_ReadLeaflet_PrintsText()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["open mailbox", "read leaflet", "quit", "y"]);

        Assert.Contains("ZORK is a game", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

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
        // Going north should reach the forest.
        Assert.Contains("North of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zork1_Inventory_AtStart()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["inventory", "quit", "y"]);

        // Should say you're carrying nothing or be empty-handed.
        string output = harness.Screen.Output;
        bool hasResponse = output.Contains("carrying", StringComparison.OrdinalIgnoreCase)
                        || output.Contains("empty-handed", StringComparison.OrdinalIgnoreCase)
                        || output.Contains("nothing", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasResponse, "Expected inventory response in output");
    }

    [Fact]
    public void Zork1_GoNorth_ReachesNorthOfHouse()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["go north", "quit", "y"]);

        Assert.Contains("North of House", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zork1_InstructionCount_IsReasonable()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path, ["quit", "y"]);

        // Boot + quit should take far less than the default limit.
        Assert.True(harness.InstructionsExecuted > 100,
            "Expected more than 100 instructions for boot");
        Assert.True(harness.InstructionsExecuted < 1_000_000,
            "Expected fewer than 1M instructions for boot+quit");
    }

    #endregion

    #region Minizork — V3

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

    [Fact]
    public void Czech_Boots_PrintsHeader()
    {
        if (!File.Exists(CzechPath))
            return;

        var harness = TestHarness.Run(CzechPath, [""]);

        Assert.False(harness.HitInstructionLimit);
        // Czech conformance test prints its name on boot.
        Assert.Contains("Czech", harness.Screen.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Harness Infrastructure

    [Fact]
    public void TestHarness_InstructionLimit_Aborts()
    {
        if (!File.Exists(Zork1Path))
            return;

        // A very small instruction limit should abort before reading any input.
        var harness = TestHarness.Run(Zork1Path, ["quit", "y"],
            instructionLimit: 100);

        Assert.True(harness.HitInstructionLimit);
        Assert.Equal(100, harness.InstructionsExecuted);
    }

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

    [Fact]
    public void ScriptedInputStream_Exhausted_ReturnsQuit()
    {
        var stream = new ScriptedInputStream(["look"]);

        var (text1, _) = stream.ReadLine(80);
        Assert.Equal("look", text1);

        var (text2, _) = stream.ReadLine(80);
        Assert.Equal("quit", text2);
    }

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

    [Fact]
    public void CaptureScreen_CapturesOutput()
    {
        var screen = new CaptureScreen();

        screen.Print("Hello ");
        screen.PrintChar('W');
        screen.NewLine();

        Assert.Equal("Hello W" + Environment.NewLine, screen.Output);
    }

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
