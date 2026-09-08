namespace ZMachine.Tests;

using System.Text;
using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Integration tests for the ZMachine execution loop. Boots real story files
/// with scripted input and verifies the output text at each step.
/// </summary>
public class ZMachineIntegrationTests
{
    private const string Zork1Path = "stories/zork1.z3";

    /// <summary>
    /// Verifies that Zork I boots, prints opening text, and responds
    /// to basic commands: look and inventory.
    /// </summary>
    [Fact]
    public void Zork1_BootAndFirstMoves()
    {
        if (!File.Exists(Zork1Path))
            return; // Skip on CI where story files aren't available.

        var commands = new[] { "look", "inventory", "quit" };
        var (output, _) = RunWithCommands(Zork1Path, commands);

        // Opening text should mention ZORK and West of House.
        Assert.Contains("ZORK", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("West of House", output, StringComparison.OrdinalIgnoreCase);

        // @look should describe the location again.
        Assert.Contains("white house", output, StringComparison.OrdinalIgnoreCase);

        // @inventory should list items or say nothing carried.
        bool hasInventory = output.Contains("carrying", StringComparison.OrdinalIgnoreCase)
                         || output.Contains("empty-handed", StringComparison.OrdinalIgnoreCase)
                         || output.Contains("nothing", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasInventory, "Expected inventory response");
    }

    /// <summary>
    /// Verifies that the machine stops after @quit.
    /// </summary>
    [Fact]
    public void Zork1_Quit_StopsMachine()
    {
        if (!File.Exists(Zork1Path))
            return;

        var commands = new[] { "quit", "y" };
        var (_, machine) = RunWithCommands(Zork1Path, commands);

        Assert.False(machine.Running);
    }

    /// <summary>
    /// Verifies the machine can execute multiple turns without crashing.
    /// </summary>
    [Fact]
    public void Zork1_FiveMoves()
    {
        if (!File.Exists(Zork1Path))
            return;

        var commands = new[] { "look", "open mailbox", "read leaflet", "go north", "inventory", "quit", "y" };
        var (output, machine) = RunWithCommands(Zork1Path, commands);

        // Should have gotten through multiple turns without exceptions.
        Assert.Contains("West of House", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mailbox", output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs a story file with a list of scripted commands and captures
    /// all screen output.
    /// </summary>
    private static (string Output, Interpreter Machine) RunWithCommands(
        string storyPath, string[] commands)
    {
        var sb = new StringBuilder();
        var inputStream = new ScriptedInputStream(commands);
        var screen = new CaptureScreen(sb);

        var machine = new Interpreter();
        machine.Load(storyPath, inputStream, screen);
        machine.Run();

        return (sb.ToString(), machine);
    }

    /// <summary>
    /// A simple IInputStream that feeds lines from a pre-set list.
    /// When exhausted, signals the machine to quit by returning empty
    /// input.
    /// </summary>
    private class ScriptedInputStream : IInputStream
    {
        private readonly Queue<string> _lines;

        public ScriptedInputStream(IEnumerable<string> lines)
        {
            _lines = new Queue<string>(lines);
        }

        public bool HasMore => _lines.Count > 0;

        public (string Text, int TerminatingChar) ReadLine(int maxLength, int timeoutTenths = 0)
        {
            if (_lines.Count == 0)
                return ("quit", 13);

            string line = _lines.Dequeue();
            if (line.Length > maxLength)
                line = line[..maxLength];
            return (line, 13);
        }

        public int ReadChar(int timeoutTenths = 0)
        {
            if (_lines.Count == 0)
                return 13;
            string line = _lines.Dequeue();
            return line.Length > 0 ? line[0] : 13;
        }
    }

    /// <summary>
    /// A minimal IScreen that captures all printed text to a StringBuilder.
    /// </summary>
    private class CaptureScreen : IScreen
    {
        private readonly StringBuilder _sb;

        public CaptureScreen(StringBuilder sb) => _sb = sb;

        public void Print(string text) => _sb.Append(text);
        public void PrintChar(char c) => _sb.Append(c);
        public void NewLine() => _sb.AppendLine();
        public void ShowStatusLine(string location, string scoreOrTime) { }
        public void SplitWindow(int lines) { }
        public void SetWindow(int window) { }
        public void EraseLine() { }
        public void EraseWindow(int window) { }
        public void SetCursor(int line, int column) { }
        public void SetTextStyle(int style) { }
        public void BufferMode(bool enabled) { }
        public (int Columns, int Rows) GetScreenSize() => (80, 25);
    }
}
