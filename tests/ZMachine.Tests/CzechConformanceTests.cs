using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Runs the czech.z5 Z-Machine conformance test suite and verifies that
/// all tests pass. czech.z5 exercises opcodes, text handling, stack
/// operations, and header fields systematically.
/// </summary>
/// <remarks>
/// ZSpec S14/S15 — opcode correctness. ZSpec S11 — Standard 1.1 header.
/// </remarks>
public class CzechConformanceTests
{
    private static readonly string StoryPath =
        Path.Combine(FindRepoRoot(), "stories", "czech.z5");

    /// <summary>
    /// Runs czech.z5 to completion and verifies zero failures.
    /// Czech 0.8 runs 406 opcode tests plus 19 print format checks.
    /// </summary>
    [SkippableFact]
    public void Czech_AllTestsPass()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        string output = harness.Screen.Output;

        Assert.False(harness.HitInstructionLimit,
            $"czech.z5 hit instruction limit at {harness.InstructionsExecuted} instructions");
        Assert.Contains("Failed: 0", output);
    }

    /// <summary>
    /// Verifies that czech.z5 runs all expected tests without crashing.
    /// </summary>
    [SkippableFact]
    public void Czech_RunsToCompletion()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        string output = harness.Screen.Output;

        Assert.Contains("Didn't crash: hooray!", output);
        Assert.Contains("Performed", output);
    }

    /// <summary>
    /// Verifies no ERROR lines appear in the czech.z5 output.
    /// </summary>
    [SkippableFact]
    public void Czech_NoErrors()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        string output = harness.Screen.Output;

        Assert.DoesNotContain("ERROR", output);
    }

    /// <summary>
    /// Verifies the Standard 1.1 version header bytes at $32/$33.
    /// ZSpec11 — header bytes $32/$33 should be $01/$01.
    /// </summary>
    [SkippableFact]
    public void Czech_StandardVersionHeader()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        byte major = harness.Machine.Memory.ReadByte(0x32);
        byte minor = harness.Machine.Memory.ReadByte(0x33);

        Assert.Equal(0x01, major);
        Assert.Equal(0x01, minor);
    }

    /// <summary>
    /// Verifies that the interpreter number and version are set.
    /// ZSpec S11 — interpreter number at $1E, version at $1F.
    /// </summary>
    [SkippableFact]
    public void Czech_InterpreterIdentification()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        byte interpNum = harness.Machine.Memory.ReadByte(0x1E);
        byte interpVer = harness.Machine.Memory.ReadByte(0x1F);

        Assert.NotEqual(0, interpNum);
        Assert.NotEqual(0, interpVer);
    }

    /// <summary>
    /// Verifies that basic capability flags are set in the header.
    /// ZSpec S11 — Flags 1 bits 0 (color), 2 (bold), 3 (italic),
    /// 4 (fixed-space) should be set for a compliant V5 interpreter.
    /// </summary>
    [SkippableFact]
    public void Czech_CapabilityFlagsSet()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        byte flags1 = harness.Machine.Memory.ReadByte(0x01);

        Assert.NotEqual(0, flags1 & 0x01); // colors
        Assert.NotEqual(0, flags1 & 0x04); // bold
        Assert.NotEqual(0, flags1 & 0x08); // italic
        Assert.NotEqual(0, flags1 & 0x10); // fixed-space
    }

    /// <summary>
    /// Verifies that screen dimensions are set to non-zero values.
    /// ZSpec S11 — screen height at $20, width at $21.
    /// </summary>
    [SkippableFact]
    public void Czech_ScreenDimensionsSet()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        byte height = harness.Machine.Memory.ReadByte(0x20);
        byte width = harness.Machine.Memory.ReadByte(0x21);

        Assert.NotEqual(0, height);
        Assert.NotEqual(0, width);
    }

    /// <summary>
    /// Verifies that all opcode test sections complete successfully.
    /// Each section should appear in the output with dots (no ERROR lines).
    /// </summary>
    [SkippableTheory]
    [InlineData("Jumps")]
    [InlineData("Variables")]
    [InlineData("Arithmetic ops")]
    [InlineData("Logical ops")]
    [InlineData("Memory")]
    [InlineData("Subroutines")]
    [InlineData("Objects")]
    [InlineData("Indirect Opcodes")]
    [InlineData("Misc")]
    public void Czech_SectionCompletes(string section)
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        Assert.Contains(section, harness.Screen.Output);
    }

    /// <summary>
    /// Verifies that print opcode tests produce correct output.
    /// </summary>
    [SkippableFact]
    public void Czech_PrintTests()
    {
        Skip.IfNot(File.Exists(StoryPath), "czech.z5 not found in stories/");

        var harness = TestHarness.Run(StoryPath, [],
            instructionLimit: 100_000_000);

        string output = harness.Screen.Output;

        Assert.Contains("print_num (0, 1, -1, 32767,-32768, -1): 0, 1, -1, 32767, -32768, -1", output);
        Assert.Contains("print_char (abcd): abcd", output);
        Assert.Contains("print_obj (Test Object #1Test Object #2): Test Object #1Test Object #2", output);
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
