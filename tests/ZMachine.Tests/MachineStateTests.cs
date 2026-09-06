namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for MachineState — variable read/write (stack, locals, globals),
/// indirect variable references, StoreResult, and ExecuteBranch.
/// </summary>
public class MachineStateTests
{
    #region Stack Operations (Variable 0)

    [Fact]
    public void WriteVariable0_PushesToStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);
        Assert.Single(state.Stack);
    }

    [Fact]
    public void ReadVariable0_PopsFromStack()
    {
        var state = CreateState();
        state.WriteVariable(0, 100);
        state.WriteVariable(0, 200);

        Assert.Equal(200, state.ReadVariable(0));
        Assert.Equal(100, state.ReadVariable(0));
        Assert.Empty(state.Stack);
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

        // Global 16 is at globalsAddress + 0 (first global)
        // Globals address for our test memory is 0x20
        ushort value = memory.ReadWord(0x20);
        Assert.Equal(0x4242, value);
    }

    [Fact]
    public void Global_ReadsFromMemory()
    {
        var (memory, state) = CreateMemoryAndState();
        // Write directly to memory at global 17 = globalsAddress + 2
        memory.WriteWord(0x22, 0xBEEF);

        Assert.Equal(0xBEEF, state.ReadVariable(17));
    }

    [Fact]
    public void Global_HighestIndex()
    {
        var state = CreateState();
        // Variable 255 = global (255-16) = global 239
        // At offset 239*2 = 478 from globals address
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
        Assert.Single(state.Stack); // still there
    }

    [Fact]
    public void WriteVariableIndirect_Stack_ReplacesTop()
    {
        var state = CreateState();
        state.WriteVariable(0, 42);

        state.WriteVariableIndirect(0, 99);
        Assert.Single(state.Stack); // count unchanged
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
        // Offset 2 means target = addressAfterBranch + 2 - 2 = addressAfterBranch
        // This is an infinite loop (or a no-op branch)
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 2 };
        var result = MachineState.ExecuteBranch(true, branch, 100);

        Assert.Equal(BranchAction.Jump, result.Action);
        Assert.Equal(100, result.TargetAddress);
    }

    [Fact]
    public void ExecuteBranch_RFalse_ConditionNotMet_DontBranch()
    {
        // Even for rfalse/rtrue, if condition doesn't match, don't branch
        var branch = new BranchInfo { BranchOnTrue = true, Offset = 0 };
        var result = MachineState.ExecuteBranch(false, branch, 100);

        Assert.Equal(BranchAction.DontBranch, result.Action);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a MachineState with a synthetic V3 memory that has
    /// enough space for 240 global variables in dynamic memory.
    /// </summary>
    private static MachineState CreateState(int localCount = 0)
    {
        var (_, state) = CreateMemoryAndState(localCount);
        return state;
    }

    private static (Memory memory, MachineState state) CreateMemoryAndState(int localCount = 0)
    {
        // Need 240 globals × 2 bytes = 480 bytes starting at globals address.
        // Globals at $20, so we need at least $20 + 480 = $200 (512) bytes
        // with static base above the globals area.
        var data = new byte[1024];
        data[0] = 3; // V3
        data[0x04] = 0x03; data[0x05] = 0x00; // high base at $300
        data[0x0C] = 0x00; data[0x0D] = 0x20; // globals at $20
        data[0x0E] = 0x02; data[0x0F] = 0x00; // static base at $200

        var memory = new Memory();
        memory.LoadStory(data);

        var state = new MachineState(memory, 0x20)
        {
            LocalCount = localCount
        };

        return (memory, state);
    }

    #endregion
}
