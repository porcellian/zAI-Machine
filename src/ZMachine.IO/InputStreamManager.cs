namespace ZMachine.IO;

/// <summary>
/// Manages the Z-Machine input streams: stream 0 (keyboard) and
/// stream 1 (file playback). Implements @input_stream switching and
/// automatic fallback when file input is exhausted.
/// </summary>
/// <remarks>
/// ZSpec S10 — @input_stream 0/1 switches between keyboard and file.
/// When file input is exhausted, the interpreter reverts to keyboard.
/// </remarks>
public class InputStreamManager : IInputStream
{
    private readonly IInputStream _keyboard;
    private IInputStream? _file;
    private int _activeStream;

    public InputStreamManager(IInputStream keyboard)
    {
        _keyboard = keyboard;
    }

    /// <summary>The currently active stream number (0 or 1).</summary>
    public int ActiveStream => _activeStream;

    public bool HasMore => ActiveInputStream.HasMore;

    /// <summary>
    /// Switches the active input stream. Stream 0 = keyboard, stream 1 = file.
    /// </summary>
    /// <remarks>
    /// ZSpec S10 — @input_stream: 0 selects keyboard, 1 selects file playback.
    /// </remarks>
    public void SelectStream(int stream, IInputStream? fileStream = null)
    {
        _activeStream = stream;
        if (stream == 1 && fileStream != null)
            _file = fileStream;
    }

    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        var source = ActiveInputStream;
        var result = source.ReadLine(maxLength, timeoutTenths);

        if (_activeStream == 1 && !source.HasMore)
            _activeStream = 0;

        return result;
    }

    public int ReadChar(int timeoutTenths = 0)
    {
        var source = ActiveInputStream;
        int result = source.ReadChar(timeoutTenths);

        if (_activeStream == 1 && !source.HasMore)
            _activeStream = 0;

        return result;
    }

    private IInputStream ActiveInputStream =>
        _activeStream == 1 && _file != null ? _file : _keyboard;
}
