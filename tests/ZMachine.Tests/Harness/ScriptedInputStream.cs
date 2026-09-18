namespace ZMachine.Tests.Harness;

using ZMachine.Core;

/// <summary>
/// An IInputStream that feeds lines from a pre-set list, simulating user
/// input for automated testing. When all commands are exhausted, returns
/// "quit" to terminate the game cleanly.
/// </summary>
/// <remarks>
/// ReadLine consumes one command per call (for @read).
/// ReadChar iterates through the current command character by character
/// (for @read_char). Include '\r' (carriage return) at the end of a
/// command to simulate pressing Enter after typing. A single-char
/// command without '\r' works for "press any key" prompts.
/// </remarks>
public class ScriptedInputStream : IInputStream
{
    private readonly Queue<string> _lines;
    private string? _charBuffer;
    private int _charPos;
    private bool _didTimeout;

    /// <summary>Number of lines consumed so far.</summary>
    public int LinesConsumed { get; private set; }

    /// <summary>
    /// Creates a scripted input stream from the given commands.
    /// </summary>
    public ScriptedInputStream(IEnumerable<string> commands)
    {
        _lines = new Queue<string>(commands);
    }

    /// <summary>
    /// Creates a scripted input stream by reading commands from a file,
    /// one command per line. Blank lines and lines starting with '#'
    /// are skipped.
    /// </summary>
    public static ScriptedInputStream FromFile(string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToArray();
        return new ScriptedInputStream(lines);
    }

    /// <inheritdoc />
    public bool HasMore => _lines.Count > 0 || (_charBuffer != null && _charPos < _charBuffer.Length);

    /// <inheritdoc />
    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        _charBuffer = null;
        _charPos = 0;

        // When timeout is specified, simulate one timeout before returning
        // input so the game's timed callback can fire (e.g., cab arrival)
        if (timeoutTenths > 0 && _lines.Count > 0 && !_didTimeout)
        {
            _didTimeout = true;
            return ("", 0);
        }
        _didTimeout = false;

        if (_lines.Count == 0)
            return ("quit", 13);

        string line = _lines.Dequeue();
        LinesConsumed++;
        line = line.TrimEnd('\r');
        if (line.Length > maxLength)
            line = line[..maxLength];
        return (line, 13);
    }

    /// <inheritdoc />
    public int ReadChar(int timeoutTenths = 0)
    {
        if (_charBuffer != null && _charPos < _charBuffer.Length)
        {
            char c = _charBuffer[_charPos++];
            return c == '\r' ? 13 : c;
        }

        _charBuffer = null;
        _charPos = 0;

        if (_lines.Count == 0)
            return 13;

        string line = _lines.Dequeue();
        LinesConsumed++;

        if (line.Length == 0)
            return 13;

        _charBuffer = line;
        _charPos = 1;
        return line[0] == '\r' ? 13 : line[0];
    }
}
