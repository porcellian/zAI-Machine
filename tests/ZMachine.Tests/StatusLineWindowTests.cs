namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Tests for StatusLineHandler (V1-3 status line construction from globals
/// and object names) and WindowManager (split/set/erase/cursor with
/// implicit split expansion). Uses zork1.z3 for real object name decoding
/// and mock IScreen for window operation verification.
/// </summary>
public class StatusLineWindowTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region StatusLineHandler — Score Game

    [Fact]
    public void StatusLine_ScoreGame_FormatsScoreAndTurns()
    {
        var (handler, memory) = CreateStatusHandler(isTimeGame: false);
        SetGlobal(memory, 1, 35);  // score
        SetGlobal(memory, 2, 42);  // turns

        string scoreOrTime = handler.GetScoreOrTime();
        Assert.Equal("35/42", scoreOrTime);
    }

    [Fact]
    public void StatusLine_ScoreGame_NegativeScore()
    {
        var (handler, memory) = CreateStatusHandler(isTimeGame: false);
        SetGlobal(memory, 1, unchecked((ushort)-10));  // negative score
        SetGlobal(memory, 2, 5);

        string scoreOrTime = handler.GetScoreOrTime();
        Assert.Equal("-10/5", scoreOrTime);
    }

    [Fact]
    public void StatusLine_IsNotTimeGame()
    {
        var (handler, _) = CreateStatusHandler(isTimeGame: false);
        Assert.False(handler.IsTimeGame);
    }

    #endregion

    #region StatusLineHandler — Time Game

    [Fact]
    public void StatusLine_TimeGame_FormatsHoursMinutes()
    {
        var (handler, memory) = CreateStatusHandler(isTimeGame: true);
        SetGlobal(memory, 1, 14);  // hours
        SetGlobal(memory, 2, 5);   // minutes

        string scoreOrTime = handler.GetScoreOrTime();
        Assert.Equal("14:05", scoreOrTime);
    }

    [Fact]
    public void StatusLine_IsTimeGame()
    {
        var (handler, _) = CreateStatusHandler(isTimeGame: true);
        Assert.True(handler.IsTimeGame);
    }

    #endregion

    #region StatusLineHandler — Location Name (zork1)

    [Fact]
    public void StatusLine_Zork1_ReadsLocationName()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objectTable = new ObjectTable(memory, version, objTableAddr);

        int abbrAddr = memory.ReadWord(0x18);
        var textDecoder = new TextDecoder(memory, version, abbrAddr);

        // Set global 0 to object 180 ("West of House") in zork1.
        int globalsAddr = memory.ReadWord(0x0C);
        memory.WriteWord(globalsAddr, 180);

        var handler = new StatusLineHandler(memory, objectTable, textDecoder);
        string location = handler.GetLocationName();

        Assert.Equal("West of House", location);
    }

    [Fact]
    public void StatusLine_Zork1_BuildStatusLine()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objectTable = new ObjectTable(memory, version, objTableAddr);

        int abbrAddr = memory.ReadWord(0x18);
        var textDecoder = new TextDecoder(memory, version, abbrAddr);

        int globalsAddr = memory.ReadWord(0x0C);
        memory.WriteWord(globalsAddr, 180);        // global 0 = West of House
        memory.WriteWord(globalsAddr + 2, 0);       // global 1 = score 0
        memory.WriteWord(globalsAddr + 4, 0);       // global 2 = turns 0

        var handler = new StatusLineHandler(memory, objectTable, textDecoder);
        var (loc, score) = handler.BuildStatusLine();

        Assert.Equal("West of House", loc);
        Assert.Equal("0/0", score);
    }

    [Fact]
    public void StatusLine_ObjectZero_ReturnsEmpty()
    {
        var (handler, memory) = CreateStatusHandler(isTimeGame: false);
        SetGlobal(memory, 0, 0);

        string location = handler.GetLocationName();
        Assert.Equal("", location);
    }

    #endregion

    #region WindowManager — SplitWindow

    [Fact]
    public void WindowManager_SplitWindow_TracksLines()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);

        Assert.Equal(5, wm.UpperWindowLines);
        Assert.Equal(5, screen.LastSplitLines);
    }

    [Fact]
    public void WindowManager_Unsplit_SetsLinesToZero()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);
        wm.SplitWindow(0);

        Assert.Equal(0, wm.UpperWindowLines);
        Assert.Equal(0, wm.CurrentWindow);
    }

    #endregion

    #region WindowManager — SetWindow

    [Fact]
    public void WindowManager_SetWindow_TracksCurrentWindow()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SetWindow(1);
        Assert.Equal(1, wm.CurrentWindow);
        Assert.Equal(1, screen.LastSetWindow);

        wm.SetWindow(0);
        Assert.Equal(0, wm.CurrentWindow);
        Assert.Equal(0, screen.LastSetWindow);
    }

    #endregion

    #region WindowManager — SetCursor with Implicit Split

    [Fact]
    public void WindowManager_SetCursor_WithinSplit_NoExpansion()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);
        wm.SetWindow(1);
        wm.SetCursor(3, 1);

        Assert.Equal(5, wm.UpperWindowLines);
        Assert.Equal((3, 1), screen.LastCursorPosition);
    }

    [Fact]
    public void WindowManager_SetCursor_BelowSplit_ExpandsImplicitly()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(3);
        wm.SetWindow(1);
        wm.SetCursor(7, 1);

        // ZSpec11 "@set_cursor" — implicit split expansion.
        Assert.Equal(7, wm.UpperWindowLines);
        Assert.Equal((7, 1), screen.LastCursorPosition);
    }

    [Fact]
    public void WindowManager_SetCursor_LowerWindow_NoExpansion()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(3);
        wm.SetWindow(0);
        wm.SetCursor(10, 1);

        // Lower window — no implicit split.
        Assert.Equal(3, wm.UpperWindowLines);
    }

    #endregion

    #region WindowManager — EraseWindow

    [Fact]
    public void WindowManager_EraseWindowNeg1_Unsplits()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);
        wm.SetWindow(1);
        wm.EraseWindow(-1);

        Assert.Equal(0, wm.UpperWindowLines);
        Assert.Equal(0, wm.CurrentWindow);
        Assert.Equal(-1, screen.LastEraseWindow);
    }

    [Fact]
    public void WindowManager_EraseWindowNeg2_KeepsSplit()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);
        wm.EraseWindow(-2);

        Assert.Equal(5, wm.UpperWindowLines);
        Assert.Equal(-2, screen.LastEraseWindow);
    }

    [Fact]
    public void WindowManager_EraseWindow0_ClearsLower()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        wm.SplitWindow(5);
        wm.EraseWindow(0);

        Assert.Equal(5, wm.UpperWindowLines);
        Assert.Equal(0, screen.LastEraseWindow);
    }

    #endregion

    #region WindowManager — ShowStatusLine

    [Fact]
    public void WindowManager_ShowStatusLine_V3_DelegatesToScreen()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 3);

        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objectTable = new ObjectTable(memory, version, objTableAddr);
        int abbrAddr = memory.ReadWord(0x18);
        var textDecoder = new TextDecoder(memory, version, abbrAddr);

        int globalsAddr = memory.ReadWord(0x0C);
        memory.WriteWord(globalsAddr, 180);
        memory.WriteWord(globalsAddr + 2, 10);
        memory.WriteWord(globalsAddr + 4, 3);

        var statusHandler = new StatusLineHandler(memory, objectTable, textDecoder);
        wm.ShowStatusLine(statusHandler);

        Assert.Equal("West of House", screen.LastStatusLocation);
        Assert.Equal("10/3", screen.LastStatusScoreOrTime);
    }

    [Fact]
    public void WindowManager_ShowStatusLine_V5_DoesNothing()
    {
        var screen = new MockScreen();
        var wm = new WindowManager(screen, 5);

        // Should not call screen.ShowStatusLine for V5.
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));

        int version = memory.ReadByte(0x00);
        int objTableAddr = memory.ReadWord(0x0A);
        var objectTable = new ObjectTable(memory, version, objTableAddr);
        int abbrAddr = memory.ReadWord(0x18);
        var textDecoder = new TextDecoder(memory, version, abbrAddr);

        var statusHandler = new StatusLineHandler(memory, objectTable, textDecoder);
        wm.ShowStatusLine(statusHandler);

        Assert.Null(screen.LastStatusLocation);
    }

    #endregion

    #region Helpers

    private static (StatusLineHandler, Memory) CreateStatusHandler(bool isTimeGame)
    {
        var memory = new Memory();

        // Build a minimal V3 story with globals and a simple object table.
        int dataSize = 0x0400;
        byte[] data = new byte[dataSize];
        data[0] = 3; // version

        // Flags 1 bit 1: 0 = score, 1 = time.
        if (isTimeGame)
            data[1] = 0x02;

        // Static memory base at end.
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);

        // Globals table at 0x0100.
        int globalsAddr = 0x0100;
        data[0x0C] = (byte)(globalsAddr >> 8);
        data[0x0D] = (byte)(globalsAddr & 0xFF);

        // Object table at 0x0200 — minimal: just defaults + one object.
        int objTableAddr = 0x0200;
        data[0x0A] = (byte)(objTableAddr >> 8);
        data[0x0B] = (byte)(objTableAddr & 0xFF);

        // Abbreviation table at 0 (no abbreviations for synthetic).
        data[0x18] = 0;
        data[0x19] = 0;

        memory.LoadStory(data);

        var objectTable = new ObjectTable(memory, 3, objTableAddr);
        var textDecoder = new TextDecoder(memory, 3, 0);
        var handler = new StatusLineHandler(memory, objectTable, textDecoder);

        return (handler, memory);
    }

    private static void SetGlobal(Memory memory, int index, ushort value)
    {
        int globalsAddr = memory.ReadWord(0x0C);
        memory.WriteWord(globalsAddr + index * 2, value);
    }

    private class MockScreen : IScreen
    {
        public int LastSplitLines { get; private set; } = -1;
        public int LastSetWindow { get; private set; } = -1;
        public int LastEraseWindow { get; private set; } = int.MinValue;
        public (int Line, int Column) LastCursorPosition { get; private set; }
        public string? LastStatusLocation { get; private set; }
        public string? LastStatusScoreOrTime { get; private set; }

        public void Print(string text) { }
        public void PrintChar(char c) { }
        public void NewLine() { }

        public void ShowStatusLine(string location, string scoreOrTime)
        {
            LastStatusLocation = location;
            LastStatusScoreOrTime = scoreOrTime;
        }

        public void SplitWindow(int lines) => LastSplitLines = lines;
        public void SetWindow(int window) => LastSetWindow = window;
        public void EraseLine() { }
        public void EraseWindow(int window) => LastEraseWindow = window;

        public void SetCursor(int line, int column) =>
            LastCursorPosition = (line, column);

        public void SetTextStyle(int style) { }
        public void BufferMode(bool enabled) { }
        public (int Columns, int Rows) GetScreenSize() => (80, 25);
    }

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName!;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
