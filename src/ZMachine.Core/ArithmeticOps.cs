namespace ZMachine.Core;

/// <summary>
/// Implements Z-Machine arithmetic, bitwise, shift, comparison, and
/// random opcodes. All operations use 16-bit unsigned storage with
/// signed interpretation where the spec requires it.
/// </summary>
/// <remarks>
/// ZSpec S15 — All arithmetic is 16-bit; values are unsigned words in
/// memory but instructions interpret them as signed where specified.
/// </remarks>
public class ArithmeticOps
{
    private Random _random = new();

    /// <summary>
    /// @add: signed 16-bit addition. Overflow wraps per two's complement.
    /// </summary>
    /// <remarks>ZSpec S15 — @add: "Signed 16-bit addition."</remarks>
    public static ushort Add(ushort a, ushort b)
    {
        return (ushort)((short)a + (short)b);
    }

    /// <summary>
    /// @sub: signed 16-bit subtraction. Overflow wraps per two's complement.
    /// </summary>
    public static ushort Sub(ushort a, ushort b)
    {
        return (ushort)((short)a - (short)b);
    }

    /// <summary>
    /// @mul: signed 16-bit multiplication. Overflow wraps.
    /// </summary>
    public static ushort Mul(ushort a, ushort b)
    {
        return (ushort)((short)a * (short)b);
    }

    /// <summary>
    /// @div: signed 16-bit division. Rounds toward zero (C# default for
    /// integer division). Division by zero is a fatal error.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @div: "Signed 16-bit division. Division by zero should
    /// halt the interpreter with a suitable error message."
    /// </remarks>
    public static ushort Div(ushort a, ushort b)
    {
        if (b == 0)
            throw new DivideByZeroException("@div: division by zero.");
        return (ushort)((short)a / (short)b);
    }

    /// <summary>
    /// @mod: signed 16-bit remainder. Sign follows the dividend (C# default).
    /// Division by zero is a fatal error.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @mod: "Remainder after signed 16-bit division."
    /// ZSpec11 "@div and @mod" — "The result of a division always rounds
    /// towards zero."
    /// </remarks>
    public static ushort Mod(ushort a, ushort b)
    {
        if (b == 0)
            throw new DivideByZeroException("@mod: division by zero.");
        return (ushort)((short)a % (short)b);
    }

    /// <summary>
    /// @and: bitwise AND.
    /// </summary>
    public static ushort And(ushort a, ushort b)
    {
        return (ushort)(a & b);
    }

    /// <summary>
    /// @or: bitwise OR.
    /// </summary>
    public static ushort Or(ushort a, ushort b)
    {
        return (ushort)(a | b);
    }

    /// <summary>
    /// @not: bitwise NOT (complement). In V1-4 this is a 1OP instruction;
    /// in V5+ it moves to the VAR table but the operation is the same.
    /// </summary>
    public static ushort Not(ushort a)
    {
        return (ushort)~a;
    }

    /// <summary>
    /// @log_shift: logical shift. Positive places = shift left, negative
    /// = shift right (zero-fill). Places must be -15..+15.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@art_shift and @log_shift" — "Arithmetic and logical
    /// shifts... The number of places must be in the range -15 to +15."
    /// Logical shift right fills with zeros.
    /// </remarks>
    public static ushort LogShift(ushort value, short places)
    {
        if (places < -15 || places > 15)
            throw new ArgumentOutOfRangeException(nameof(places),
                $"@log_shift: places ({places}) must be in range -15 to +15.");

        if (places > 0)
            return (ushort)(value << places);
        if (places < 0)
            return (ushort)(value >> -places);
        return value;
    }

    /// <summary>
    /// @art_shift: arithmetic shift. Positive places = shift left, negative
    /// = shift right (sign-extending). Places must be -15..+15.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@art_shift and @log_shift" — Arithmetic shift right
    /// preserves the sign bit (fills with copies of bit 15).
    /// </remarks>
    public static ushort ArtShift(ushort value, short places)
    {
        if (places < -15 || places > 15)
            throw new ArgumentOutOfRangeException(nameof(places),
                $"@art_shift: places ({places}) must be in range -15 to +15.");

        if (places > 0)
            return (ushort)((short)value << places);
        if (places < 0)
            return (ushort)((short)value >> -places);
        return value;
    }

    /// <summary>
    /// @je: branch if the first operand equals any of the others.
    /// Supports 2-4 operands.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@je" — "je with just 1 operand is not permitted."
    /// je a b → branch if a == b.
    /// je a b c → branch if a == b || a == c.
    /// je a b c d → branch if a == b || a == c || a == d.
    /// </remarks>
    public static bool JumpEqual(ushort[] operands, int count)
    {
        if (count < 2)
            throw new InvalidOperationException("@je: requires at least 2 operands.");

        ushort first = operands[0];
        for (int i = 1; i < count; i++)
        {
            if (first == operands[i])
                return true;
        }
        return false;
    }

    /// <summary>
    /// @jl: branch if a (signed) is less than b (signed).
    /// </summary>
    public static bool JumpLessThan(ushort a, ushort b)
    {
        return (short)a < (short)b;
    }

    /// <summary>
    /// @jg: branch if a (signed) is greater than b (signed).
    /// </summary>
    public static bool JumpGreaterThan(ushort a, ushort b)
    {
        return (short)a > (short)b;
    }

    /// <summary>
    /// @jz: branch if value is zero.
    /// </summary>
    public static bool JumpZero(ushort value)
    {
        return value == 0;
    }

    /// <summary>
    /// @test: branch if all bits in flags are set in bitmap.
    /// Equivalent to: (bitmap &amp; flags) == flags.
    /// </summary>
    /// <remarks>ZSpec S15 — @test: "Jump if all of the flags in bitmap are set."</remarks>
    public static bool Test(ushort bitmap, ushort flags)
    {
        return (bitmap & flags) == flags;
    }

    /// <summary>
    /// @random: if range > 0, returns a random number in 1..range.
    /// If range &lt; 0, seeds the RNG with |range| and returns 0.
    /// If range == 0, re-randomizes (seeds from system entropy) and returns 0.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @random: "If range is positive, returns a uniformly
    /// distributed random number between 1 and range. If range is
    /// negative, the random number generator is seeded to that value
    /// and the return value is 0. If range is zero, the generator is
    /// seeded randomly."
    /// </remarks>
    public ushort Random(ushort range)
    {
        short signed = (short)range;

        if (signed > 0)
            return (ushort)(_random.Next(signed) + 1);

        if (signed < 0)
        {
            _random = new Random(-signed);
            return 0;
        }

        // range == 0: re-randomize
        _random = new Random();
        return 0;
    }
}
