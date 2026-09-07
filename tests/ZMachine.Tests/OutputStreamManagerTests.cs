namespace ZMachine.Tests;

using System.Text;
using ZMachine.Core;

/// <summary>
/// Tests for OutputStreamManager — stream selection, dispatching,
/// stream 3 memory table writes, nesting, and suppression behavior.
/// </summary>
public class OutputStreamManagerTests
{
    #region Stream 1 — Screen

    [Fact]
    public void Stream1_ActiveByDefault()
    {
        var (mgr, screenOutput, _, _) = CreateManager();

        mgr.Print("Hello");

        Assert.Equal("Hello", screenOutput.ToString());
    }

    [Fact]
    public void Stream1_Disable_SuppressesScreen()
    {
        var (mgr, screenOutput, _, _) = CreateManager();

        mgr.SelectStream(-1);
        mgr.Print("Hello");

        Assert.Equal("", screenOutput.ToString());
    }

    [Fact]
    public void Stream1_ReEnable()
    {
        var (mgr, screenOutput, _, _) = CreateManager();

        mgr.SelectStream(-1);
        mgr.Print("suppressed");
        mgr.SelectStream(1);
        mgr.Print("visible");

        Assert.Equal("visible", screenOutput.ToString());
    }

    #endregion

    #region Stream 2 — Transcript

    [Fact]
    public void Stream2_InactiveByDefault()
    {
        var (mgr, _, transcriptOutput, _) = CreateManager();

        mgr.Print("Hello");

        Assert.Equal("", transcriptOutput.ToString());
    }

    [Fact]
    public void Stream2_Enable_ReceivesText()
    {
        var (mgr, _, transcriptOutput, _) = CreateManager();

        mgr.SelectStream(2);
        mgr.Print("Hello");

        Assert.Equal("Hello", transcriptOutput.ToString());
    }

    [Fact]
    public void Stream2_BothScreenAndTranscript()
    {
        var (mgr, screenOutput, transcriptOutput, _) = CreateManager();

        mgr.SelectStream(2);
        mgr.Print("Hello");

        Assert.Equal("Hello", screenOutput.ToString());
        Assert.Equal("Hello", transcriptOutput.ToString());
    }

    [Fact]
    public void Stream2_Disable()
    {
        var (mgr, _, transcriptOutput, _) = CreateManager();

        mgr.SelectStream(2);
        mgr.Print("first");
        mgr.SelectStream(-2);
        mgr.Print("second");

        Assert.Equal("first", transcriptOutput.ToString());
    }

    [Fact]
    public void IsTranscriptActive_ReflectsState()
    {
        var (mgr, _, _, _) = CreateManager();

        Assert.False(mgr.IsTranscriptActive);
        mgr.SelectStream(2);
        Assert.True(mgr.IsTranscriptActive);
        mgr.SelectStream(-2);
        Assert.False(mgr.IsTranscriptActive);
    }

    #endregion

    #region Stream 4 — Command Recording

    [Fact]
    public void Stream4_InactiveByDefault()
    {
        var (mgr, _, _, commandOutput) = CreateManager();

        mgr.Print("Hello");

        Assert.Equal("", commandOutput.ToString());
    }

    [Fact]
    public void Stream4_Enable_ReceivesText()
    {
        var (mgr, _, _, commandOutput) = CreateManager();

        mgr.SelectStream(4);
        mgr.Print("save");

        Assert.Equal("save", commandOutput.ToString());
    }

    #endregion

    #region Stream 3 — Memory Table

    [Fact]
    public void Stream3_CapturesTextToMemory()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int tableAddr = 0x0040;
        mgr.SelectStream(3, tableAddr);
        mgr.Print("Hello");
        mgr.SelectStream(-3);

        // Check character count word.
        Assert.Equal(5, memory.ReadWord(tableAddr));

        // Check ZSCII bytes.
        Assert.Equal((byte)'H', memory.ReadByte(tableAddr + 2));
        Assert.Equal((byte)'e', memory.ReadByte(tableAddr + 3));
        Assert.Equal((byte)'l', memory.ReadByte(tableAddr + 4));
        Assert.Equal((byte)'l', memory.ReadByte(tableAddr + 5));
        Assert.Equal((byte)'o', memory.ReadByte(tableAddr + 6));
    }

    [Fact]
    public void Stream3_SuppressesAllOtherStreams()
    {
        var (mgr, screenOutput, transcriptOutput, commandOutput) = CreateManager();

        mgr.SelectStream(2);
        mgr.SelectStream(4);

        int tableAddr = 0x0040;
        mgr.SelectStream(3, tableAddr);
        mgr.Print("captured");
        mgr.SelectStream(-3);

        // Nothing should have reached screen, transcript, or command.
        Assert.Equal("", screenOutput.ToString());
        Assert.Equal("", transcriptOutput.ToString());
        Assert.Equal("", commandOutput.ToString());
    }

    [Fact]
    public void Stream3_AfterDeselect_ScreenResumes()
    {
        var (mgr, screenOutput, _, _) = CreateManager();

        int tableAddr = 0x0040;
        mgr.SelectStream(3, tableAddr);
        mgr.Print("captured");
        mgr.SelectStream(-3);
        mgr.Print("visible");

        Assert.Equal("visible", screenOutput.ToString());
    }

    [Fact]
    public void Stream3_InitializesCountToZero()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int tableAddr = 0x0040;
        // Pre-fill with non-zero to verify initialization.
        memory.WriteWord(tableAddr, 0xFFFF);

        mgr.SelectStream(3, tableAddr);
        // Count should be 0 now.
        Assert.Equal(0, memory.ReadWord(tableAddr));
    }

    [Fact]
    public void Stream3_MultiplePrints_AccumulateCount()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int tableAddr = 0x0040;
        mgr.SelectStream(3, tableAddr);
        mgr.Print("abc");
        mgr.Print("de");
        mgr.SelectStream(-3);

        Assert.Equal(5, memory.ReadWord(tableAddr));
        Assert.Equal((byte)'d', memory.ReadByte(tableAddr + 2 + 3));
        Assert.Equal((byte)'e', memory.ReadByte(tableAddr + 2 + 4));
    }

    [Fact]
    public void IsStream3Active_ReflectsState()
    {
        var (mgr, _) = CreateManagerWithMemory();

        Assert.False(mgr.IsStream3Active);
        mgr.SelectStream(3, 0x0040);
        Assert.True(mgr.IsStream3Active);
        mgr.SelectStream(-3);
        Assert.False(mgr.IsStream3Active);
    }

    #endregion

    #region Stream 3 — Nesting

    [Fact]
    public void Stream3_Nesting_TwoLevels()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int table1 = 0x0040;
        int table2 = 0x0080;

        mgr.SelectStream(3, table1);
        mgr.Print("outer");

        mgr.SelectStream(3, table2);
        mgr.Print("inner");
        mgr.SelectStream(-3);

        // Inner table: "inner" (5 chars).
        Assert.Equal(5, memory.ReadWord(table2));
        Assert.Equal((byte)'i', memory.ReadByte(table2 + 2));

        mgr.Print("more");
        mgr.SelectStream(-3);

        // Outer table: "outer" + "more" = 9 chars.
        Assert.Equal(9, memory.ReadWord(table1));
    }

    [Fact]
    public void Stream3_Nesting_InnerDoesNotAffectOuter()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int table1 = 0x0040;
        int table2 = 0x0080;

        mgr.SelectStream(3, table1);
        mgr.Print("A");

        mgr.SelectStream(3, table2);
        mgr.Print("BB");
        mgr.SelectStream(-3);

        // Outer should only have "A" so far.
        Assert.Equal(1, memory.ReadWord(table1));

        mgr.Print("C");
        mgr.SelectStream(-3);

        // Outer: "A" + "C" = 2 chars.
        Assert.Equal(2, memory.ReadWord(table1));
        // Inner: "BB" = 2 chars.
        Assert.Equal(2, memory.ReadWord(table2));
    }

    [Fact]
    public void Stream3_Nesting_MaxDepth_Throws()
    {
        var (mgr, _) = CreateManagerWithMemory();

        for (int i = 0; i < 16; i++)
            mgr.SelectStream(3, 0x0040 + i * 0x40);

        Assert.Throws<InvalidOperationException>(
            () => mgr.SelectStream(3, 0x0440));
    }

    [Fact]
    public void Stream3_Deselect_WhenNotActive_NoOp()
    {
        var (mgr, _) = CreateManagerWithMemory();

        // Should not throw.
        mgr.SelectStream(-3);
        Assert.False(mgr.IsStream3Active);
    }

    #endregion

    #region PrintChar

    [Fact]
    public void PrintChar_DispatchesToScreen()
    {
        var (mgr, screenOutput, _, _) = CreateManager();

        mgr.PrintChar('X');

        Assert.Equal("X", screenOutput.ToString());
    }

    [Fact]
    public void PrintChar_Stream3_CapturesChar()
    {
        var (mgr, memory) = CreateManagerWithMemory();

        int tableAddr = 0x0040;
        mgr.SelectStream(3, tableAddr);
        mgr.PrintChar('Z');
        mgr.SelectStream(-3);

        Assert.Equal(1, memory.ReadWord(tableAddr));
        Assert.Equal((byte)'Z', memory.ReadByte(tableAddr + 2));
    }

    #endregion

    #region Helpers

    private static (OutputStreamManager, StringBuilder, StringBuilder, StringBuilder) CreateManager()
    {
        var memory = CreateMemory();
        var mgr = new OutputStreamManager(memory);

        var screenOutput = new StringBuilder();
        var transcriptOutput = new StringBuilder();
        var commandOutput = new StringBuilder();

        mgr.ScreenPrint = s => screenOutput.Append(s);
        mgr.TranscriptPrint = s => transcriptOutput.Append(s);
        mgr.CommandPrint = s => commandOutput.Append(s);

        return (mgr, screenOutput, transcriptOutput, commandOutput);
    }

    private static (OutputStreamManager, Memory) CreateManagerWithMemory()
    {
        var memory = CreateMemory();
        var mgr = new OutputStreamManager(memory);
        return (mgr, memory);
    }

    private static Memory CreateMemory()
    {
        int dataSize = 0x0500;
        byte[] data = new byte[dataSize];
        data[0] = 5; // version
        // Static base at the end so the entire range is dynamic memory.
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);

        var memory = new Memory();
        memory.LoadStory(data);
        return memory;
    }

    #endregion
}
