namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for AddressHelper — packed-to-byte address conversion across
/// all version ranges, including V6/V7 offset handling and verification
/// against real story file addresses.
/// </summary>
public class AddressHelperTests
{
    #region V1–3: packed × 2

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void V1to3_Routine_PackedTimes2(int version)
    {
        Assert.Equal(0x5472, AddressHelper.UnpackRoutineAddress(0x2A39, version, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void V1to3_String_PackedTimes2(int version)
    {
        Assert.Equal(0x1000, AddressHelper.UnpackStringAddress(0x0800, version, 0));
    }

    [Fact]
    public void V3_ZeroPackedAddress()
    {
        Assert.Equal(0, AddressHelper.UnpackRoutineAddress(0, 3, 0));
    }

    [Fact]
    public void V3_MaxPackedAddress()
    {
        // 0xFFFF × 2 = 131070 (within V3's 128K limit)
        Assert.Equal(131070, AddressHelper.UnpackRoutineAddress(0xFFFF, 3, 0));
    }

    [Fact]
    public void V3_Zork1_FirstCallTarget()
    {
        // zork1.z3 first instruction calls packed $2A39 → byte $5472
        int addr = AddressHelper.UnpackRoutineAddress(0x2A39, 3, 0);
        Assert.Equal(0x5472, addr);
    }

    #endregion

    #region V4–5: packed × 4

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void V4to5_Routine_PackedTimes4(int version)
    {
        Assert.Equal(0x4000, AddressHelper.UnpackRoutineAddress(0x1000, version, 0));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void V4to5_String_PackedTimes4(int version)
    {
        Assert.Equal(0x2000, AddressHelper.UnpackStringAddress(0x0800, version, 0));
    }

    [Fact]
    public void V5_MaxPackedAddress()
    {
        // 0xFFFF × 4 = 262140 (within V5's 256K limit)
        Assert.Equal(262140, AddressHelper.UnpackRoutineAddress(0xFFFF, 5, 0));
    }

    #endregion

    #region V6–7: packed × 4 + offset × 8

    [Fact]
    public void V6_Routine_WithOffset()
    {
        // packed $1000 × 4 + routinesOffset $0100 × 8 = $4000 + $0800 = $4800
        int addr = AddressHelper.UnpackRoutineAddress(0x1000, 6, 0x0100);
        Assert.Equal(0x4800, addr);
    }

    [Fact]
    public void V6_String_WithOffset()
    {
        // packed $0800 × 4 + stringsOffset $0200 × 8 = $2000 + $1000 = $3000
        int addr = AddressHelper.UnpackStringAddress(0x0800, 6, 0x0200);
        Assert.Equal(0x3000, addr);
    }

    [Fact]
    public void V7_Routine_WithOffset()
    {
        int addr = AddressHelper.UnpackRoutineAddress(0x1000, 7, 0x0100);
        Assert.Equal(0x4800, addr);
    }

    [Fact]
    public void V6_ZeroOffset_SameAsPackedTimes4()
    {
        int addr = AddressHelper.UnpackRoutineAddress(0x1000, 6, 0);
        Assert.Equal(0x4000, addr);
    }

    [Fact]
    public void V6_Routine_And_String_UseDifferentOffsets()
    {
        // Routine and string offsets are independent
        int routineAddr = AddressHelper.UnpackRoutineAddress(0x1000, 6, 0x0100);
        int stringAddr = AddressHelper.UnpackStringAddress(0x1000, 6, 0x0200);

        Assert.Equal(0x4800, routineAddr); // $4000 + $0800
        Assert.Equal(0x5000, stringAddr);  // $4000 + $1000
        Assert.NotEqual(routineAddr, stringAddr);
    }

    #endregion

    #region V8: packed × 8

    [Fact]
    public void V8_Routine_PackedTimes8()
    {
        Assert.Equal(0x8000, AddressHelper.UnpackRoutineAddress(0x1000, 8, 0));
    }

    [Fact]
    public void V8_String_PackedTimes8()
    {
        Assert.Equal(0x4000, AddressHelper.UnpackStringAddress(0x0800, 8, 0));
    }

    [Fact]
    public void V8_MaxPackedAddress()
    {
        // 0xFFFF × 8 = 524280 (within V8's 512K limit)
        Assert.Equal(524280, AddressHelper.UnpackRoutineAddress(0xFFFF, 8, 0));
    }

    [Fact]
    public void V8_IgnoresOffset()
    {
        // V8 doesn't use routines/strings offsets
        int withOffset = AddressHelper.UnpackRoutineAddress(0x1000, 8, 0x0100);
        int withoutOffset = AddressHelper.UnpackRoutineAddress(0x1000, 8, 0);
        Assert.Equal(withoutOffset, withOffset);
    }

    #endregion

    #region Consistency

    [Fact]
    public void RoutineAndString_SameForNonV6V7()
    {
        // Outside V6–7, routine and string unpacking are identical
        foreach (int v in new[] { 1, 2, 3, 4, 5, 8 })
        {
            int routine = AddressHelper.UnpackRoutineAddress(0x1234, v, 0);
            int str = AddressHelper.UnpackStringAddress(0x1234, v, 0);
            Assert.Equal(routine, str);
        }
    }

    #endregion
}
