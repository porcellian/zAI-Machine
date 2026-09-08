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

    public void Print(string text) => _output.Append(text);
    public void PrintChar(char c) => _output.Append(c);
    public void NewLine() => _output.AppendLine();

    public void ShowStatusLine(string location, string scoreOrTime)
    {
        _statusLines.Add($"{location} | {scoreOrTime}");
    }

    public void SplitWindow(int lines) { }
    public void SetWindow(int window) { }
    public void EraseLine() { }
    public void EraseWindow(int window) { }
    public void SetCursor(int line, int column) { }
    public void SetTextStyle(int style) { }
    public void BufferMode(bool enabled) { }
    public (int Columns, int Rows) GetScreenSize() => (80, 25);
}
