namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for ControlFlowOps — Z-Machine control flow opcodes: routine
/// call/return cycles, catch/throw stack unwinding, jump, restart,
/// verify checksum, and check_arg_count. Uses both synthetic memory
/// for unit tests and zork1.z3 for verify checksum validation.
/// </summary>
public class ControlFlowOpsTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region Call / Return cycle

    [Fact]
    public void Call_PackedAddress0_ReturnsFalse()
    {
        var (ops, state, _) = CreateV3Ops();
        PushInitialFrame(state);

        bool called = ops.Call(0, [], 0, 0, false, 0x100);

        Assert.False(called);
    }

    [Fact]
    public void Call_PushesFrame_And_SetsPCPastHeader_V3()
    {
        var (ops, state, memory) = CreateV3Ops();
        PushInitialFrame(state);

        // Place a routine at packed address 0x10 → byte 0x20 (V3: ×2).
        // Header: 2 locals, then 2 initial-value words.
        int routineAddr = 0x20;
        memory.WriteByte(routineAddr, 2);          // 2 locals
        memory.WriteWord(routineAddr + 1, 0x0042); // local 1 init
        memory.WriteWord(routineAddr + 3, 0x0099); // local 2 init

        bool called = ops.Call(0x10, [0xAAAA], 1, 5, false, 0x100);

        Assert.True(called);
        Assert.Equal(2, state.CallStack.FrameCount);

        // PC should be past header: routineAddr + 1 + 2*2 = 0x25
        Assert.Equal(routineAddr + 5, state.PC);

        // Local 1 overwritten by arg, local 2 keeps initial value.
        var frame = state.CallStack.CurrentFrame!;
        Assert.Equal((ushort)0xAAAA, frame.Locals[1]);
        Assert.Equal((ushort)0x0099, frame.Locals[2]);
    }

    [Fact]
    public void Call_V5_LocalsInitToZero()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        // V5 routine at byte address 0x200 (packed 0x80, ×4).
        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 3); // 3 locals

        bool called = ops.Call(0x80, [], 0, 5, false, 0x100);

        Assert.True(called);
        // PC = routineAddr + 1 (no initial value words in V5).
        Assert.Equal(routineAddr + 1, state.PC);

        var frame = state.CallStack.CurrentFrame!;
        Assert.Equal(0, frame.Locals[1]);
        Assert.Equal(0, frame.Locals[2]);
        Assert.Equal(0, frame.Locals[3]);
    }

    [Fact]
    public void Call_ArgsOverwriteLocals_ExcessIgnored()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 2); // 2 locals

        // Pass 3 args — only first 2 should be used.
        ops.Call(0x80, [10, 20, 30], 3, 5, false, 0x100);

        var frame = state.CallStack.CurrentFrame!;
        Assert.Equal((ushort)10, frame.Locals[1]);
        Assert.Equal((ushort)20, frame.Locals[2]);
        Assert.Equal(3, frame.ArgumentCount);
    }

    [Fact]
    public void Return_PopsFrame_RestoresPC_StoresResult()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 1);

        // Store result in local 1 of the calling frame. Use global var 16.
        ops.Call(0x80, [0xFF], 1, 16, false, 0x300);

        Assert.Equal(2, state.CallStack.FrameCount);

        ops.Return(42);

        Assert.Equal(1, state.CallStack.FrameCount);
        Assert.Equal(0x300, state.PC);
        // Global 16 = variable 16, check via ReadVariable.
        Assert.Equal((ushort)42, state.ReadVariable(16));
    }

    [Fact]
    public void Return_DiscardResult_DoesNotStore()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 0);

        ops.Call(0x80, [], 0, 0, true, 0x300);
        ops.Return(999);

        Assert.Equal(1, state.CallStack.FrameCount);
        Assert.Equal(0x300, state.PC);
    }

    [Fact]
    public void ReturnTrue_Returns1()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 0);

        ops.Call(0x80, [], 0, 16, false, 0x300);
        ops.ReturnTrue();

        Assert.Equal((ushort)1, state.ReadVariable(16));
    }

    [Fact]
    public void ReturnFalse_Returns0()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        // Write a nonzero value first so we can distinguish.
        state.WriteVariable(16, 0xFFFF);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 0);

        ops.Call(0x80, [], 0, 16, false, 0x300);
        ops.ReturnFalse();

        Assert.Equal((ushort)0, state.ReadVariable(16));
    }

    [Fact]
    public void ReturnPopped_PopsStackAndReturns()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        int routineAddr = 0x200;
        memory.WriteByte(routineAddr, 0);

        ops.Call(0x80, [], 0, 16, false, 0x300);

        // Push a value onto the callee's eval stack.
        state.WriteVariable(0, 77);

        ops.ReturnPopped();

        Assert.Equal((ushort)77, state.ReadVariable(16));
    }

    #endregion

    #region Call/Return nested

    [Fact]
    public void NestedCalls_UnwindCorrectly()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        // Routine A at 0x200 (packed 0x80)
        memory.WriteByte(0x200, 1);
        // Routine B at 0x300 (packed 0xC0)
        memory.WriteByte(0x300, 0);

        ops.Call(0x80, [10], 1, 16, false, 0x100);
        Assert.Equal(2, state.CallStack.FrameCount);

        ops.Call(0xC0, [], 0, 17, false, 0x201);
        Assert.Equal(3, state.CallStack.FrameCount);

        ops.Return(99);
        Assert.Equal(2, state.CallStack.FrameCount);
        Assert.Equal(0x201, state.PC);

        ops.Return(42);
        Assert.Equal(1, state.CallStack.FrameCount);
        Assert.Equal(0x100, state.PC);
    }

    #endregion

    #region Jump

    [Fact]
    public void Jump_PositiveOffset()
    {
        var (ops, state, _) = CreateV5Ops();
        PushInitialFrame(state);
        state.PC = 0x100;

        ops.Jump(10, 0x102);

        // target = 0x102 + 10 - 2 = 0x10A
        Assert.Equal(0x10A, state.PC);
    }

    [Fact]
    public void Jump_NegativeOffset_BackwardsJump()
    {
        var (ops, state, _) = CreateV5Ops();
        PushInitialFrame(state);
        state.PC = 0x200;

        ops.Jump(-5, 0x202);

        // target = 0x202 + (-5) - 2 = 0x1FB
        Assert.Equal(0x1FB, state.PC);
    }

    #endregion

    #region Catch / Throw

    [Fact]
    public void Catch_ReturnsFrameCount()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        memory.WriteByte(0x200, 0);
        ops.Call(0x80, [], 0, 16, false, 0x100);

        ushort caught = ops.Catch();
        Assert.Equal((ushort)2, caught);
    }

    [Fact]
    public void Throw_UnwindsToFrameCount_AndReturns()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        // Call two routines.
        memory.WriteByte(0x200, 0);
        memory.WriteByte(0x300, 0);

        ops.Call(0x80, [], 0, 16, false, 0x100);
        ushort catchFrame = ops.Catch(); // = 2

        ops.Call(0xC0, [], 0, 17, false, 0x201);
        Assert.Equal(3, state.CallStack.FrameCount);

        // Throw back to the caught frame.
        ops.Throw(42, catchFrame);

        // Should be back at frame count 1 (initial frame), having
        // returned from frame 2.
        Assert.Equal(1, state.CallStack.FrameCount);
        Assert.Equal(0x100, state.PC);
        Assert.Equal((ushort)42, state.ReadVariable(16));
    }

    #endregion

    #region check_arg_count

    [Fact]
    public void CheckArgCount_WithinRange_True()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        memory.WriteByte(0x200, 3);
        ops.Call(0x80, [1, 2, 3], 3, 0, true, 0x100);

        Assert.True(ops.CheckArgCount(1));
        Assert.True(ops.CheckArgCount(2));
        Assert.True(ops.CheckArgCount(3));
    }

    [Fact]
    public void CheckArgCount_BeyondRange_False()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        memory.WriteByte(0x200, 3);
        ops.Call(0x80, [1], 1, 0, true, 0x100);

        Assert.True(ops.CheckArgCount(1));
        Assert.False(ops.CheckArgCount(2));
    }

    #endregion

    #region Piracy / Nop

    [Fact]
    public void Piracy_AlwaysTrue()
    {
        var (ops, _, _) = CreateV5Ops();
        Assert.True(ops.Piracy());
    }

    [Fact]
    public void Nop_DoesNothing()
    {
        var (ops, state, _) = CreateV5Ops();
        PushInitialFrame(state);
        state.PC = 0x100;

        ops.Nop();

        Assert.Equal(0x100, state.PC);
    }

    #endregion

    #region Verify

    [Fact]
    public void Verify_Zork1_ChecksumMatches()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int globalsAddr = memory.ReadWord(0x0C);
        var state = new MachineState(memory, globalsAddr);
        var ops = new ControlFlowOps(memory, state, version);

        Assert.True(ops.Verify());
    }

    [Fact]
    public void Verify_CorruptedFile_Fails()
    {
        var memory = new Memory();
        byte[] data = File.ReadAllBytes(Zork1Path);

        // Corrupt a byte in the checksum range.
        data[0x50] ^= 0xFF;
        memory.LoadStory(data);

        int version = memory.ReadByte(0x00);
        int globalsAddr = memory.ReadWord(0x0C);
        var state = new MachineState(memory, globalsAddr);
        var ops = new ControlFlowOps(memory, state, version);

        Assert.False(ops.Verify());
    }

    [Fact]
    public void Verify_StillPassesAfterDynamicMemoryModified()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int globalsAddr = memory.ReadWord(0x0C);
        var state = new MachineState(memory, globalsAddr);
        var ops = new ControlFlowOps(memory, state, version);

        // Modify dynamic memory (globals area) — this is within the
        // $40..fileLength checksum range and would cause a mismatch
        // if ComputeChecksum used the live _bytes instead of OriginalBytes.
        memory.WriteByte(globalsAddr, 0xFF);
        memory.WriteByte(globalsAddr + 1, 0xFF);

        Assert.True(ops.Verify());
    }

    #endregion

    #region Restart

    [Fact]
    public void Restart_RestoresDynamicMemory()
    {
        var (ops, state, memory) = CreateV3Ops();
        PushInitialFrame(state);

        // Mutate dynamic memory.
        byte original = memory.ReadByte(0x10);
        memory.WriteByte(0x10, (byte)(original ^ 0xFF));
        Assert.NotEqual(original, memory.ReadByte(0x10));

        ops.Restart();

        Assert.Equal(original, memory.ReadByte(0x10));
    }

    [Fact]
    public void Restart_ResetsCallStackToBaseFrame()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        memory.WriteByte(0x200, 0);
        ops.Call(0x80, [], 0, 0, true, 0x100);
        Assert.Equal(2, state.CallStack.FrameCount);

        ops.Restart();

        // Must have exactly one base frame so post-restart instructions
        // can use the eval stack and local variables.
        Assert.Equal(1, state.CallStack.FrameCount);
    }

    [Fact]
    public void Restart_PostRestartExecutionWorks()
    {
        var (ops, state, memory) = CreateV5Ops();
        PushInitialFrame(state);

        memory.WriteByte(0x200, 0);
        ops.Call(0x80, [], 0, 0, true, 0x100);

        ops.Restart();

        // Verify eval stack operations work on the new base frame.
        state.WriteVariable(0, 42); // push
        Assert.Equal((ushort)42, state.ReadVariable(0)); // pop
    }

    [Fact]
    public void Restart_SetsPCToInitialPC()
    {
        var (ops, state, memory) = CreateV3Ops();
        PushInitialFrame(state);
        state.PC = 0x999;

        int initialPC = ops.Restart();

        Assert.Equal(initialPC, state.PC);
        // Initial PC comes from header word $06.
        int expected = memory.ReadWord(0x06);
        Assert.Equal(expected, state.PC);
    }

    #endregion

    #region Helpers

    private static (ControlFlowOps Ops, MachineState State, Memory Memory) CreateV3Ops()
    {
        var memory = new Memory();
        byte[] story = new byte[0x10000];
        story[0x00] = 3; // V3
        // Header $04: high memory base
        story[0x04] = 0x80; story[0x05] = 0x00;
        // Header $06: initial PC
        story[0x06] = 0x08; story[0x07] = 0x00;
        // Header $0C: globals address
        story[0x0C] = 0x10; story[0x0D] = 0x00;
        // Header $0E: static memory base
        story[0x0E] = 0x80; story[0x0F] = 0x00;
        memory.LoadStory(story);

        int globalsAddr = memory.ReadWord(0x0C);
        var state = new MachineState(memory, globalsAddr);
        var ops = new ControlFlowOps(memory, state, 3);
        return (ops, state, memory);
    }

    private static (ControlFlowOps Ops, MachineState State, Memory Memory) CreateV5Ops()
    {
        var memory = new Memory();
        byte[] story = new byte[0x10000];
        story[0x00] = 5; // V5
        story[0x04] = 0x80; story[0x05] = 0x00;
        story[0x06] = 0x08; story[0x07] = 0x00;
        story[0x0C] = 0x10; story[0x0D] = 0x00;
        story[0x0E] = 0x80; story[0x0F] = 0x00;
        memory.LoadStory(story);

        int globalsAddr = memory.ReadWord(0x0C);
        var state = new MachineState(memory, globalsAddr);
        var ops = new ControlFlowOps(memory, state, 5);
        return (ops, state, memory);
    }

    private static void PushInitialFrame(MachineState state)
    {
        state.CallStack.PushFrame(new CallFrame(0, 0, false, 0, 0));
    }

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName!;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
