namespace ZMachine.Core;

/// <summary>
/// Exception raised by the Z-Machine interpreter when an illegal or
/// unrecoverable situation is detected during execution. Carries the
/// PC address and opcode context for diagnostics.
/// </summary>
/// <remarks>
/// ZSpec S15 — "Illegal operations should halt the interpreter with a
/// suitable error message."
/// </remarks>
public class ZMachineException : Exception
{
    /// <summary>Program counter at the time of the error.</summary>
    public int PC { get; }

    /// <summary>Opcode form name (e.g. "2OP", "VAR", "EXT").</summary>
    public string OpcodeForm { get; }

    /// <summary>Opcode number within its form.</summary>
    public int OpcodeNumber { get; }

    /// <summary>Whether this error is recoverable (warn and continue).</summary>
    public bool IsWarning { get; }

    public ZMachineException(string message, int pc, string opcodeForm,
        int opcodeNumber, Exception? inner = null, bool isWarning = false)
        : base(FormatMessage(message, pc, opcodeForm, opcodeNumber), inner)
    {
        PC = pc;
        OpcodeForm = opcodeForm;
        OpcodeNumber = opcodeNumber;
        IsWarning = isWarning;
    }

    private static string FormatMessage(string message, int pc,
        string opcodeForm, int opcodeNumber)
    {
        return $"[${pc:X5} {opcodeForm}:{opcodeNumber}] {message}";
    }
}
