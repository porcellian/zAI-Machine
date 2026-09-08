namespace ZMachine.Tests.Harness;

using ZMachine.Core;

/// <summary>
/// An IInputStream that feeds lines from a pre-set list, simulating user
/// input for automated testing. When all commands are exhausted, returns
/// "quit" to terminate the game cleanly.
/// </summary>
public class ScriptedInputStream : IInputStream
{
    private readonly Queue<string> _lines;

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

    public bool HasMore => _lines.Count > 0;

    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        if (_lines.Count == 0)
            return ("quit", 13);

        string line = _lines.Dequeue();
        LinesConsumed++;
        if (line.Length > maxLength)
            line = line[..maxLength];
        return (line, 13);
    }

    public int ReadChar(int timeoutTenths = 0)
    {
        if (_lines.Count == 0)
            return 13;

        string line = _lines.Dequeue();
        LinesConsumed++;
        return line.Length > 0 ? line[0] : 13;
    }
}
