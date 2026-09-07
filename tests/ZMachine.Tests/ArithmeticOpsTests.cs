namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for Z-Machine arithmetic, bitwise, shift, comparison, and random
/// opcodes. Covers signed overflow, division by zero, shift boundaries,
/// and multi-operand @je.
/// </summary>
public class ArithmeticOpsTests
{
    #region @add

    [Fact]
    public void Add_PositiveValues()
    {
        Assert.Equal((ushort)30, ArithmeticOps.Add(10, 20));
    }

    [Fact]
    public void Add_NegativeValues()
    {
        // -5 + -3 = -8 → 0xFFF8
        ushort a = unchecked((ushort)(short)-5);
        ushort b = unchecked((ushort)(short)-3);
        ushort result = ArithmeticOps.Add(a, b);
        Assert.Equal(-8, (short)result);
    }

    [Fact]
    public void Add_Overflow_Wraps()
    {
        // 32767 + 1 = -32768 (signed overflow wraps)
        ushort result = ArithmeticOps.Add(0x7FFF, 1);
        Assert.Equal(-32768, (short)result);
    }

    [Fact]
    public void Add_MixedSignOverflow()
    {
        // -1 + -1 = -2
        ushort result = ArithmeticOps.Add(0xFFFF, 0xFFFF);
        Assert.Equal(-2, (short)result);
    }

    #endregion

    #region @sub

    [Fact]
    public void Sub_PositiveValues()
    {
        Assert.Equal((ushort)10, ArithmeticOps.Sub(30, 20));
    }

    [Fact]
    public void Sub_NegativeResult()
    {
        ushort result = ArithmeticOps.Sub(5, 10);
        Assert.Equal(-5, (short)result);
    }

    [Fact]
    public void Sub_Underflow_Wraps()
    {
        // -32768 - 1 = 32767 (underflow wraps)
        ushort result = ArithmeticOps.Sub(0x8000, 1);
        Assert.Equal(32767, (short)result);
    }

    #endregion

    #region @mul

    [Fact]
    public void Mul_PositiveValues()
    {
        Assert.Equal((ushort)200, ArithmeticOps.Mul(10, 20));
    }

    [Fact]
    public void Mul_NegativeTimesPositive()
    {
        ushort a = unchecked((ushort)(short)-5);
        ushort result = ArithmeticOps.Mul(a, 3);
        Assert.Equal(-15, (short)result);
    }

    [Fact]
    public void Mul_Overflow_Wraps()
    {
        // 1000 * 1000 = 1000000 → truncated to 16 bits = 0x4240 = 16960
        ushort result = ArithmeticOps.Mul(1000, 1000);
        Assert.Equal((ushort)(1000000 & 0xFFFF), result);
    }

    [Fact]
    public void Mul_ByZero()
    {
        Assert.Equal((ushort)0, ArithmeticOps.Mul(12345, 0));
    }

    #endregion

    #region @div

    [Fact]
    public void Div_PositiveValues()
    {
        Assert.Equal((ushort)5, ArithmeticOps.Div(10, 2));
    }

    [Fact]
    public void Div_RoundsTowardZero_Positive()
    {
        // 7 / 2 = 3 (rounds toward zero)
        Assert.Equal((ushort)3, ArithmeticOps.Div(7, 2));
    }

    [Fact]
    public void Div_RoundsTowardZero_Negative()
    {
        // -7 / 2 = -3 (rounds toward zero, not -4)
        ushort a = unchecked((ushort)(short)-7);
        ushort result = ArithmeticOps.Div(a, 2);
        Assert.Equal(-3, (short)result);
    }

    [Fact]
    public void Div_NegativeByNegative()
    {
        ushort a = unchecked((ushort)(short)-10);
        ushort b = unchecked((ushort)(short)-3);
        ushort result = ArithmeticOps.Div(a, b);
        Assert.Equal(3, (short)result);
    }

    [Fact]
    public void Div_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => ArithmeticOps.Div(10, 0));
    }

    #endregion

    #region @mod

    [Fact]
    public void Mod_PositiveValues()
    {
        Assert.Equal((ushort)1, ArithmeticOps.Mod(7, 2));
    }

    [Fact]
    public void Mod_NegativeDividend()
    {
        // -7 % 2 = -1 (sign follows dividend)
        ushort a = unchecked((ushort)(short)-7);
        ushort result = ArithmeticOps.Mod(a, 2);
        Assert.Equal(-1, (short)result);
    }

    [Fact]
    public void Mod_NegativeDivisor()
    {
        // 7 % -2 = 1 (sign follows dividend)
        ushort b = unchecked((ushort)(short)-2);
        ushort result = ArithmeticOps.Mod(7, b);
        Assert.Equal(1, (short)result);
    }

    [Fact]
    public void Mod_NoRemainder()
    {
        Assert.Equal((ushort)0, ArithmeticOps.Mod(10, 5));
    }

    [Fact]
    public void Mod_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => ArithmeticOps.Mod(10, 0));
    }

    #endregion

    #region @and, @or, @not

    [Fact]
    public void And_MasksBits()
    {
        Assert.Equal((ushort)0x0012, ArithmeticOps.And(0xFF12, 0x00FF));
    }

    [Fact]
    public void Or_SetsBits()
    {
        Assert.Equal((ushort)0xFF00, ArithmeticOps.Or(0xF000, 0x0F00));
    }

    [Fact]
    public void Not_InvertsBits()
    {
        Assert.Equal((ushort)0x0000, ArithmeticOps.Not(0xFFFF));
        Assert.Equal((ushort)0xFFFF, ArithmeticOps.Not(0x0000));
        Assert.Equal((ushort)0xFF00, ArithmeticOps.Not(0x00FF));
    }

    #endregion

    #region @log_shift

    [Fact]
    public void LogShift_Left()
    {
        Assert.Equal((ushort)0x0008, ArithmeticOps.LogShift(1, 3));
    }

    [Fact]
    public void LogShift_Right_ZeroFill()
    {
        // 0xFF00 >> 4 = 0x0FF0 (zero fill, not sign-extend)
        Assert.Equal((ushort)0x0FF0, ArithmeticOps.LogShift(0xFF00, -4));
    }

    [Fact]
    public void LogShift_Right_HighBitSet_ZeroFill()
    {
        // 0x8000 >> 1 = 0x4000 (logical: zero fill)
        Assert.Equal((ushort)0x4000, ArithmeticOps.LogShift(0x8000, -1));
    }

    [Fact]
    public void LogShift_ZeroPlaces()
    {
        Assert.Equal((ushort)0xABCD, ArithmeticOps.LogShift(0xABCD, 0));
    }

    [Fact]
    public void LogShift_MaxLeft()
    {
        Assert.Equal((ushort)0x8000, ArithmeticOps.LogShift(1, 15));
    }

    [Fact]
    public void LogShift_MaxRight()
    {
        Assert.Equal((ushort)0x0001, ArithmeticOps.LogShift(0x8000, -15));
    }

    [Fact]
    public void LogShift_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArithmeticOps.LogShift(1, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArithmeticOps.LogShift(1, -16));
    }

    #endregion

    #region @art_shift

    [Fact]
    public void ArtShift_Left()
    {
        Assert.Equal((ushort)0x0008, ArithmeticOps.ArtShift(1, 3));
    }

    [Fact]
    public void ArtShift_Right_SignExtends()
    {
        // 0xFF00 as signed = -256. >> 4 arithmetic = -16 = 0xFFF0
        Assert.Equal((ushort)0xFFF0, ArithmeticOps.ArtShift(0xFF00, -4));
    }

    [Fact]
    public void ArtShift_Right_Positive_ZeroFill()
    {
        // 0x0FF0 >> 4 = 0x00FF (positive, so MSBs fill with 0)
        Assert.Equal((ushort)0x00FF, ArithmeticOps.ArtShift(0x0FF0, -4));
    }

    [Fact]
    public void ArtShift_Right_HighBitSet_SignExtends()
    {
        // 0x8000 >> 1 = 0xC000 (arithmetic: sign-extend)
        Assert.Equal((ushort)0xC000, ArithmeticOps.ArtShift(0x8000, -1));
    }

    [Fact]
    public void ArtShift_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArithmeticOps.ArtShift(1, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArithmeticOps.ArtShift(1, -16));
    }

    #endregion

    #region @je

    [Fact]
    public void JumpEqual_TwoOperands_Equal()
    {
        Assert.True(ArithmeticOps.JumpEqual([5, 5], 2));
    }

    [Fact]
    public void JumpEqual_TwoOperands_NotEqual()
    {
        Assert.False(ArithmeticOps.JumpEqual([5, 6], 2));
    }

    [Fact]
    public void JumpEqual_ThreeOperands_MatchesSecond()
    {
        Assert.True(ArithmeticOps.JumpEqual([5, 3, 5], 3));
    }

    [Fact]
    public void JumpEqual_ThreeOperands_MatchesFirst()
    {
        Assert.True(ArithmeticOps.JumpEqual([5, 5, 9], 3));
    }

    [Fact]
    public void JumpEqual_ThreeOperands_NoMatch()
    {
        Assert.False(ArithmeticOps.JumpEqual([5, 3, 7], 3));
    }

    [Fact]
    public void JumpEqual_FourOperands_MatchesThird()
    {
        Assert.True(ArithmeticOps.JumpEqual([5, 1, 2, 5], 4));
    }

    [Fact]
    public void JumpEqual_FourOperands_NoMatch()
    {
        Assert.False(ArithmeticOps.JumpEqual([5, 1, 2, 3], 4));
    }

    [Fact]
    public void JumpEqual_OneOperand_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => ArithmeticOps.JumpEqual([5], 1));
    }

    #endregion

    #region @jl, @jg, @jz

    [Fact]
    public void JumpLessThan_True()
    {
        Assert.True(ArithmeticOps.JumpLessThan(5, 10));
    }

    [Fact]
    public void JumpLessThan_False()
    {
        Assert.False(ArithmeticOps.JumpLessThan(10, 5));
    }

    [Fact]
    public void JumpLessThan_Equal_False()
    {
        Assert.False(ArithmeticOps.JumpLessThan(5, 5));
    }

    [Fact]
    public void JumpLessThan_Signed()
    {
        // -1 (0xFFFF) < 0 — signed comparison
        Assert.True(ArithmeticOps.JumpLessThan(0xFFFF, 0));
    }

    [Fact]
    public void JumpGreaterThan_True()
    {
        Assert.True(ArithmeticOps.JumpGreaterThan(10, 5));
    }

    [Fact]
    public void JumpGreaterThan_False()
    {
        Assert.False(ArithmeticOps.JumpGreaterThan(5, 10));
    }

    [Fact]
    public void JumpGreaterThan_Signed()
    {
        // 0 > -1 (0xFFFF) — signed comparison
        Assert.True(ArithmeticOps.JumpGreaterThan(0, 0xFFFF));
    }

    [Fact]
    public void JumpZero_True()
    {
        Assert.True(ArithmeticOps.JumpZero(0));
    }

    [Fact]
    public void JumpZero_False()
    {
        Assert.False(ArithmeticOps.JumpZero(1));
    }

    [Fact]
    public void JumpZero_HighValue_False()
    {
        Assert.False(ArithmeticOps.JumpZero(0xFFFF));
    }

    #endregion

    #region @test

    [Fact]
    public void Test_AllBitsSet()
    {
        Assert.True(ArithmeticOps.Test(0xFF, 0x0F));
    }

    [Fact]
    public void Test_NotAllBitsSet()
    {
        Assert.False(ArithmeticOps.Test(0x0F, 0xFF));
    }

    [Fact]
    public void Test_ExactMatch()
    {
        Assert.True(ArithmeticOps.Test(0xABCD, 0xABCD));
    }

    [Fact]
    public void Test_ZeroFlags()
    {
        // All zero bits are trivially set in anything.
        Assert.True(ArithmeticOps.Test(0x1234, 0x0000));
    }

    #endregion

    #region @random

    [Fact]
    public void Random_PositiveRange_ReturnsInRange()
    {
        var ops = new ArithmeticOps();
        for (int i = 0; i < 100; i++)
        {
            ushort result = ops.Random(6);
            Assert.InRange(result, 1, 6);
        }
    }

    [Fact]
    public void Random_Seed_ReturnsZero()
    {
        var ops = new ArithmeticOps();
        ushort result = ops.Random(unchecked((ushort)(short)-42));
        Assert.Equal((ushort)0, result);
    }

    [Fact]
    public void Random_Seed_Deterministic()
    {
        var ops1 = new ArithmeticOps();
        var ops2 = new ArithmeticOps();

        ops1.Random(unchecked((ushort)(short)-100));
        ops2.Random(unchecked((ushort)(short)-100));

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(ops1.Random(1000), ops2.Random(1000));
        }
    }

    [Fact]
    public void Random_Zero_Rerandomizes()
    {
        var ops = new ArithmeticOps();
        ushort result = ops.Random(0);
        Assert.Equal((ushort)0, result);
    }

    [Fact]
    public void Random_Range1_AlwaysReturns1()
    {
        var ops = new ArithmeticOps();
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal((ushort)1, ops.Random(1));
        }
    }

    #endregion
}
