namespace ZMachine.IO;

using ZMachine.Core;

/// <summary>
/// File-based IInputStream that plays back pre-recorded commands.
/// This is input stream 1 — the counterpart of output stream 4's
/// command recording.
/// </summary>
/// <remarks>
/// ZSpec S10 — Stream 1 reads commands from a file. When the file
/// is exhausted, the interpreter reverts to stream 0 (keyboard).
/// Each line in the file is one command.
/// </remarks>
public class FileInputStream : IInputStream
{
    private readonly Queue<string> _lines;

    public FileInputStream(IEnumerable<string> lines)
    {
        _lines = new Queue<string>(lines);
    }

    public FileInputStream(string filePath)
        : this(File.ReadAllLines(filePath))
    {
    }

    public bool HasMore => _lines.Count > 0;

    public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
    {
        if (_lines.Count == 0)
            return ("", 0);

        string line = _lines.Dequeue();
        if (line.Length > maxLength)
            line = line[..maxLength];

        return (line, 13);
    }

    public int ReadChar(int timeoutTenths = 0)
    {
        if (_lines.Count == 0)
            return 0;

        string line = _lines.Dequeue();
        return line.Length > 0 ? line[0] : 13;
    }
}
