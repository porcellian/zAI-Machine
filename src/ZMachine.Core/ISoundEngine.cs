namespace ZMachine.Core;

/// <summary>
/// Abstraction for sound resource playback, decoupling the interpreter
/// engine from audio libraries. The IO layer implements this to load
/// and play sounds from Blorb resources.
/// </summary>
/// <remarks>
/// ZSpec S9 — sound effects. ZSpec11 "@sound_effect" — dual-channel
/// model, V5 repeats, callback semantics.
/// Blorb "Sound Resource Chunks" — AIFF, Ogg, MOD/SONG formats.
/// </remarks>
public interface ISoundEngine
{
    /// <summary>
    /// Returns true if any sound resources are available.
    /// Used to set header sound capability flags.
    /// </summary>
    bool HasSounds { get; }

    /// <summary>Total number of sound resources.</summary>
    int SoundCount { get; }

    /// <summary>
    /// Prepares a sound for playback (action 1).
    /// Implementation may pre-decode/buffer the audio data.
    /// </summary>
    /// <param name="number">Sound resource number.</param>
    void PrepareSound(int number);

    /// <summary>
    /// Plays a sound (action 2).
    /// ZSpec11 "@sound_effect" — effects interrupt effects,
    /// music interrupts music, they do not cross-interrupt.
    /// </summary>
    /// <param name="number">Sound resource number.</param>
    /// <param name="volume">Volume 1–8, or 255 for loudest.</param>
    /// <param name="repeats">
    /// V5+: total number of plays (not extra repeats). 0 treated as 1.
    /// 255 (0xFF) = loop forever.
    /// V3: controlled by Blorb 'Loop' chunk, not this parameter.
    /// </param>
    /// <param name="callback">
    /// Routine address to call when playback finishes naturally.
    /// 0 means no callback. Not called on manual stop or interruption.
    /// </param>
    void PlaySound(int number, int volume, int repeats, ushort callback);

    /// <summary>
    /// Stops a specific sound (action 3).
    /// ZSpec11 — only stops if the sound is currently playing.
    /// Callback is NOT invoked on manual stop.
    /// </summary>
    /// <param name="number">
    /// Sound resource number. 0 = stop all sounds on both channels.
    /// </param>
    void StopSound(int number);

    /// <summary>
    /// Stops and unloads a specific sound (action 4).
    /// ZSpec11 — stops if playing, then releases cached data.
    /// </summary>
    /// <param name="number">
    /// Sound resource number. 0 = stop and unload all sounds.
    /// </param>
    void UnloadSound(int number);

    /// <summary>
    /// Stops all currently playing sounds on both channels.
    /// </summary>
    void StopAll();

    /// <summary>
    /// Returns the sound channel a resource belongs to.
    /// Blorb "Z-Machine Compatibility Issues" — AIFF is effect,
    /// MOD/SONG/Ogg is music.
    /// </summary>
    SoundChannel GetChannel(int number);
}

/// <summary>
/// Classifies a sound resource into the dual-channel model.
/// </summary>
/// <remarks>
/// Blorb "Z-Machine Compatibility Issues" — effects and music
/// are separate channels that do not cross-interrupt.
/// </remarks>
public enum SoundChannel
{
    /// <summary>Sound resource not found or unknown format.</summary>
    None,

    /// <summary>
    /// Sampled sound (AIFF). Plays on the effects channel.
    /// </summary>
    Effect,

    /// <summary>
    /// Music (MOD, SONG, Ogg Vorbis). Plays on the music channel.
    /// </summary>
    Music,
}
