namespace ZMachine.IO;

/// <summary>
/// Abstraction for Z-Machine sound playback, supporting the dual-channel
/// model required by Blorb: an effects channel and a music channel that
/// operate independently.
/// </summary>
/// <remarks>
/// ZSpec S9, Blorb "Z-Machine Compatibility Issues" — Effects interrupt
/// other effects; music interrupts other music; the two channels do NOT
/// cross-interrupt.
/// </remarks>
public interface ISoundEngine
{
    /// <summary>
    /// Loads a sound resource into memory for playback.
    /// </summary>
    /// <param name="number">The sound resource number (from Blorb index).</param>
    /// <param name="data">Raw sound data bytes.</param>
    /// <param name="format">Format identifier: "AIFF", "OGGV", "MOD", etc.</param>
    void LoadSound(int number, byte[] data, string format);

    /// <summary>
    /// Plays a previously loaded sound.
    /// </summary>
    /// <param name="number">Sound resource number.</param>
    /// <param name="volume">Volume level 1–8; 255 means loudest.</param>
    /// <param name="repeats">
    /// Number of times to play. In V5+, this is total plays (not extra repeats);
    /// 0 means play once with a warning; 255 means loop forever.
    /// </param>
    /// <param name="callback">
    /// Routine address to call when playback finishes naturally (not on manual stop).
    /// 0 means no callback.
    /// </param>
    void PlaySound(int number, int volume, int repeats, ushort callback);

    /// <summary>Stops a specific sound if it is currently playing.</summary>
    void StopSound(int number);

    /// <summary>Stops all currently playing sounds on both channels.</summary>
    void StopAll();
}
