namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for ScreenStyleOps — V5+ screen and style opcodes: text style
/// combinations, font switching, colour setting, cursor queries, buffer
/// mode, Unicode capability checks, save/restore undo, and the fixed-pitch
/// header bit.
/// </summary>
public class ScreenStyleOpsTests
{
    #region @set_text_style

    [Fact]
    public void SetTextStyle_Roman_ClearsAll()
    {
        var ops = CreateOps();

        ops.SetTextStyle(2); // Bold
        ops.SetTextStyle(4); // Italic (combined: 6)
        Assert.Equal(6, ops.CurrentStyle);

        ops.SetTextStyle(0); // Roman clears
        Assert.Equal(0, ops.CurrentStyle);
    }

    [Fact]
    public void SetTextStyle_CombinesViaAddition()
    {
        var ops = CreateOps();

        ops.SetTextStyle(1); // Reverse
        ops.SetTextStyle(2); // Bold
        Assert.Equal(3, ops.CurrentStyle);

        ops.SetTextStyle(4); // Italic
        Assert.Equal(7, ops.CurrentStyle);

        ops.SetTextStyle(8); // Fixed
        Assert.Equal(15, ops.CurrentStyle);
    }

    [Fact]
    public void SetTextStyle_DuplicateStyleIdempotent()
    {
        var ops = CreateOps();

        ops.SetTextStyle(2);
        ops.SetTextStyle(2);
        Assert.Equal(2, ops.CurrentStyle);
    }

    [Fact]
    public void SetTextStyle_InvokesCallback()
    {
        var ops = CreateOps();
        int callbackStyle = -1;
        ops.OnSetTextStyle = s => callbackStyle = s;

        ops.SetTextStyle(2);
        Assert.Equal(2, callbackStyle);

        ops.SetTextStyle(4);
        Assert.Equal(6, callbackStyle);
    }

    #endregion

    #region @set_font

    [Fact]
    public void SetFont_Normal_ReturnsPrevious()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        ushort prev = ops.SetFont(1);
        Assert.Equal((ushort)1, prev); // was 1 (default), set to 1
    }

    [Fact]
    public void SetFont_FixedPitch_ReturnsPrevious()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        ushort prev = ops.SetFont(4);
        Assert.Equal((ushort)1, prev); // was 1, now 4

        ushort prev2 = ops.SetFont(1);
        Assert.Equal((ushort)4, prev2); // was 4, back to 1
    }

    [Fact]
    public void SetFont_CharacterGraphics_ReturnsPrevious()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        ushort prev = ops.SetFont(3);
        Assert.Equal((ushort)1, prev);
        Assert.Equal(3, ops.CurrentFont);
    }

    [Fact]
    public void SetFont_Font2_Returns0_Unchanged()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        ushort result = ops.SetFont(2);
        Assert.Equal((ushort)0, result);
        Assert.Equal(1, ops.CurrentFont);
    }

    [Fact]
    public void SetFont_Font5Plus_Returns0_Unchanged()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        Assert.Equal((ushort)0, ops.SetFont(5));
        Assert.Equal((ushort)0, ops.SetFont(100));
        Assert.Equal(1, ops.CurrentFont);
    }

    [Fact]
    public void SetFont_Font0_ReturnsCurrentWithoutChanging()
    {
        var ops = CreateOps();
        ops.OnSetFont = f => f;

        ops.SetFont(4);
        ushort result = ops.SetFont(0);
        Assert.Equal((ushort)4, result);
        Assert.Equal(4, ops.CurrentFont);
    }

    #endregion

    #region @set_colour

    [Fact]
    public void SetColour_SetsForegoundAndBackground()
    {
        var ops = CreateOps();
        int cbFg = 0, cbBg = 0;
        ops.OnSetColour = (fg, bg) => { cbFg = fg; cbBg = bg; };

        ops.SetColour(3, 9); // red fg, white bg
        Assert.Equal(3, ops.ForegroundColor);
        Assert.Equal(9, ops.BackgroundColor);
        Assert.Equal(3, cbFg);
        Assert.Equal(9, cbBg);
    }

    [Fact]
    public void SetColour_Zero_MeansCurrent()
    {
        var ops = CreateOps();
        ops.OnSetColour = (_, _) => { };

        ops.SetColour(3, 9);
        ops.SetColour(0, 5); // fg unchanged, bg → yellow
        Assert.Equal(3, ops.ForegroundColor);
        Assert.Equal(5, ops.BackgroundColor);

        ops.SetColour(6, 0); // fg → blue, bg unchanged
        Assert.Equal(6, ops.ForegroundColor);
        Assert.Equal(5, ops.BackgroundColor);
    }

    [Fact]
    public void SetColour_Default_IsColour1()
    {
        var ops = CreateOps();
        ops.OnSetColour = (_, _) => { };

        ops.SetColour(3, 9);
        ops.SetColour(1, 1); // reset to default
        Assert.Equal(1, ops.ForegroundColor);
        Assert.Equal(1, ops.BackgroundColor);
    }

    [Fact]
    public void SetColour_AllStandardColors()
    {
        var ops = CreateOps();
        ops.OnSetColour = (_, _) => { };

        // 2=black through 9=white
        for (int c = 2; c <= 9; c++)
        {
            ops.SetColour(c, c);
            Assert.Equal(c, ops.ForegroundColor);
            Assert.Equal(c, ops.BackgroundColor);
        }
    }

    [Fact]
    public void SetColour_Standard11Greys()
    {
        var ops = CreateOps();
        ops.OnSetColour = (_, _) => { };

        // 10=light grey, 11=medium grey, 12=dark grey
        for (int c = 10; c <= 12; c++)
        {
            ops.SetColour(c, 1);
            Assert.Equal(c, ops.ForegroundColor);
        }
    }

    #endregion

    #region @get_cursor

    [Fact]
    public void GetCursor_WritesPositionToMemory()
    {
        var (ops, memory) = CreateOpsWithMemory();
        ops.OnGetCursor = () => (5, 12);

        ushort array = 0x1000;
        ops.GetCursor(array);

        Assert.Equal((ushort)5, memory.ReadWord(array));
        Assert.Equal((ushort)12, memory.ReadWord(array + 2));
    }

    [Fact]
    public void GetCursor_DefaultsTo11_WhenNoCallback()
    {
        var (ops, memory) = CreateOpsWithMemory();

        ushort array = 0x1000;
        ops.GetCursor(array);

        Assert.Equal((ushort)1, memory.ReadWord(array));
        Assert.Equal((ushort)1, memory.ReadWord(array + 2));
    }

    #endregion

    #region @erase_line

    [Fact]
    public void EraseLine_Value1_InvokesCallback()
    {
        var ops = CreateOps();
        bool called = false;
        ops.OnEraseLine = () => called = true;

        ops.EraseLine(1);
        Assert.True(called);
    }

    [Fact]
    public void EraseLine_ValueNot1_Ignored()
    {
        var ops = CreateOps();
        bool called = false;
        ops.OnEraseLine = () => called = true;

        ops.EraseLine(0);
        Assert.False(called);

        ops.EraseLine(2);
        Assert.False(called);
    }

    #endregion

    #region @buffer_mode

    [Fact]
    public void BufferMode_1_EnablesWrapping()
    {
        var ops = CreateOps();
        bool? enabled = null;
        ops.OnBufferMode = b => enabled = b;

        ops.BufferMode(1);
        Assert.True(enabled);
    }

    [Fact]
    public void BufferMode_0_DisablesWrapping()
    {
        var ops = CreateOps();
        bool? enabled = null;
        ops.OnBufferMode = b => enabled = b;

        ops.BufferMode(0);
        Assert.False(enabled);
    }

    #endregion

    #region @check_unicode

    [Fact]
    public void CheckUnicode_PrintableAscii_BothBits()
    {
        var ops = CreateOps();

        ushort result = ops.CheckUnicode((ushort)'A');
        Assert.Equal((ushort)3, result); // bit 0 + bit 1
    }

    [Fact]
    public void CheckUnicode_Space_BothBits()
    {
        var ops = CreateOps();

        Assert.Equal((ushort)3, ops.CheckUnicode(32));
    }

    [Fact]
    public void CheckUnicode_BmpNonAscii_PrintOnly()
    {
        var ops = CreateOps();

        // é (0xE9) — printable but outside ASCII input range.
        ushort result = ops.CheckUnicode(0xE9);
        Assert.Equal((ushort)1, result); // bit 0 only
    }

    [Fact]
    public void CheckUnicode_ControlCode_NeitherBit()
    {
        var ops = CreateOps();

        Assert.Equal((ushort)0, ops.CheckUnicode(10)); // newline
        Assert.Equal((ushort)0, ops.CheckUnicode(127)); // DEL
        Assert.Equal((ushort)0, ops.CheckUnicode(0x9F)); // last C1
    }

    #endregion

    #region @save_undo / @restore_undo

    [Fact]
    public void SaveUndo_Returns1()
    {
        var (ops, _) = CreateOpsWithMemory();

        Assert.Equal((ushort)1, ops.SaveUndo());
    }

    [Fact]
    public void RestoreUndo_WithoutSave_Returns0()
    {
        var (ops, _) = CreateOpsWithMemory();

        Assert.Equal((ushort)0, ops.RestoreUndo());
    }

    [Fact]
    public void SaveAndRestoreUndo_RestoresDynamicMemory()
    {
        var (ops, memory) = CreateOpsWithMemory();

        // Read original value, save undo.
        byte original = memory.ReadByte(0x20);
        ops.SaveUndo();

        // Modify dynamic memory.
        memory.WriteByte(0x20, (byte)(original ^ 0xFF));
        Assert.NotEqual(original, memory.ReadByte(0x20));

        // Restore undo.
        ushort result = ops.RestoreUndo();
        Assert.Equal((ushort)2, result);
        Assert.Equal(original, memory.ReadByte(0x20));
    }

    [Fact]
    public void RestoreUndo_OnlyWorksOnce()
    {
        var (ops, memory) = CreateOpsWithMemory();

        ops.SaveUndo();
        memory.WriteByte(0x20, 0xFF);

        Assert.Equal((ushort)2, ops.RestoreUndo());
        Assert.Equal((ushort)0, ops.RestoreUndo()); // second restore fails
    }

    #endregion

    #region Fixed-pitch header bit

    [Fact]
    public void IsFixedPitchRequested_DefaultFalse()
    {
        var (ops, _) = CreateOpsWithMemory();

        Assert.False(ops.IsFixedPitchRequested());
    }

    [Fact]
    public void IsFixedPitchRequested_WhenBitSet_True()
    {
        var (ops, memory) = CreateOpsWithMemory();

        // Flags 2 low byte is at $11. Set bit 3.
        byte flags2 = memory.ReadByte(0x11);
        memory.WriteByte(0x11, (byte)(flags2 | 0x08));

        Assert.True(ops.IsFixedPitchRequested());
    }

    [Fact]
    public void IsFixedPitchRequested_WhenBitCleared_False()
    {
        var (ops, memory) = CreateOpsWithMemory();

        byte flags2 = memory.ReadByte(0x11);
        memory.WriteByte(0x11, (byte)(flags2 | 0x08));
        Assert.True(ops.IsFixedPitchRequested());

        memory.WriteByte(0x11, (byte)(flags2 & ~0x08));
        Assert.False(ops.IsFixedPitchRequested());
    }

    #endregion

    #region Helpers

    private static ScreenStyleOps CreateOps()
    {
        var (ops, _) = CreateOpsWithMemory();
        return ops;
    }

    private static (ScreenStyleOps Ops, Memory Memory) CreateOpsWithMemory()
    {
        var memory = new Memory();
        byte[] story = new byte[0x10000];
        story[0x00] = 5;
        story[0x04] = 0x80; story[0x05] = 0x00;
        story[0x0E] = 0x80; story[0x0F] = 0x00;
        memory.LoadStory(story);

        var ops = new ScreenStyleOps(memory, 5);
        return (ops, memory);
    }

    #endregion
}
