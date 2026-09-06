namespace ZMachine.Core;

/// <summary>
/// Converts packed addresses to byte addresses. The Z-Machine uses packed
/// addresses to fit larger address spaces into 16-bit values. The packing
/// formula differs by version and by address type (routine vs string).
/// </summary>
/// <remarks>
/// ZSpec S1.2.3 — Packed addresses.
/// V1–3: packed × 2.
/// V4–5: packed × 4.
/// V6–7: packed × 4 + offset × 8 (separate offsets for routines and strings).
/// V8:   packed × 8.
/// </remarks>
public static class AddressHelper
{
    /// <summary>
    /// Unpacks a routine address (used by call instructions and the
    /// initial PC in V6).
    /// </summary>
    /// <param name="packed">The packed address from the instruction operand.</param>
    /// <param name="version">Z-Machine version (1–8).</param>
    /// <param name="routinesOffset">
    /// Routines offset from header word $28 (V6–V7 only, 0 otherwise).
    /// </param>
    /// <returns>The byte address of the routine.</returns>
    public static int UnpackRoutineAddress(ushort packed, int version, ushort routinesOffset)
    {
        return version switch
        {
            <= 3 => packed * 2,
            <= 5 => packed * 4,
            6 or 7 => packed * 4 + routinesOffset * 8,
            _ => packed * 8,
        };
    }

    /// <summary>
    /// Unpacks a string address (used by print_paddr and abbreviation
    /// table entries).
    /// </summary>
    /// <param name="packed">The packed address from the instruction operand.</param>
    /// <param name="version">Z-Machine version (1–8).</param>
    /// <param name="stringsOffset">
    /// Strings offset from header word $2A (V6–V7 only, 0 otherwise).
    /// </param>
    /// <returns>The byte address of the encoded string.</returns>
    public static int UnpackStringAddress(ushort packed, int version, ushort stringsOffset)
    {
        return version switch
        {
            <= 3 => packed * 2,
            <= 5 => packed * 4,
            6 or 7 => packed * 4 + stringsOffset * 8,
            _ => packed * 8,
        };
    }
}
