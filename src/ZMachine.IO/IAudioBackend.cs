namespace ZMachine.IO;

/// <summary>
/// Low-level audio output abstraction. Concrete implementations
/// wrap platform-specific libraries (NAudio, SDL2, OpenAL).
/// SoundManager handles channel logic; this just plays bytes.
/// </summary>
public interface IAudioBackend
{
    /// <summary>
    /// Loads/caches raw audio data for later playback.
    /// </summary>
    /// <param name="number">Sound resource number.</param>
    /// <param name="data">Raw audio bytes (AIFF, Ogg, MOD, etc.).</param>
    /// <param name="format">Format identifier: "AIFF", "OGGV", "MOD ", "SONG".</param>
    void LoadSound(int number, byte[] data, string format);

    /// <summary>
    /// Plays a previously loaded sound.
    /// </summary>
    /// <param name="number">Sound resource number.</param>
    /// <param name="volume">Volume 1–8.</param>
    /// <param name="repeats">Total play count. 255 (0xFF) = loop forever.</param>
    void Play(int number, int volume, int repeats);

    /// <summary>Stops a currently playing sound.</summary>
    /// <param name="number">Sound resource number.</param>
    void Stop(int number);

    /// <summary>Releases cached data for a sound.</summary>
    /// <param name="number">Sound resource number.</param>
    void Unload(int number);

    /// <summary>Releases all cached sound data.</summary>
    void UnloadAll();

    /// <summary>
    /// Raised when a sound finishes playing naturally (all repeats done).
    /// Not raised when stopped manually via <see cref="Stop"/>.
    /// The int parameter is the sound resource number.
    /// </summary>
    event Action<int>? OnPlaybackFinished;
}
