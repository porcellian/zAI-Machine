namespace ZMachine.IO;

using ZMachine.Core;

/// <summary>
/// Manages sound resource loading and the dual-channel playback model.
/// Classifies sounds into effect (AIFF) and music (MOD/SONG/Ogg)
/// channels. Delegates actual audio output to an <see cref="IAudioBackend"/>.
/// </summary>
/// <remarks>
/// ZSpec11 "@sound_effect" — dual-channel model where effects interrupt
/// effects and music interrupts music, but they do not cross-interrupt.
/// Blorb "Sound Resource Chunks" — AIFF, OGGV, MOD, SONG formats.
/// Blorb "The Looping Chunk" — V3 loop control.
/// </remarks>
public class SoundManager : ISoundEngine, IDisposable
{
    private readonly BlorbReader? _blorb;
    private readonly IAudioBackend _backend;
    private readonly int _version;

    private int _currentEffect;
    private int _currentMusic;
    private ushort _effectCallback;
    private ushort _musicCallback;

    /// <summary>
    /// Creates a SoundManager backed by an optional Blorb resource file.
    /// </summary>
    /// <param name="blorb">Blorb reader, or null if no resources available.</param>
    /// <param name="backend">Audio backend for actual playback.</param>
    /// <param name="version">Z-Machine version (affects loop semantics).</param>
    public SoundManager(BlorbReader? blorb, IAudioBackend backend, int version)
    {
        _blorb = blorb;
        _backend = backend;
        _version = version;
        _backend.OnPlaybackFinished += HandlePlaybackFinished;
    }

    /// <summary>
    /// Event raised when a sound finishes naturally and has a callback.
    /// The interpreter should call the routine at the given address.
    /// </summary>
    public event Action<ushort>? OnCallback;

    /// <inheritdoc />
    public bool HasSounds => _blorb != null && _blorb.SoundCount > 0;

    /// <inheritdoc />
    public int SoundCount => _blorb?.SoundCount ?? 0;

    /// <inheritdoc />
    public void PrepareSound(int number)
    {
        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Sound, number))
            return;

        byte[] data = _blorb.GetResource(BlorbUsage.Sound, number);
        string format = _blorb.GetResourceType(BlorbUsage.Sound, number);
        _backend.LoadSound(number, data, format);
    }

    /// <inheritdoc />
    public void PlaySound(int number, int volume, int repeats, ushort callback)
    {
        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Sound, number))
            return;

        var channel = GetChannel(number);
        if (channel == SoundChannel.None)
            return;

        // ZSpec11 — effects interrupt effects, music interrupts music
        if (channel == SoundChannel.Effect && _currentEffect != 0)
            StopChannelSound(SoundChannel.Effect);
        else if (channel == SoundChannel.Music && _currentMusic != 0)
            StopChannelSound(SoundChannel.Music);

        // Ensure sound data is loaded
        PrepareSound(number);

        // ZSpec11 — V5 repeats is total play count; 0 is illegal, treat as 1
        int actualRepeats = repeats;
        if (_version >= 5)
        {
            if (actualRepeats == 0)
                actualRepeats = 1;
        }
        else if (_version == 3)
        {
            // Blorb "The Looping Chunk" — V3 uses Loop chunk
            actualRepeats = GetV3Repeats(number);
        }

        // ZSpec11 "Volume guidelines" — 255 = loudest (mapped to 8)
        int mappedVolume = volume == 255 ? 8 : Math.Clamp(volume, 1, 8);

        if (channel == SoundChannel.Effect)
        {
            _currentEffect = number;
            _effectCallback = callback;
        }
        else
        {
            _currentMusic = number;
            _musicCallback = callback;
        }

        _backend.Play(number, mappedVolume, actualRepeats);
    }

    /// <inheritdoc />
    public void StopSound(int number)
    {
        // ZSpec11 — @sound_effect 0 3/4 stops all
        if (number == 0)
        {
            StopAll();
            return;
        }

        if (number == _currentEffect)
            StopChannelSound(SoundChannel.Effect);
        else if (number == _currentMusic)
            StopChannelSound(SoundChannel.Music);
    }

    /// <inheritdoc />
    public void UnloadSound(int number)
    {
        if (number == 0)
        {
            StopAll();
            _backend.UnloadAll();
            return;
        }

        StopSound(number);
        _backend.Unload(number);
    }

    /// <inheritdoc />
    public void StopAll()
    {
        StopChannelSound(SoundChannel.Effect);
        StopChannelSound(SoundChannel.Music);
    }

    /// <inheritdoc />
    public SoundChannel GetChannel(int number)
    {
        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Sound, number))
            return SoundChannel.None;

        string format = _blorb.GetResourceType(BlorbUsage.Sound, number);

        // Blorb "Z-Machine Compatibility Issues" — AIFF = effect, MOD/SONG/Ogg = music
        return format switch
        {
            "AIFF" => SoundChannel.Effect,
            "MOD " => SoundChannel.Music,
            "SONG" => SoundChannel.Music,
            "OGGV" => SoundChannel.Music,
            _ => SoundChannel.None,
        };
    }

    /// <summary>
    /// Gets the V3 repeat count from the Blorb 'Loop' chunk.
    /// Blorb "The Looping Chunk" — 1 = play once, 0 = loop forever.
    /// Default (no entry) = play once.
    /// </summary>
    private int GetV3Repeats(int number)
    {
        if (_blorb?.LoopInfo == null)
            return 1;

        if (_blorb.LoopInfo.TryGetValue(number, out int repeats))
            return repeats == 0 ? 255 : repeats; // 0 = loop forever → 0xFF

        return 1;
    }

    private void StopChannelSound(SoundChannel channel)
    {
        if (channel == SoundChannel.Effect)
        {
            if (_currentEffect != 0)
            {
                _backend.Stop(_currentEffect);
                _currentEffect = 0;
                _effectCallback = 0;
            }
        }
        else if (channel == SoundChannel.Music)
        {
            if (_currentMusic != 0)
            {
                _backend.Stop(_currentMusic);
                _currentMusic = 0;
                _musicCallback = 0;
            }
        }
    }

    /// <summary>
    /// ZSpec11 — callback only when finished naturally, not on manual stop.
    /// </summary>
    private void HandlePlaybackFinished(int number)
    {
        ushort callback = 0;

        if (number == _currentEffect)
        {
            callback = _effectCallback;
            _currentEffect = 0;
            _effectCallback = 0;
        }
        else if (number == _currentMusic)
        {
            callback = _musicCallback;
            _currentMusic = 0;
            _musicCallback = 0;
        }

        if (callback != 0)
            OnCallback?.Invoke(callback);
    }

    /// <summary>Releases backend resources.</summary>
    public void Dispose()
    {
        _backend.OnPlaybackFinished -= HandlePlaybackFinished;
        StopAll();
        if (_backend is IDisposable disposable)
            disposable.Dispose();
    }
}
