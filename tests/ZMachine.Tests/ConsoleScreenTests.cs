namespace ZMachine.Tests;

using ZMachine.IO;

/// <summary>
/// Smoke tests for the ConsoleScreen placeholder to verify the I/O
/// abstraction layer compiles and basic operations don't throw.
/// </summary>
public class ConsoleScreenTests
{
    [Fact]
    public void GetScreenSize_ReturnsPositiveDimensions()
    {
        var screen = new ConsoleScreen();
        var (cols, rows) = screen.GetScreenSize();

        Assert.True(cols > 0, "Screen width should be positive");
        Assert.True(rows > 0, "Screen height should be positive");
    }

    [Fact]
    public void SplitWindow_ZeroUnsplits_DoesNotThrow()
    {
        var screen = new ConsoleScreen();

        screen.SplitWindow(5);
        screen.SplitWindow(0);
    }

    [Fact]
    public void SetTextStyle_AllStyles_DoNotThrow()
    {
        var screen = new ConsoleScreen();

        // ZSpec11 "@set_text_style" — 0=Roman, 1=Reverse, 2=Bold, 4=Italic, 8=Fixed
        screen.SetTextStyle(0);
        screen.SetTextStyle(1);
        screen.SetTextStyle(2);
        screen.SetTextStyle(4);
        screen.SetTextStyle(8);
        screen.SetTextStyle(1 | 2 | 4); // combined styles
    }

    [Fact]
    public void BufferMode_ToggleDoesNotThrow()
    {
        var screen = new ConsoleScreen();

        screen.BufferMode(true);
        screen.BufferMode(false);
    }
}
