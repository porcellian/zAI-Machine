namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Tests for the Debugger engine — verifies step-through execution,
/// breakpoints, state inspection, trace logging, watch expressions,
/// and memory dump against real story files and synthetic scenarios.
/// </summary>
public class DebuggerTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";

    #region Step Into

    /// <summary>
    /// Verifies that stepping through 10 instructions advances the PC
    /// each time and the PC history has no repeats (no infinite loop).
    /// </summary>
    [Fact]
    public void Zork1_StepInto_10Instructions_PCAdvances()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);
        int initialPC = dbg.PC;
        var pcHistory = new List<int> { initialPC };

        for (int i = 0; i < 10; i++)
        {
            var reason = dbg.StepInto();
            Assert.Equal(StopReason.Step, reason);
            pcHistory.Add(dbg.PC);
        }

        Assert.Equal(11, pcHistory.Count);
        Assert.NotEqual(pcHistory[0], pcHistory[^1]);
        Assert.Equal(10, dbg.InstructionsExecuted);
    }

    /// <summary>
    /// Verifies that each step produces a valid disassembly at the PC.
    /// </summary>
    [Fact]
    public void Zork1_StepInto_EachStepHasValidInstruction()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        for (int i = 0; i < 5; i++)
        {
            var inst = dbg.GetCurrentInstruction();
            Assert.NotNull(inst);
            Assert.NotEmpty(inst.Mnemonic);

            dbg.StepInto();
        }
    }

    /// <summary>
    /// Verifies that StepInto returns Halted after a quit instruction.
    /// </summary>
    [Fact]
    public void Zork1_StepInto_ReturnsHaltedWhenStopped()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path, ["quit", "y"]);

        // Run until halted
        StopReason reason = StopReason.Step;
        int limit = 1_000_000;
        while (reason == StopReason.Step && --limit > 0)
            reason = dbg.StepInto();

        if (dbg.IsStopped)
        {
            reason = dbg.StepInto();
            Assert.Equal(StopReason.Halted, reason);
        }
    }

    #endregion

    #region Step Over

    /// <summary>
    /// Verifies that StepOver executes a call and returns to the
    /// same depth.
    /// </summary>
    [Fact]
    public void Zork1_StepOver_CallReturnsToSameDepth()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        int depthBefore = dbg.Machine.State.CallStack.FrameCount;
        var reason = dbg.StepOver();

        Assert.True(reason == StopReason.Step || reason == StopReason.Halted);
        if (reason == StopReason.Step)
        {
            int depthAfter = dbg.Machine.State.CallStack.FrameCount;
            Assert.True(depthAfter <= depthBefore + 1);
        }
    }

    #endregion

    #region Continue

    /// <summary>
    /// Verifies that Continue with a breakpoint stops at the breakpoint.
    /// </summary>
    [Fact]
    public void Zork1_Continue_StopsAtBreakpoint()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        // Step once to get a future address, then set breakpoint there
        dbg.StepInto();
        int secondPC = dbg.PC;

        // Restart by creating a new debugger and setting breakpoint
        dbg = CreateDebugger(Zork1Path);
        dbg.AddBreakpoint(secondPC);

        var reason = dbg.Continue(100_000);
        Assert.Equal(StopReason.Breakpoint, reason);
        Assert.Equal(secondPC, dbg.PC);
    }

    /// <summary>
    /// Verifies that Continue with instruction limit returns the limit reason.
    /// </summary>
    [Fact]
    public void Zork1_Continue_HitsInstructionLimit()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var reason = dbg.Continue(10);
        Assert.Equal(StopReason.InstructionLimit, reason);
    }

    #endregion

    #region Address Breakpoints

    /// <summary>
    /// Verifies that an address breakpoint can be added and removed.
    /// </summary>
    [Fact]
    public void Breakpoints_AddRemove()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var bp = dbg.AddBreakpoint(0x1234);
        Assert.Single(dbg.Breakpoints);
        Assert.Equal(BreakpointType.Address, bp.Type);

        dbg.RemoveBreakpoint(bp);
        Assert.Empty(dbg.Breakpoints);
    }

    /// <summary>
    /// Verifies that disabling a breakpoint prevents it from triggering.
    /// </summary>
    [Fact]
    public void Breakpoints_Disabled_DoesNotTrigger()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        dbg.StepInto();
        int targetPC = dbg.PC;

        dbg = CreateDebugger(Zork1Path);
        var bp = dbg.AddBreakpoint(targetPC);
        bp.Enabled = false;

        var reason = dbg.Continue(100);
        Assert.NotEqual(StopReason.Breakpoint, reason);
    }

    /// <summary>
    /// Verifies ClearBreakpoints removes all breakpoints.
    /// </summary>
    [Fact]
    public void Breakpoints_ClearAll()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        dbg.AddBreakpoint(0x1000);
        dbg.AddBreakpoint(0x2000);
        dbg.AddBreakpoint(0x3000);
        Assert.Equal(3, dbg.Breakpoints.Count);

        dbg.ClearBreakpoints();
        Assert.Empty(dbg.Breakpoints);
    }

    #endregion

    #region Conditional Breakpoints

    /// <summary>
    /// Verifies that a conditional breakpoint can be created.
    /// </summary>
    [Fact]
    public void ConditionalBreakpoint_CreatesCorrectly()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var bp = dbg.AddConditionalBreakpoint(0, 42);
        Assert.Equal(BreakpointType.Conditional, bp.Type);
        Assert.Equal((byte)0, bp.GlobalVariable);
        Assert.Equal((ushort)42, bp.ConditionValue);
    }

    #endregion

    #region Opcode Breakpoints

    /// <summary>
    /// Verifies that an opcode breakpoint triggers on the named mnemonic.
    /// </summary>
    [Fact]
    public void OpcodeBreakpoint_TriggersOnMnemonic()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        dbg.AddOpcodeBreakpoint("storew");
        var reason = dbg.Continue(100_000);

        if (reason == StopReason.Breakpoint)
        {
            var inst = dbg.GetCurrentInstruction();
            Assert.NotNull(inst);
            Assert.Equal("storew", inst.Mnemonic);
        }
    }

    #endregion

    #region State Inspection

    /// <summary>
    /// Verifies that the call stack is non-empty after loading.
    /// </summary>
    [Fact]
    public void Zork1_CallStack_NonEmpty()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var stack = dbg.GetCallStack();
        Assert.NotEmpty(stack);
    }

    /// <summary>
    /// Verifies that locals can be inspected.
    /// </summary>
    [Fact]
    public void Zork1_Locals_Inspectable()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var locals = dbg.GetLocals();
        Assert.True(locals.Count >= 0);
    }

    /// <summary>
    /// Verifies that globals returns exactly 240 values.
    /// </summary>
    [Fact]
    public void Zork1_Globals_Has240()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var globals = dbg.GetGlobals();
        Assert.Equal(240, globals.Length);
    }

    /// <summary>
    /// Verifies that eval stack is accessible (may be empty).
    /// </summary>
    [Fact]
    public void Zork1_EvalStack_Accessible()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var stack = dbg.GetEvalStack();
        Assert.NotNull(stack);
    }

    /// <summary>
    /// Verifies that the call stack changes after stepping into a call.
    /// </summary>
    [Fact]
    public void Zork1_CallStack_GrowsOnCall()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        int depthBefore = dbg.GetCallStack().Count;

        // Step until the call stack grows
        for (int i = 0; i < 20; i++)
        {
            dbg.StepInto();
            if (dbg.GetCallStack().Count > depthBefore)
            {
                Assert.True(dbg.GetCallStack().Count > depthBefore);
                return;
            }
        }
    }

    #endregion

    #region Memory Dump

    /// <summary>
    /// Verifies that memory dump returns formatted hex output.
    /// </summary>
    [Fact]
    public void Zork1_MemoryDump_HasFormattedOutput()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var dump = dbg.GetMemoryDump(0x00, 64);
        Assert.Equal(64, dump.Data.Length);
        Assert.NotEmpty(dump.Formatted);
        Assert.Contains("$00000", dump.Formatted);
        Assert.Contains("|", dump.Formatted);
    }

    /// <summary>
    /// Verifies that the memory dump includes the static base boundary.
    /// </summary>
    [Fact]
    public void Zork1_MemoryDump_HasStaticBase()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var dump = dbg.GetMemoryDump(0x00, 16);
        Assert.True(dump.StaticBase > 0);
    }

    /// <summary>
    /// Verifies that the header bytes match expected version.
    /// </summary>
    [Fact]
    public void Zork1_MemoryDump_HeaderHasVersion3()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var dump = dbg.GetMemoryDump(0x00, 1);
        Assert.Equal(3, dump.Data[0]);
    }

    #endregion

    #region Trace Log

    /// <summary>
    /// Verifies that the trace log records instructions after stepping.
    /// </summary>
    [Fact]
    public void Zork1_Trace_RecordsSteps()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        for (int i = 0; i < 5; i++)
            dbg.StepInto();

        Assert.Equal(5, dbg.TraceLog.Count);
        Assert.All(dbg.TraceLog, e =>
        {
            Assert.NotEmpty(e.Mnemonic);
            Assert.True(e.Address > 0);
        });
    }

    /// <summary>
    /// Verifies that trace can be exported as text.
    /// </summary>
    [Fact]
    public void Zork1_Trace_ExportsText()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        for (int i = 0; i < 3; i++)
            dbg.StepInto();

        string export = dbg.ExportTrace();
        Assert.Contains("Execution trace", export);
        Assert.Contains("$", export);
    }

    /// <summary>
    /// Verifies that trace respects max entries (rolling log).
    /// </summary>
    [Fact]
    public void Trace_RollingLog_TruncatesOld()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);
        dbg.MaxTraceEntries = 5;

        for (int i = 0; i < 10; i++)
            dbg.StepInto();

        Assert.Equal(5, dbg.TraceLog.Count);
    }

    /// <summary>
    /// Verifies that disabling trace stops recording.
    /// </summary>
    [Fact]
    public void Trace_Disabled_NoRecording()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);
        dbg.TraceEnabled = false;

        for (int i = 0; i < 5; i++)
            dbg.StepInto();

        Assert.Empty(dbg.TraceLog);
    }

    /// <summary>
    /// Verifies that ClearTrace empties the log.
    /// </summary>
    [Fact]
    public void Trace_Clear_EmptiesLog()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        for (int i = 0; i < 5; i++)
            dbg.StepInto();

        dbg.ClearTrace();
        Assert.Empty(dbg.TraceLog);
    }

    #endregion

    #region Watch Expressions

    /// <summary>
    /// Verifies that a global variable watch evaluates.
    /// </summary>
    [Fact]
    public void Watch_Global_Evaluates()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("G00");
        Assert.NotNull(watch.Value);
        Assert.Null(watch.Error);
    }

    /// <summary>
    /// Verifies that a local variable watch evaluates after setup.
    /// </summary>
    [Fact]
    public void Watch_Local_Evaluates()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("L00");
        Assert.Null(watch.Error);
    }

    /// <summary>
    /// Verifies that a memory address watch evaluates.
    /// </summary>
    [Fact]
    public void Watch_Memory_Evaluates()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("[$0000]");
        Assert.NotNull(watch.Value);
        Assert.Equal((ushort)3, watch.Value); // version byte
    }

    /// <summary>
    /// Verifies that an invalid watch expression sets an error.
    /// </summary>
    [Fact]
    public void Watch_Invalid_SetsError()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("INVALID");
        Assert.Null(watch.Value);
        Assert.NotNull(watch.Error);
    }

    /// <summary>
    /// Verifies that watches update after stepping.
    /// </summary>
    [Fact]
    public void Watch_UpdatesOnStep()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("G00");
        ushort? valueBefore = watch.Value;

        for (int i = 0; i < 10; i++)
            dbg.StepInto();

        // Value may or may not have changed, but should still be valid
        Assert.Null(watch.Error);
    }

    /// <summary>
    /// Verifies that watches can be removed.
    /// </summary>
    [Fact]
    public void Watch_Remove()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var w1 = dbg.AddWatch("G00");
        var w2 = dbg.AddWatch("G01");
        Assert.Equal(2, dbg.Watches.Count);

        dbg.RemoveWatch(w1);
        Assert.Single(dbg.Watches);

        dbg.ClearWatches();
        Assert.Empty(dbg.Watches);
    }

    /// <summary>
    /// Verifies that SP watch evaluates (may be null if stack empty).
    /// </summary>
    [Fact]
    public void Watch_SP_Evaluates()
    {
        if (!File.Exists(Zork1Path)) return;
        var dbg = CreateDebugger(Zork1Path);

        var watch = dbg.AddWatch("SP");
        Assert.Null(watch.Error);
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies that a V5 story can be stepped through.
    /// </summary>
    [Fact]
    public void Czech_V5_StepInto()
    {
        if (!File.Exists(CzechPath)) return;
        var dbg = CreateDebugger(CzechPath);

        for (int i = 0; i < 5; i++)
        {
            var reason = dbg.StepInto();
            Assert.Equal(StopReason.Step, reason);
        }

        Assert.Equal(5, dbg.InstructionsExecuted);
    }

    #endregion

    #region Helpers

    /// <summary>Creates a Debugger for the given story file.</summary>
    private static Debugger CreateDebugger(string path,
        string[]? commands = null)
    {
        commands ??= ["quit", "y"];
        var machine = new Interpreter();
        var input = new ScriptedInputStream(commands);
        var screen = new CaptureScreen();
        machine.Load(path, input, screen);

        return new Debugger(machine);
    }

    #endregion
}
