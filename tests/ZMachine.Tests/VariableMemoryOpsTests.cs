namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for Z-Machine variable manipulation, memory read/write, and
/// table opcodes. Covers indirect stack semantics, @scan_table word/byte
/// modes, and @copy_table overlap scenarios.
/// </summary>
public class VariableMemoryOpsTests
{
    #region @load / @store (indirect)

    [Fact]
    public void Load_Local()
    {
        var state = CreateState();
        state.WriteVariable(1, 42);

        ushort result = VariableMemoryOps.Load(state, 1);
        Assert.Equal((ushort)42, result);
    }

    [Fact]
    public void Load_Stack_Peeks()
    {
        var state = CreateState();
        state.WriteVariable(0, 99); // push 99

        // Indirect load of variable 0 should peek, not pop.
        ushort result = VariableMemoryOps.Load(state, 0);
        Assert.Equal((ushort)99, result);

        // Value should still be there.
        ushort again = state.ReadVariable(0); // pop
        Assert.Equal((ushort)99, again);
    }

    [Fact]
    public void Store_Local()
    {
        var state = CreateState();
        VariableMemoryOps.Store(state, 1, 77);

        Assert.Equal((ushort)77, state.ReadVariable(1));
    }

    [Fact]
    public void Store_Stack_Replaces()
    {
        var state = CreateState();
        state.WriteVariable(0, 10); // push 10

        // Indirect store to variable 0 should replace top, not push.
        VariableMemoryOps.Store(state, 0, 20);

        ushort result = state.ReadVariable(0); // pop
        Assert.Equal((ushort)20, result);
    }

    #endregion

    #region @inc / @dec (indirect, signed)

    [Fact]
    public void Inc_Local()
    {
        var state = CreateState();
        state.WriteVariable(1, 5);

        VariableMemoryOps.Inc(state, 1);
        Assert.Equal((ushort)6, state.ReadVariable(1));
    }

    [Fact]
    public void Inc_Overflow()
    {
        var state = CreateState();
        state.WriteVariable(1, 0x7FFF); // 32767

        VariableMemoryOps.Inc(state, 1);
        Assert.Equal(-32768, (short)state.ReadVariable(1));
    }

    [Fact]
    public void Inc_Stack_InPlace()
    {
        var state = CreateState();
        state.WriteVariable(0, 10); // push 10

        VariableMemoryOps.Inc(state, 0);

        ushort result = state.ReadVariable(0); // pop
        Assert.Equal((ushort)11, result);
    }

    [Fact]
    public void Dec_Local()
    {
        var state = CreateState();
        state.WriteVariable(1, 5);

        VariableMemoryOps.Dec(state, 1);
        Assert.Equal((ushort)4, state.ReadVariable(1));
    }

    [Fact]
    public void Dec_Underflow()
    {
        var state = CreateState();
        state.WriteVariable(1, 0x8000); // -32768

        VariableMemoryOps.Dec(state, 1);
        Assert.Equal(32767, (short)state.ReadVariable(1));
    }

    [Fact]
    public void Dec_Stack_InPlace()
    {
        var state = CreateState();
        state.WriteVariable(0, 10); // push 10

        VariableMemoryOps.Dec(state, 0);

        ushort result = state.ReadVariable(0); // pop
        Assert.Equal((ushort)9, result);
    }

    #endregion

    #region @inc_chk / @dec_chk

    [Fact]
    public void IncChk_IncrementsAndBranches()
    {
        var state = CreateState();
        state.WriteVariable(1, 9);

        bool result = VariableMemoryOps.IncChk(state, 1, 9);

        Assert.Equal((ushort)10, state.ReadVariable(1));
        Assert.True(result); // 10 > 9
    }

    [Fact]
    public void IncChk_IncrementsAndDoesNotBranch()
    {
        var state = CreateState();
        state.WriteVariable(1, 8);

        bool result = VariableMemoryOps.IncChk(state, 1, 9);

        Assert.Equal((ushort)9, state.ReadVariable(1));
        Assert.False(result); // 9 == 9, not >
    }

    [Fact]
    public void IncChk_SignedComparison()
    {
        var state = CreateState();
        state.WriteVariable(1, unchecked((ushort)(short)-2));

        bool result = VariableMemoryOps.IncChk(state, 1, -1);

        Assert.Equal(-1, (short)state.ReadVariable(1));
        Assert.False(result); // -1 == -1, not >
    }

    [Fact]
    public void DecChk_DecrementsAndBranches()
    {
        var state = CreateState();
        state.WriteVariable(1, 5);

        bool result = VariableMemoryOps.DecChk(state, 1, 5);

        Assert.Equal((ushort)4, state.ReadVariable(1));
        Assert.True(result); // 4 < 5
    }

    [Fact]
    public void DecChk_DecrementsAndDoesNotBranch()
    {
        var state = CreateState();
        state.WriteVariable(1, 5);

        bool result = VariableMemoryOps.DecChk(state, 1, 4);

        Assert.Equal((ushort)4, state.ReadVariable(1));
        Assert.False(result); // 4 == 4, not <
    }

    [Fact]
    public void DecChk_SignedComparison()
    {
        var state = CreateState();
        state.WriteVariable(1, 0); // 0

        bool result = VariableMemoryOps.DecChk(state, 1, 0);

        Assert.Equal(-1, (short)state.ReadVariable(1));
        Assert.True(result); // -1 < 0
    }

    #endregion

    #region @push / @pull

    [Fact]
    public void Push_AddsToStack()
    {
        var state = CreateState();

        VariableMemoryOps.Push(state, 42);
        VariableMemoryOps.Push(state, 99);

        Assert.Equal((ushort)99, state.ReadVariable(0)); // pop
        Assert.Equal((ushort)42, state.ReadVariable(0)); // pop
    }

    [Fact]
    public void Pull_PopsToLocal()
    {
        var state = CreateState();
        state.WriteVariable(0, 77); // push 77

        VariableMemoryOps.Pull(state, 2);

        Assert.Equal((ushort)77, state.ReadVariable(2));
    }

    [Fact]
    public void Pull_ToStack_ReplacesTop()
    {
        var state = CreateState();
        state.WriteVariable(0, 10); // push 10
        state.WriteVariable(0, 20); // push 20

        // Pull pops 20, then indirect-writes to variable 0
        // which replaces the new top (10).
        VariableMemoryOps.Pull(state, 0);

        ushort result = state.ReadVariable(0); // pop
        Assert.Equal((ushort)20, result);
    }

    #endregion

    #region @loadw / @loadb / @storew / @storeb

    [Fact]
    public void LoadWord_ReadsFromArray()
    {
        var memory = CreateMemory();
        memory.WriteWord(0x0100, 0xABCD);

        ushort result = VariableMemoryOps.LoadWord(memory, 0x0100, 0);
        Assert.Equal((ushort)0xABCD, result);
    }

    [Fact]
    public void LoadWord_WithIndex()
    {
        var memory = CreateMemory();
        memory.WriteWord(0x0104, 0x1234);

        ushort result = VariableMemoryOps.LoadWord(memory, 0x0100, 2);
        Assert.Equal((ushort)0x1234, result);
    }

    [Fact]
    public void LoadByte_ReadsFromArray()
    {
        var memory = CreateMemory();
        memory.WriteByte(0x0100, 0xAB);

        ushort result = VariableMemoryOps.LoadByte(memory, 0x0100, 0);
        Assert.Equal((ushort)0xAB, result);
    }

    [Fact]
    public void LoadByte_WithIndex()
    {
        var memory = CreateMemory();
        memory.WriteByte(0x0103, 0xCD);

        ushort result = VariableMemoryOps.LoadByte(memory, 0x0100, 3);
        Assert.Equal((ushort)0xCD, result);
    }

    [Fact]
    public void StoreWord_WritesToArray()
    {
        var memory = CreateMemory();
        VariableMemoryOps.StoreWord(memory, 0x0100, 0, 0xBEEF);

        Assert.Equal((ushort)0xBEEF, memory.ReadWord(0x0100));
    }

    [Fact]
    public void StoreWord_WithIndex()
    {
        var memory = CreateMemory();
        VariableMemoryOps.StoreWord(memory, 0x0100, 3, 0xDEAD);

        Assert.Equal((ushort)0xDEAD, memory.ReadWord(0x0106));
    }

    [Fact]
    public void StoreByte_WritesToArray()
    {
        var memory = CreateMemory();
        VariableMemoryOps.StoreByte(memory, 0x0100, 0, 0xFF);

        Assert.Equal(0xFF, memory.ReadByte(0x0100));
    }

    [Fact]
    public void StoreByte_WithIndex()
    {
        var memory = CreateMemory();
        VariableMemoryOps.StoreByte(memory, 0x0100, 5, 0x42);

        Assert.Equal(0x42, memory.ReadByte(0x0105));
    }

    #endregion

    #region @scan_table

    [Fact]
    public void ScanTable_Word_Found()
    {
        var memory = CreateMemory();
        // Table of 3 word entries at 0x0100, each 2 bytes.
        memory.WriteWord(0x0100, 10);
        memory.WriteWord(0x0102, 20);
        memory.WriteWord(0x0104, 30);

        // form: 0x82 = word mode (bit 7) + entry length 2
        var (addr, found) = VariableMemoryOps.ScanTable(memory, 20, 0x0100, 3, 0x82);

        Assert.True(found);
        Assert.Equal((ushort)0x0102, addr);
    }

    [Fact]
    public void ScanTable_Word_NotFound()
    {
        var memory = CreateMemory();
        memory.WriteWord(0x0100, 10);
        memory.WriteWord(0x0102, 20);

        var (addr, found) = VariableMemoryOps.ScanTable(memory, 99, 0x0100, 2, 0x82);

        Assert.False(found);
        Assert.Equal((ushort)0, addr);
    }

    [Fact]
    public void ScanTable_Byte_Found()
    {
        var memory = CreateMemory();
        // Table of 4 byte entries at 0x0100, each 1 byte.
        memory.WriteByte(0x0100, 5);
        memory.WriteByte(0x0101, 10);
        memory.WriteByte(0x0102, 15);
        memory.WriteByte(0x0103, 20);

        // form: 0x01 = byte mode (bit 7 clear) + entry length 1
        var (addr, found) = VariableMemoryOps.ScanTable(memory, 15, 0x0100, 4, 0x01);

        Assert.True(found);
        Assert.Equal((ushort)0x0102, addr);
    }

    [Fact]
    public void ScanTable_Byte_NotFound()
    {
        var memory = CreateMemory();
        memory.WriteByte(0x0100, 5);
        memory.WriteByte(0x0101, 10);

        var (addr, found) = VariableMemoryOps.ScanTable(memory, 99, 0x0100, 2, 0x01);

        Assert.False(found);
        Assert.Equal((ushort)0, addr);
    }

    [Fact]
    public void ScanTable_Word_WithLargerEntries()
    {
        var memory = CreateMemory();
        // 4-byte entries: word key + 2 bytes data.
        memory.WriteWord(0x0100, 100);
        memory.WriteWord(0x0104, 200);
        memory.WriteWord(0x0108, 300);

        // form: 0x84 = word mode + entry length 4
        var (addr, found) = VariableMemoryOps.ScanTable(memory, 200, 0x0100, 3, 0x84);

        Assert.True(found);
        Assert.Equal((ushort)0x0104, addr);
    }

    [Fact]
    public void ScanTable_FirstEntry()
    {
        var memory = CreateMemory();
        memory.WriteWord(0x0100, 42);

        var (addr, found) = VariableMemoryOps.ScanTable(memory, 42, 0x0100, 1, 0x82);

        Assert.True(found);
        Assert.Equal((ushort)0x0100, addr);
    }

    #endregion

    #region @copy_table

    [Fact]
    public void CopyTable_BasicCopy()
    {
        var memory = CreateMemory();
        memory.WriteByte(0x0100, 0xAA);
        memory.WriteByte(0x0101, 0xBB);
        memory.WriteByte(0x0102, 0xCC);

        VariableMemoryOps.CopyTable(memory, 0x0100, 0x0200, 3);

        Assert.Equal(0xAA, memory.ReadByte(0x0200));
        Assert.Equal(0xBB, memory.ReadByte(0x0201));
        Assert.Equal(0xCC, memory.ReadByte(0x0202));
    }

    [Fact]
    public void CopyTable_ZeroTable()
    {
        var memory = CreateMemory();
        memory.WriteByte(0x0100, 0xFF);
        memory.WriteByte(0x0101, 0xFF);
        memory.WriteByte(0x0102, 0xFF);

        VariableMemoryOps.CopyTable(memory, 0x0100, 0, 3);

        Assert.Equal(0, memory.ReadByte(0x0100));
        Assert.Equal(0, memory.ReadByte(0x0101));
        Assert.Equal(0, memory.ReadByte(0x0102));
    }

    [Fact]
    public void CopyTable_OverlapForward_PositiveSize()
    {
        var memory = CreateMemory();
        // [0x0100]=1, [0x0101]=2, [0x0102]=3, [0x0103]=4
        memory.WriteByte(0x0100, 1);
        memory.WriteByte(0x0101, 2);
        memory.WriteByte(0x0102, 3);
        memory.WriteByte(0x0103, 4);

        // Copy 0x0100→0x0102 (3 bytes), overlapping.
        // Positive size: implementation copies backward to avoid corruption.
        VariableMemoryOps.CopyTable(memory, 0x0100, 0x0102, 3);

        Assert.Equal(1, memory.ReadByte(0x0102));
        Assert.Equal(2, memory.ReadByte(0x0103));
        Assert.Equal(3, memory.ReadByte(0x0104));
    }

    [Fact]
    public void CopyTable_NegativeSize_ForceForward()
    {
        var memory = CreateMemory();
        // [0x0100]=A, [0x0101]=B, [0x0102]=0, [0x0103]=0
        memory.WriteByte(0x0100, 0x0A);
        memory.WriteByte(0x0101, 0x0B);

        // Negative size = copy forward (allows fill pattern).
        // Copy 2 bytes from 0x0100 to 0x0101 forward:
        // step 0: [0x0101] = [0x0100] = 0x0A
        // step 1: [0x0102] = [0x0101] = 0x0A (the value just written)
        VariableMemoryOps.CopyTable(memory, 0x0100, 0x0101, -2);

        Assert.Equal(0x0A, memory.ReadByte(0x0101));
        Assert.Equal(0x0A, memory.ReadByte(0x0102));
    }

    [Fact]
    public void CopyTable_NoOverlap()
    {
        var memory = CreateMemory();
        memory.WriteWord(0x0100, 0x1234);
        memory.WriteWord(0x0102, 0x5678);

        VariableMemoryOps.CopyTable(memory, 0x0100, 0x0200, 4);

        Assert.Equal((ushort)0x1234, memory.ReadWord(0x0200));
        Assert.Equal((ushort)0x5678, memory.ReadWord(0x0202));
    }

    #endregion

    #region Helpers

    private static MachineState CreateState()
    {
        var memory = CreateMemory();
        int globalsAddr = 0x0100;
        var state = new MachineState(memory, globalsAddr);

        // Push an initial frame with 15 locals.
        var frame = new CallFrame(0, 0, false, 15, 0);
        state.CallStack.PushFrame(frame);

        return state;
    }

    private static Memory CreateMemory()
    {
        int dataSize = 0x0400;
        byte[] data = new byte[dataSize];
        data[0] = 5; // version
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);

        // Globals at 0x0100.
        data[0x0C] = 0x01;
        data[0x0D] = 0x00;

        var memory = new Memory();
        memory.LoadStory(data);
        return memory;
    }

    #endregion
}
