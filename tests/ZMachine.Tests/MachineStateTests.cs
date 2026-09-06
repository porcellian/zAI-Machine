namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for MachineState — variable read/write (stack, locals, globals),
/// indirect variable references, StoreResult, ExecuteBranch, and per-frame
/// stack/local isolation via CallStack.
/// </summary>
public class MachineStateTests
{
    #region Stack Operations (Variable 0)

    [Fact]
    public void WriteVariable0_PushesToStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);
        Assert.Single(state.CallStack.CurrentFrame!.EvalStack);
    }

    [Fact]
    public void ReadVariable0_PopsFromStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 100);
        state.WriteVariable(0, 200);

        Assert.Equal(200, state.ReadVariable(0));
        Assert.Equal(100, state.ReadVariable(0));
        Assert.Empty(state.CallStack.CurrentFrame!.EvalStack);
    }

    [Fact]
    public void ReadVariable0_EmptyStack_Throws()
    {
        var state = CreateState();
        Assert.Throws<InvalidOperationException>(() => state.ReadVariable(0));
    }

    [Fact]
    public void Stack_LIFO_Order()
    {
        var state = CreateState();
        state.WriteVariable(0, 1);
        state.WriteVariable(0, 2);
        state.WriteVariable(0, 3);

        Assert.Equal(3, state.ReadVariable(0));
        Assert.Equal(2, state.ReadVariable(0));
        Assert.Equal(1, state.ReadVariable(0));
    }

    #endregion

    #region Local Variables (1–15)

    [Fact]
    public void WriteLocal_ThenRead()
    {
        var state = CreateState(localCount: 5);
        state.WriteVariable(3, 0x1234);
        Assert.Equal(0x1234, state.ReadVariable(3));
    }

    [Fact]
    public void LocalsAreIndependent()
    {
        var state = CreateState(localCount: 15);
        state.WriteVariable(1, 0xAAAA);
        state.WriteVariable(15, 0xBBBB);

        Assert.Equal(0xAAAA, state.ReadVariable(1));
        Assert.Equal(0xBBBB, state.ReadVariable(15));
    }

    [Fact]
    public void ReadLocal_BeyondLocalCount_Throws()
    {
        var state = CreateState(localCount: 3);
        Assert.Throws<InvalidOperationException>(() => state.ReadVariable(4));
    }

    [Fact]
    public void WriteLocal_BeyondLocalCount_Throws()
    {
        var state = CreateState(localCount: 3);
        Assert.Throws<InvalidOperationException>(() => state.WriteVariable(4, 0));
    }

    #endregion

    #region Global Variables (16–255)

    [Fact]
    public void WriteGlobal_ThenRead()
    {
        var state = CreateState();
        state.WriteVariable(16, 0xDEAD);
        Assert.Equal(0xDEAD, state.ReadVariable(16));
    }

    [Fact]
    public void Global_WritesToMemory()
    {
        var (memory, state) = CreateMemoryAndState();
        state.WriteVariable(16, 0x4242);

        ushort value = memory.ReadWord(0x20);
        Assert.Equal(0x4242, value);
    }

    [Fact]
    public void Global_ReadsFromMemory()
    {
        var (memory, state) = CreateMemoryAndState();
        memory.WriteWord(0x22, 0xBEEF);

        Assert.Equal(0xBEEF, state.ReadVariable(17));
    }

    [Fact]
    public void Global_HighestIndex()
    {
        var state = CreateState();
        state.WriteVariable(255, 0x1111);
        Assert.Equal(0x1111, state.ReadVariable(255));
    }

    #endregion

    #region Indirect Variable References

    [Fact]
    public void ReadVariableIndirect_Stack_PeeksInsteadOfPop()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);

        ushort value = state.ReadVariableIndirect(0);
        Assert.Equal(42, value);
        Assert.Single(state.CallStack.CurrentFrame!.EvalStack);
    }

    [Fact]
    public void WriteVariableIndirect_Stack_ReplacesTop()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);

        state.WriteVariableIndirect(0, 99);
        Assert.Single(state.CallStack.CurrentFrame!.EvalStack);
        Assert.Equal(99, state.ReadVariable(0));
    }

    [Fact]
    public void ReadVariableIndirect_EmptyStack_Throws()
    {
        var state = CreateState();
        Assert.Throws<InvalidOperationException>(() => state.ReadVariableIndirect(0));
    }

    [Fact]
    public void WriteVariableIndirect_EmptyStack_Throws()
    {
        var state = CreateState();
        Assert.Throws<InvalidOperationException>(() => state.WriteVariableIndirect(0, 99));
    }

    [Fact]
    public void ReadVariableIndirect_Local_SameAsRead()
    {
        var state = CreateState(localCount: 5);
        state.WriteVariable(3, 0x1234);
        Assert.Equal(0x1234, state.ReadVariableIndirect(3));
    }

    [Fact]
    public void WriteVariableIndirect_Global_SameAsWrite()
    {
        var state = CreateState();
        state.WriteVariableIndirect(20, 0xABCD);
        Assert.Equal(0xABCD, state.ReadVariable(20));
    }

    #endregion

    #region StoreResult

    [Fact]
    public void StoreResult_ToStack()
    {
        var state = CreateState();
        state.StoreResult(0, 0x5555);
        Assert.Equal(0x5555, state.ReadVariable(0));
    }

    [Fact]
    public void StoreResult_ToLocal()
    {
        var state = CreateState(localCount: 5);
        state.StoreResult(2, 0x7777);
        Assert.Equal(0x7777, state.ReadVariable(2));
    }

    [Fact]
    public void StoreResult_ToGlobal()
    {
        var state = CreateState();
        state.StoreResult(16, 0x9999);
        Assert.Equal(0x9999, state.ReadVariable(16));
    }

    #endregion

    #region ExecuteBranch

    [Fact]
    public void ExecuteBranch_ConditionTrue_BranchOnTrue_Jumps()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 10 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.Jump, result.Action);
        Assert.Equal(108, result.TargetAddress); // 100 + 10 - 2
    }

    [Fact]
    public void ExecuteBranch_ConditionFalse_BranchOnTrue_DontBranch()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 10 };
        var result = MachineState.ExecuteBranch(false, branch, 100);

        Assert.Equal(BranchAction.DontBranch, result.Action);
    }

    [Fact]
    public void ExecuteBranch_ConditionFalse_BranchOnFalse_Jumps()
    {
        var branch = new BranchInfo { BranchOnTrue = false, Offset = 5 };
        var result = MachineState.ExecuteBranch(false, branch, 200);

        Assert.Equal(BranchAction.Jump, result.Action);
        Assert.Equal(203, result.TargetAddress); // 200 + 5 - 2
    }

    [Fact]
    public void ExecuteBranch_ConditionTrue_BranchOnFalse_DontBranch()
    {
        var branch = new BranchInfo { BranchOnTrue = false, Offset = 5 };
        var result = MachineState.ExecuteBranch(true, branch, 200);

        Assert.Equal(BranchAction.DontBranch, result.Action);
    }

    [Fact]
    public void ExecuteBranch_RFalse()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 0 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.ReturnFalse, result.Action);
    }

    [Fact]
    public void ExecuteBranch_RTrue()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 1 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.ReturnTrue, result.Action);
    }

    [Fact]
    public void ExecuteBranch_NegativeOffset()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = -5 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.Jump, result.Action);
        Assert.Equal(93, result.TargetAddress); // 100 + (-5) - 2
    }

    [Fact]
    public void ExecuteBranch_Offset2_JumpsToSameAddress()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 2 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.Jump, result.Action);
        Assert.Equal(100, result.TargetAddress);
    }

    [Fact]
    public void ExecuteBranch_RFalse_ConditionNotMet_DontBranch()
    {
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 0 };
        var result = MachineState.ExecuteBranch(false, branch, 100);

        Assert.Equal(BranchAction.DontBranch, result.Action);
    }

    #endregion

    #region No Frame — Operations Throw

    [Fact]
    public void ReadVariable_NoFrame_Throws()
    {
        var state = CreateStateWithoutFrame();
        Assert.Throws<InvalidOperationException>(() => state.ReadVariable(0));
    }

    [Fact]
    public void WriteVariable_NoFrame_Throws()
    {
        var state = CreateStateWithoutFrame();
        Assert.Throws<InvalidOperationException>(() => state.WriteVariable(0, 42));
    }

    [Fact]
    public void ReadLocal_NoFrame_Throws()
    {
        var state = CreateStateWithoutFrame();
        Assert.Throws<InvalidOperationException>(() => state.ReadVariable(1));
    }

    [Fact]
    public void GlobalAccess_NoFrame_StillWorks()
    {
        var state = CreateStateWithoutFrame();
        state.WriteVariable(16, 0x1234);
        Assert.Equal(0x1234, state.ReadVariable(16));
    }

    #endregion

    #region Frame Isolation — Per-Frame Eval Stack

    [Fact]
    public void PushFrame_GetsCleanStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 111);
        state.WriteVariable(0, 222);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        Assert.Empty(state.CallStack.CurrentFrame!.EvalStack);
    }

    [Fact]
    public void PopFrame_RestoresOuterStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 111);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        state.WriteVariable(0, 999);

        state.CallStack.PopFrame();
        Assert.Equal(111, state.ReadVariable(0));
    }

    [Fact]
    public void NestedFrames_StacksAreIndependent()
    {
        var state = CreateState();
        state.WriteVariable(0, 10);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        state.WriteVariable(0, 20);

        state.CallStack.PushFrame(new CallFrame(0x200, 0, false, 0, 0));
        state.WriteVariable(0, 30);

        Assert.Equal(30, state.ReadVariable(0));
        state.CallStack.PopFrame();
        Assert.Equal(20, state.ReadVariable(0));
        state.CallStack.PopFrame();
        Assert.Equal(10, state.ReadVariable(0));
    }

    #endregion

    #region Frame Isolation — Per-Frame Locals

    [Fact]
    public void PushFrame_GetsOwnLocals()
    {
        var state = CreateState(localCount: 3);
        state.WriteVariable(1, 0xAAAA);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 5, 0));
        Assert.Equal(0, state.ReadVariable(1));
        state.WriteVariable(1, 0xBBBB);

        state.CallStack.PopFrame();
        Assert.Equal(0xAAAA, state.ReadVariable(1));
    }

    [Fact]
    public void InnerFrame_DifferentLocalCount()
    {
        var state = CreateState(localCount: 2);
        Assert.Throws<InvalidOperationException>(() => state.ReadVariable(3));

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 5, 0));
        state.WriteVariable(3, 0x1234);
        Assert.Equal(0x1234, state.ReadVariable(3));
    }

    #endregion

    #region CallFrame Properties

    [Fact]
    public void CallFrame_ReturnPC_Preserved()
    {
        var frame = new CallFrame(0x5472, 5, false, 3, 2);
        Assert.Equal(0x5472, frame.ReturnPC);
    }

    [Fact]
    public void CallFrame_StoreVariable_Preserved()
    {
        var frame = new CallFrame(0x100, 5, false, 3, 2);
        Assert.Equal(5, frame.StoreVariable);
    }

    [Fact]
    public void CallFrame_DiscardResult_Preserved()
    {
        var frame = new CallFrame(0x100, 0, true, 3, 2);
        Assert.True(frame.DiscardResult);
    }

    [Fact]
    public void CallFrame_ArgumentCount_Preserved()
    {
        var frame = new CallFrame(0x100, 0, false, 5, 3);
        Assert.Equal(3, frame.ArgumentCount);
    }

    [Fact]
    public void CallFrame_LocalCount_Preserved()
    {
        var frame = new CallFrame(0x100, 0, false, 7, 0);
        Assert.Equal(7, frame.LocalCount);
    }

    [Fact]
    public void CallFrame_Locals_InitToZero()
    {
        var frame = new CallFrame(0x100, 0, false, 15, 0);
        for (int i = 1; i <= 15; i++)
            Assert.Equal(0, frame.Locals[i]);
    }

    #endregion

    #region CallStack Operations

    [Fact]
    public void CallStack_Empty_CurrentFrameIsNull()
    {
        var state = CreateStateWithoutFrame();
        Assert.Null(state.CallStack.CurrentFrame);
    }

    [Fact]
    public void CallStack_Empty_FrameCountIsZero()
    {
        var state = CreateStateWithoutFrame();
        Assert.Equal(0, state.CallStack.FrameCount);
    }

    [Fact]
    public void CallStack_PushIncrementsFrameCount()
    {
        var state = CreateStateWithoutFrame();
        state.CallStack.PushFrame(new CallFrame(0, 0, false, 0, 0));
        Assert.Equal(1, state.CallStack.FrameCount);
        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        Assert.Equal(2, state.CallStack.FrameCount);
    }

    [Fact]
    public void CallStack_PopDecrementsFrameCount()
    {
        var state = CreateState();
        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        Assert.Equal(2, state.CallStack.FrameCount);

        state.CallStack.PopFrame();
        Assert.Equal(1, state.CallStack.FrameCount);
    }

    [Fact]
    public void CallStack_PopEmpty_Throws()
    {
        var state = CreateStateWithoutFrame();
        Assert.Throws<InvalidOperationException>(() => state.CallStack.PopFrame());
    }

    [Fact]
    public void CallStack_GetFramesBottomUp_ReturnsCorrectOrder()
    {
        var state = CreateStateWithoutFrame();
        var frame1 = new CallFrame(0, 0, false, 0, 0);
        var frame2 = new CallFrame(0x100, 0, false, 0, 0);
        var frame3 = new CallFrame(0x200, 0, false, 0, 0);

        state.CallStack.PushFrame(frame1);
        state.CallStack.PushFrame(frame2);
        state.CallStack.PushFrame(frame3);

        var frames = state.CallStack.GetFramesBottomUp().ToList();
        Assert.Equal(3, frames.Count);
        Assert.Same(frame1, frames[0]);
        Assert.Same(frame2, frames[1]);
        Assert.Same(frame3, frames[2]);
    }

    #endregion

    #region Indirect References Across Frames

    [Fact]
    public void IndirectRead_PeeksCurrentFrameStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        state.WriteVariable(0, 99);

        Assert.Equal(99, state.ReadVariableIndirect(0));
        Assert.Single(state.CallStack.CurrentFrame!.EvalStack);
    }

    [Fact]
    public void IndirectWrite_ReplacesCurrentFrameStackTop()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);

        state.CallStack.PushFrame(new CallFrame(0x100, 0, false, 0, 0));
        state.WriteVariable(0, 99);
        state.WriteVariableIndirect(0, 77);

        Assert.Equal(77, state.ReadVariable(0));

        state.CallStack.PopFrame();
        Assert.Equal(42, state.ReadVariable(0));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a MachineState with a synthetic V3 memory and one initial
    /// call frame pushed (simulating the main routine).
    /// </summary>
    private static MachineState CreateState(int localCount = 0)
    {
        var (_, state) = CreateMemoryAndState(localCount);
        return state;
    }

    private static (Memory memory, MachineState state) CreateMemoryAndState(int localCount = 0)
    {
        var data = new byte[1024];
        data[0] = 3; // V3
        data[0x04] = 0x03; data[0x05] = 0x00; // high base at $300
        data[0x0C] = 0x00; data[0x0D] = 0x20; // globals at $20
        data[0x0E] = 0x02; data[0x0F] = 0x00; // static base at $200

        var memory = new Memory();
        memory.LoadStory(data);

        var state = new MachineState(memory, 0x20);
        state.CallStack.PushFrame(new CallFrame(0, 0, false, localCount, 0));

        return (memory, state);
    }

    /// <summary>
    /// Creates a MachineState with no call frame — for testing behavior
    /// when operations are attempted without an active frame.
    /// </summary>
    private static MachineState CreateStateWithoutFrame()
    {
        var data = new byte[1024];
        data[0] = 3;
        data[0x04] = 0x03; data[0x05] = 0x00;
        data[0x0C] = 0x00; data[0x0D] = 0x20;
        data[0x0E] = 0x02; data[0x0F] = 0x00;

        var memory = new Memory();
        memory.LoadStory(data);

        return new MachineState(memory, 0x20);
    }

    #endregion
}
