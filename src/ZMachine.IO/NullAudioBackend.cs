namespace ZMachine.IO;

/// <summary>
/// Silent audio backend that accepts all operations but produces
/// no sound. Used when no audio library is available, or in tests.
/// </summary>
public class NullAudioBackend : IAudioBackend
{
    private readonly HashSet<int> _loaded = new();

    /// <inheritdoc />
    public event Action<int>? OnPlaybackFinished;

    /// <inheritdoc />
    public void LoadSound(int number, byte[] data, string format)
        => _loaded.Add(number);

    /// <inheritdoc />
    public void Play(int number, int volume, int repeats) { }

    /// <inheritdoc />
    public void Stop(int number) { }

    /// <inheritdoc />
    public void Unload(int number)
        => _loaded.Remove(number);

    /// <inheritdoc />
    public void UnloadAll()
        => _loaded.Clear();

    /// <summary>
    /// Simulates a playback-finished event for testing.
    /// </summary>
    internal void SimulateFinished(int number)
        => OnPlaybackFinished?.Invoke(number);
}
