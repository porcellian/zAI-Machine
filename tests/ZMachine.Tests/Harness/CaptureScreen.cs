namespace ZMachine.Tests.Harness;

using System.Text;
using ZMachine.Core;

/// <summary>
/// An IScreen that captures all printed text to a StringBuilder for
/// test assertions. Status line output is captured separately.
/// </summary>
public class CaptureScreen : IScreen
{
    private readonly StringBuilder _output = new();
    private readonly List<string> _statusLines = new();

    /// <summary>All captured screen output as a single string.</summary>
    public string Output => _output.ToString();

    /// <summary>All status lines displayed during the run.</summary>
    public IReadOnlyList<string> StatusLines => _statusLines;

    /// <inheritdoc />
    public void Print(string text) => _output.Append(text);

    /// <inheritdoc />
    public void PrintChar(char c) => _output.Append(c);

    /// <inheritdoc />
    public void NewLine() => _output.AppendLine();

    /// <summary>Captures the status line as "location | scoreOrTime".</summary>
    public void ShowStatusLine(string location, string scoreOrTime)
    {
        _statusLines.Add($"{location} | {scoreOrTime}");
    }

    /// <inheritdoc />
    public void SplitWindow(int lines) { }

    /// <inheritdoc />
    public void SetWindow(int window) { }

    /// <inheritdoc />
    public void EraseLine() { }

    /// <inheritdoc />
    public void EraseWindow(int window) { }

    /// <inheritdoc />
    public void SetCursor(int line, int column) { }

    /// <inheritdoc />
    public void SetTextStyle(int style) { }

    /// <inheritdoc />
    public void BufferMode(bool enabled) { }

    /// <inheritdoc />
    public (int Columns, int Rows) GetScreenSize() => (80, 25);
}
