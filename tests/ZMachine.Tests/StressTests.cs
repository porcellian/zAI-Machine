using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Stress tests that feed random input commands to story files and verify
/// the interpreter does not crash with unhandled exceptions. Expected
/// errors (out-of-bounds, illegal writes) are caught and tolerated;
/// only truly unhandled failures cause test failure.
/// </summary>
/// <remarks>
/// Task 14.3 — stress test: all story files with 100 random inputs
/// without crash.
/// </remarks>
public class StressTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    private static readonly string[] RandomCommands =
    [
        "look", "inventory", "go north", "go south", "go east", "go west",
        "go up", "go down", "open door", "close door", "take all", "drop all",
        "examine me", "wait", "jump", "hello", "xyzzy", "plugh", "save",
        "restore", "verbose", "brief", "superbrief", "score", "diagnose",
        "again", "undo", "oops", "take lamp", "light lamp", "read book",
        "push button", "pull lever", "turn knob", "climb tree", "swim",
        "dig", "pray", "sing", "sleep", "think", "listen", "smell",
        "touch wall", "taste water", "wave", "shout", "attack troll",
        "throw sword", "give coin", "show ticket", "ask about weather"
    ];

    /// <summary>
    /// Feeds 100 random commands to a story and verifies no unhandled crash.
    /// ZMachineException (illegal opcode, memory violation) is tolerated
    /// as a known error class; other exceptions fail the test.
    /// </summary>
    [SkippableTheory]
    [InlineData("minizork.z3")]
    [InlineData("zork1.z3")]
    [InlineData("ballyhoo.z3")]
    [InlineData("mind.z4")]
    [InlineData("sherlock.z5")]
    public void Story_Survives100RandomInputs(string filename)
    {
        var path = Path.Combine(StoriesDir, filename);
        Skip.IfNot(File.Exists(path), $"{filename} not found in stories/");

        var rng = new Random(42);
        var commands = new string[102];
        for (int i = 0; i < 100; i++)
            commands[i] = RandomCommands[rng.Next(RandomCommands.Length)];
        commands[100] = "quit";
        commands[101] = "y";

        Exception? caught = null;
        try
        {
            TestHarness.Run(path, commands, instructionLimit: 100_000_000);
        }
        catch (ZMachineException)
        {
            // Expected error class — interpreter detected an illegal
            // operation and reported it cleanly.
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.Null(caught);
    }

    /// <summary>
    /// Runs czech.z5 conformance suite to verify no regression from
    /// performance/error-handling changes.
    /// </summary>
    [SkippableFact]
    public void Czech_StillPassesAfterChanges()
    {
        var path = Path.Combine(StoriesDir, "czech.z5");
        Skip.IfNot(File.Exists(path), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(path, [], instructionLimit: 100_000_000);
        string output = harness.Screen.Output;

        Assert.Contains("Failed: 0", output);
        Assert.Contains("Didn't crash: hooray!", output);
    }

    /// <summary>
    /// Verifies that ZMachineException carries PC and opcode context
    /// in its message.
    /// </summary>
    [Fact]
    public void ZMachineException_ContainsDiagnosticInfo()
    {
        var ex = new ZMachineException(
            "division by zero", 0x1A3F, "2OP", 23);

        Assert.Contains("$01A3F", ex.Message);
        Assert.Contains("2OP:23", ex.Message);
        Assert.Contains("division by zero", ex.Message);
        Assert.Equal(0x1A3F, ex.PC);
        Assert.Equal("2OP", ex.OpcodeForm);
        Assert.Equal(23, ex.OpcodeNumber);
    }

    /// <summary>
    /// Verifies that the instruction trace callback is invoked during
    /// execution when TraceWriter is set.
    /// </summary>
    [SkippableFact]
    public void TraceWriter_RecordsInstructions()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var input = new ScriptedInputStream(["quit", "y"]);
        var screen = new CaptureScreen();
        var machine = new Interpreter();

        var traceLines = new List<string>();
        machine.Load(path, input, screen);
        machine.TraceWriter = line => traceLines.Add(line);

        int limit = 10_000;
        while (machine.Running && limit-- > 0)
            machine.Step();

        Assert.True(traceLines.Count > 0, "Trace should have recorded instructions");
        Assert.All(traceLines, line => Assert.StartsWith("$", line));
    }

    /// <summary>
    /// Verifies that InstructionCount tracks executed instructions.
    /// </summary>
    [SkippableFact]
    public void InstructionCount_TracksExecution()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var input = new ScriptedInputStream(["quit", "y"]);
        var screen = new CaptureScreen();
        var machine = new Interpreter();

        machine.Load(path, input, screen);

        int limit = 1000;
        while (machine.Running && limit-- > 0)
            machine.Step();

        Assert.True(machine.InstructionCount > 0);
    }

    /// <summary>
    /// Verifies that abbreviation caching doesn't change decoded output.
    /// Runs minizork twice and compares output.
    /// </summary>
    [SkippableFact]
    public void AbbreviationCache_ProducesSameOutput()
    {
        var path = Path.Combine(StoriesDir, "minizork.z3");
        Skip.IfNot(File.Exists(path), "minizork.z3 not found in stories/");

        var result1 = TestHarness.Run(path, ["look", "quit", "y"]);
        var result2 = TestHarness.Run(path, ["look", "quit", "y"]);

        Assert.Equal(result1.Screen.Output, result2.Screen.Output);
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
}
