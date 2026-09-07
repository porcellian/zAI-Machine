namespace ZMachine.Tests;

using ZMachine.IO;

/// <summary>
/// Tests for ConsoleScreen — word wrapping, status line formatting,
/// window management, and text output. Uses non-terminal mode with
/// StringWriter to capture output without ANSI escape codes.
/// </summary>
public class ConsoleScreenTests
{
    #region Word Wrapping

    [Fact]
    public void BufferMode_ShortLine_NoWrap()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("Hello, world!");
        screen.NewLine();

        Assert.Equal("Hello, world!\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_ExactWidth_NoWrap()
    {
        var (screen, writer) = CreateScreen(10);

        screen.Print("1234567890");
        screen.NewLine();

        Assert.Equal("1234567890\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_WrapsAtLastSpace()
    {
        var (screen, writer) = CreateScreen(10);

        screen.Print("Hello world");
        screen.NewLine();

        Assert.Equal("Hello\nworld\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_MultipleWords_NoneExceedWidth()
    {
        var (screen, writer) = CreateScreen(20);

        screen.Print("The quick brown fox jumps over the lazy dog");
        screen.NewLine();

        string output = GetOutput(writer);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
            Assert.True(line.Length <= 20, $"Line exceeds 20 columns: \"{line}\"");
    }

    [Fact]
    public void BufferMode_WordLongerThanWidth_ForceBreak()
    {
        var (screen, writer) = CreateScreen(5);

        screen.Print("abcdefgh");
        screen.NewLine();

        string output = GetOutput(writer);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("abcde", lines[0]);
        Assert.Equal("fgh", lines[1]);
    }

    [Fact]
    public void BufferMode_80Columns_WrapsLongSentence()
    {
        var (screen, writer) = CreateScreen(80);

        string text = "You are standing in an open field west of a white house, " +
            "with a boarded front door. There is a small mailbox here.";
        screen.Print(text);
        screen.NewLine();

        string output = GetOutput(writer);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
            Assert.True(line.Length <= 80, $"Line exceeds 80 columns: \"{line}\"");

        string rejoined = string.Join(" ", lines);
        Assert.Equal(text, rejoined);
    }

    [Fact]
    public void BufferMode_MultiplePrints_AccumulateBeforeWrap()
    {
        var (screen, writer) = CreateScreen(10);

        screen.Print("Hello ");
        screen.Print("world");
        screen.NewLine();

        Assert.Equal("Hello\nworld\n", GetOutput(writer));
    }

    [Fact]
    public void BufferModeOff_NoWordWrapping()
    {
        var (screen, writer) = CreateScreen(10);

        screen.BufferMode(false);
        screen.Print("Hello world!");
        screen.NewLine();

        string output = GetOutput(writer);
        Assert.Contains("Hello worl", output);
    }

    [Fact]
    public void BufferMode_NewlineFlushesBuffer()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("Line one");
        screen.NewLine();
        screen.Print("Line two");
        screen.NewLine();

        Assert.Equal("Line one\nLine two\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_ConsecutiveSpaces_Preserved()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("a  b  c");
        screen.NewLine();

        Assert.Equal("a  b  c\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_EmptyPrint_NoOutput()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("");
        screen.NewLine();

        Assert.Equal("\n", GetOutput(writer));
    }

    [Fact]
    public void BufferMode_TrailingSpaceAtBoundary()
    {
        var (screen, writer) = CreateScreen(6);

        screen.Print("abcde fghij");
        screen.NewLine();

        string output = GetOutput(writer);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("abcde", lines[0]);
        Assert.Equal("fghij", lines[1]);
    }

    [Fact]
    public void BufferMode_SuccessiveWraps()
    {
        var (screen, writer) = CreateScreen(10);

        screen.Print("aa bb cc dd ee ff gg hh");
        screen.NewLine();

        string output = GetOutput(writer);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
            Assert.True(line.Length <= 10, $"Line exceeds 10 columns: \"{line}\"");
    }

    #endregion

    #region Status Line

    [Fact]
    public void StatusLine_LocationLeftAligned_ScoreRightAligned()
    {
        string line = ConsoleScreen.FormatStatusLine("West of House", "Score: 0/0", 40);

        Assert.Equal(40, line.Length);
        Assert.StartsWith("West of House", line);
        Assert.EndsWith("Score: 0/0", line);
    }

    [Fact]
    public void StatusLine_FullWidth()
    {
        string line = ConsoleScreen.FormatStatusLine("Kitchen", "3:15 PM", 80);

        Assert.Equal(80, line.Length);
        Assert.StartsWith("Kitchen", line);
        Assert.EndsWith("3:15 PM", line);
    }

    [Fact]
    public void StatusLine_TruncatesIfTooLong()
    {
        string longLocation = new string('A', 50);
        string longScore = new string('B', 50);
        string line = ConsoleScreen.FormatStatusLine(longLocation, longScore, 80);

        Assert.Equal(80, line.Length);
    }

    [Fact]
    public void StatusLine_MinimumPadding()
    {
        string line = ConsoleScreen.FormatStatusLine("A", "B", 5);

        Assert.Equal(5, line.Length);
        Assert.Equal("A   B", line);
    }

    [Fact]
    public void ShowStatusLine_WritesFormattedLine()
    {
        var (screen, writer) = CreateScreen(40);

        screen.ShowStatusLine("West of House", "Score: 0/0");

        string output = GetOutput(writer);
        Assert.Contains("West of House", output);
        Assert.Contains("Score: 0/0", output);
    }

    #endregion

    #region Window Management

    [Fact]
    public void SplitWindow_SetWindow_ToUpper()
    {
        var (screen, writer) = CreateScreen(20);

        screen.SplitWindow(3);
        screen.SetWindow(1);
        screen.Print("UPPER");
        screen.SetWindow(0);
        screen.Print("lower");
        screen.NewLine();

        string output = GetOutput(writer);
        Assert.Contains("UPPER", output);
        Assert.Contains("lower", output);
    }

    [Fact]
    public void SplitWindow_Zero_Unsplits()
    {
        var (screen, _) = CreateScreen(20);

        screen.SplitWindow(5);
        screen.SplitWindow(0);

        screen.Print("After unsplit");
        screen.NewLine();
    }

    [Fact]
    public void SetCursor_UpperWindow_DoesNotCrash()
    {
        var (screen, _) = CreateScreen(80);

        screen.SplitWindow(3);
        screen.SetWindow(1);
        screen.SetCursor(2, 5);
    }

    #endregion

    #region Text Style

    [Fact]
    public void SetTextStyle_NonTerminal_NoOutput()
    {
        var (screen, writer) = CreateScreen(80);

        screen.SetTextStyle(0);
        screen.SetTextStyle(1);
        screen.SetTextStyle(2);
        screen.SetTextStyle(4);
        screen.SetTextStyle(8);
        screen.SetTextStyle(1 | 2 | 4);

        Assert.Equal("", GetOutput(writer));
    }

    #endregion

    #region PrintChar

    [Fact]
    public void PrintChar_SingleCharacters()
    {
        var (screen, writer) = CreateScreen(80);

        screen.PrintChar('H');
        screen.PrintChar('i');
        screen.NewLine();

        Assert.Equal("Hi\n", GetOutput(writer));
    }

    [Fact]
    public void PrintChar_NewlineFlushes()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("Hello");
        screen.PrintChar('\n');

        Assert.Equal("Hello\n", GetOutput(writer));
    }

    #endregion

    #region Screen Size

    [Fact]
    public void GetScreenSize_ReturnsConstructorValues()
    {
        var (screen, _) = CreateScreen(132, 50);

        var (cols, rows) = screen.GetScreenSize();
        Assert.Equal(132, cols);
        Assert.Equal(50, rows);
    }

    #endregion

    #region Erase

    [Fact]
    public void EraseWindow_MinusOne_Unsplits()
    {
        var (screen, _) = CreateScreen(80);

        screen.SplitWindow(5);
        screen.EraseWindow(-1);

        screen.Print("After clear");
        screen.NewLine();
    }

    [Fact]
    public void EraseLine_NonTerminal_NoOutput()
    {
        var (screen, writer) = CreateScreen(80);

        screen.EraseLine();

        Assert.Equal("", GetOutput(writer));
    }

    #endregion

    #region BufferMode Toggle

    [Fact]
    public void BufferMode_DisableFlushes()
    {
        var (screen, writer) = CreateScreen(80);

        screen.Print("buffered text");
        screen.BufferMode(false);

        string output = GetOutput(writer);
        Assert.Contains("buffered text", output);
    }

    #endregion

    #region Helpers

    private static (ConsoleScreen, StringWriter) CreateScreen(int columns, int rows = 25)
    {
        var writer = new StringWriter();
        var screen = new ConsoleScreen(writer, columns, rows, isTerminal: false);
        return (screen, writer);
    }

    private static string GetOutput(StringWriter writer)
    {
        writer.Flush();
        return writer.ToString().Replace("\r\n", "\n");
    }

    #endregion
}
