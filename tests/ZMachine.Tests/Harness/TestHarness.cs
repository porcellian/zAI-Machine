namespace ZMachine.Tests.Harness;

using ZMachine.Core;

/// <summary>
/// Orchestrates automated Z-Machine test runs: loads a story, feeds
/// scripted input, captures output, and enforces a turn-count safety
/// limit to prevent infinite loops in test.
/// </summary>
public class TestHarness
{
    /// <summary>
    /// Default maximum number of instructions before the harness aborts.
    /// 10 million covers any reasonable game startup and several turns.
    /// </summary>
    public const int DefaultInstructionLimit = 10_000_000;

    /// <summary>Captured screen output from the run.</summary>
    public CaptureScreen Screen { get; private set; } = new();

    /// <summary>The scripted input stream used for the run.</summary>
    public ScriptedInputStream Input { get; private set; } = null!;

    /// <summary>The interpreter instance after the run.</summary>
    public Interpreter Machine { get; private set; } = null!;

    /// <summary>Number of instructions executed during the run.</summary>
    public int InstructionsExecuted { get; private set; }

    /// <summary>Whether the run was aborted due to hitting the instruction limit.</summary>
    public bool HitInstructionLimit { get; private set; }

    /// <summary>
    /// Runs a story file with the given commands and returns the harness
    /// with captured output. Commands are fed one per @read call.
    /// </summary>
    /// <param name="storyPath">Path to the story file.</param>
    /// <param name="commands">Scripted commands to feed as input.</param>
    /// <param name="instructionLimit">
    /// Maximum instructions before aborting. Prevents infinite loops.
    /// </param>
    public static TestHarness Run(string storyPath, string[] commands,
        int instructionLimit = DefaultInstructionLimit)
    {
        var harness = new TestHarness();
        harness.Input = new ScriptedInputStream(commands);
        harness.Screen = new CaptureScreen();
        harness.Machine = new Interpreter();

        harness.Machine.Load(storyPath, harness.Input, harness.Screen);
        harness.Execute(instructionLimit);

        return harness;
    }

    /// <summary>
    /// Runs a story file with commands loaded from a script file.
    /// </summary>
    public static TestHarness RunScript(string storyPath, string scriptPath,
        int instructionLimit = DefaultInstructionLimit)
    {
        var harness = new TestHarness();
        harness.Input = ScriptedInputStream.FromFile(scriptPath);
        harness.Screen = new CaptureScreen();
        harness.Machine = new Interpreter();

        harness.Machine.Load(storyPath, harness.Input, harness.Screen);
        harness.Execute(instructionLimit);

        return harness;
    }

    /// <summary>
    /// Runs a story from a byte array (for synthetic test stories).
    /// </summary>
    public static TestHarness RunBytes(byte[] storyData, string[] commands,
        int instructionLimit = DefaultInstructionLimit)
    {
        var harness = new TestHarness();
        harness.Input = new ScriptedInputStream(commands);
        harness.Screen = new CaptureScreen();
        harness.Machine = new Interpreter();

        harness.Machine.Load(storyData, harness.Input, harness.Screen);
        harness.Execute(instructionLimit);

        return harness;
    }

    private void Execute(int instructionLimit)
    {
        int count = 0;
        while (Machine.Running)
        {
            Machine.Step();
            count++;
            if (count >= instructionLimit)
            {
                HitInstructionLimit = true;
                break;
            }
        }
        InstructionsExecuted = count;
    }
}
