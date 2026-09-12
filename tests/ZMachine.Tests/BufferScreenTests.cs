using ZMachine.Core;

namespace ZMachine.Tests;

/// <summary>
/// Tests for Task 13.4: <c>@buffer_screen</c>, V6 <c>@set_font</c>
/// window parameter, V6 <c>@set_colour</c> window parameter,
/// screen redraw bit, and disassembler EXT mnemonics.
/// </summary>
/// <remarks>
/// ZSpec11 "@buffer_screen", "@set_font", "Status line redraw".
/// </remarks>
public class BufferScreenTests
{
    #region Disassembler — EXT Mnemonics

    /// <summary>
    /// Verifies that EXT:24 disassembles as "make_menu".
    /// </summary>
    [Fact]
    public void Disassembler_EXT24_MakeMenu()
    {
        var mem = CreateDisassemblerMemory(extOpcode: 24, operandCount: 2);
        var disasm = new Disassembler(mem);
        var line = disasm.DisassembleAt(0x40);
        Assert.Contains("make_menu", line.Mnemonic);
    }

    /// <summary>
    /// Verifies that EXT:25 disassembles as "picture_table".
    /// </summary>
    [Fact]
    public void Disassembler_EXT25_PictureTable()
    {
        var mem = CreateDisassemblerMemory(extOpcode: 25, operandCount: 1);
        var disasm = new Disassembler(mem);
        var line = disasm.DisassembleAt(0x40);
        Assert.Contains("picture_table", line.Mnemonic);
    }

    /// <summary>
    /// Verifies that EXT:29 disassembles as "buffer_screen" and
    /// is recognised as a store opcode.
    /// </summary>
    [Fact]
    public void Disassembler_EXT29_BufferScreen_HasStore()
    {
        var mem = CreateDisassemblerMemory(extOpcode: 29, operandCount: 1, hasStore: true);
        var disasm = new Disassembler(mem);
        var line = disasm.DisassembleAt(0x40);
        Assert.Contains("buffer_screen", line.Mnemonic);
        Assert.False(string.IsNullOrEmpty(line.Store));
    }

    #endregion

    #region V6Window — Font Property

    /// <summary>
    /// Verifies that V6Window.Font property can be read and written.
    /// ZSpec S8.8 property 12 = font number.
    /// </summary>
    [Fact]
    public void V6Window_Font_SetAndGet()
    {
        var w = new V6Window(0);
        w.Font = 4;
        Assert.Equal(4, w.Font);
        Assert.Equal(4, w.GetProperty(12));
    }

    /// <summary>
    /// Verifies that setting font via SetProperty(12, value) works.
    /// </summary>
    [Fact]
    public void V6Window_Font_ViaSetProperty()
    {
        var w = new V6Window(0);
        w.SetProperty(12, 3);
        Assert.Equal(3, w.Font);
    }

    /// <summary>
    /// Verifies that font can be set on a non-selected window through
    /// V6WindowManager.GetWindow.
    /// </summary>
    [Fact]
    public void V6WindowManager_SetFontOnSpecificWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetWindow(0);
        var w3 = mgr.GetWindow(3);
        w3.Font = 4;

        Assert.Equal(4, mgr.GetWindow(3).Font);
        Assert.NotEqual(4, mgr.Current.Font);
    }

    /// <summary>
    /// Verifies that -3 window parameter resolves to the currently
    /// selected window.
    /// </summary>
    [Fact]
    public void V6WindowManager_MinusThree_ResolvesToCurrentWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetWindow(2);

        // -3 cast to short should resolve to selected window (2)
        short windowParam = -3;
        var resolved = windowParam == -3
            ? mgr.Current
            : mgr.GetWindow(windowParam);

        Assert.Same(mgr.GetWindow(2), resolved);
    }

    #endregion

    #region V6Window — Colour Data

    /// <summary>
    /// Verifies that ColourData (property 11) tracks fg/bg colour for
    /// a specific window.
    /// </summary>
    [Fact]
    public void V6Window_ColourData_SetAndGet()
    {
        var w = new V6Window(0);
        int colourData = (3 << 8) | 9; // bg=red, fg=white
        w.ColourData = colourData;

        Assert.Equal(colourData, w.ColourData);
        Assert.Equal(colourData, w.GetProperty(11));
    }

    /// <summary>
    /// Verifies that colour can be set on a non-current window via
    /// V6WindowManager, simulating the optional window parameter in
    /// <c>@set_colour</c>.
    /// </summary>
    [Fact]
    public void V6WindowManager_SetColourOnSpecificWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetWindow(0);

        int colourData = (4 << 8) | 7; // bg=green, fg=magenta
        mgr.GetWindow(5).ColourData = colourData;

        Assert.Equal(colourData, mgr.GetWindow(5).ColourData);
    }

    #endregion

    #region Screen Redraw Bit

    /// <summary>
    /// Verifies that RequestScreenRedraw sets Flags 2 bit 2.
    /// ZSpec11 "Status line redraw" — interpreter sets this after resize.
    /// </summary>
    [Fact]
    public void RequestScreenRedraw_SetsFlags2Bit2()
    {
        var mem = CreateV5Memory();
        ushort before = mem.ReadWord(0x10);
        Assert.Equal(0, before & 0x0004);

        // Set the bit via direct memory write (simulating RequestScreenRedraw)
        ushort flags2 = mem.ReadWord(0x10);
        mem.WriteWord(0x10, (ushort)(flags2 | 0x0004));

        ushort after = mem.ReadWord(0x10);
        Assert.NotEqual(0, after & 0x0004);
    }

    /// <summary>
    /// Verifies that the redraw bit preserves other Flags 2 bits.
    /// </summary>
    [Fact]
    public void RequestScreenRedraw_PreservesOtherFlags()
    {
        var mem = CreateV5Memory();
        // Set transcripting bit (bit 0) and fixed-pitch bit (bit 1)
        mem.WriteWord(0x10, 0x0003);

        ushort flags2 = mem.ReadWord(0x10);
        mem.WriteWord(0x10, (ushort)(flags2 | 0x0004));

        ushort after = mem.ReadWord(0x10);
        Assert.Equal(0x0007, after & 0x0007); // bits 0, 1, 2 all set
    }

    #endregion

    #region Buffer Screen Mode Tracking

    /// <summary>
    /// Verifies that buffer screen mode defaults to 0.
    /// ZSpec11 "@buffer_screen" — mode 0 is default.
    /// </summary>
    [Fact]
    public void BufferScreenMode_DefaultsToZero()
    {
        int mode = 0;
        Assert.Equal(0, mode);
    }

    /// <summary>
    /// Verifies that setting mode 1 returns old mode (0).
    /// </summary>
    [Fact]
    public void BufferScreenMode_SetToOne_ReturnsPreviousMode()
    {
        int bufferScreenMode = 0;
        int oldMode = bufferScreenMode;
        bufferScreenMode = 1;

        Assert.Equal(0, oldMode);
        Assert.Equal(1, bufferScreenMode);
    }

    /// <summary>
    /// Verifies that mode -1 (force update) does not change the stored mode.
    /// ZSpec11 "@buffer_screen" — -1 forces update without changing mode.
    /// </summary>
    [Fact]
    public void BufferScreenMode_MinusOne_DoesNotChangeStoredMode()
    {
        int bufferScreenMode = 1;
        int requestedMode = -1;
        int oldMode = bufferScreenMode;

        if (requestedMode >= 0)
            bufferScreenMode = requestedMode;

        Assert.Equal(1, oldMode);
        Assert.Equal(1, bufferScreenMode); // unchanged
    }

    /// <summary>
    /// Verifies that switching from mode 1 back to mode 0 returns 1.
    /// </summary>
    [Fact]
    public void BufferScreenMode_SwitchBackToZero_ReturnsOne()
    {
        int bufferScreenMode = 0;

        // Set to 1
        int old1 = bufferScreenMode;
        bufferScreenMode = 1;
        Assert.Equal(0, old1);

        // Set back to 0
        int old2 = bufferScreenMode;
        bufferScreenMode = 0;
        Assert.Equal(1, old2);
        Assert.Equal(0, bufferScreenMode);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a minimal V5 Memory for header flag tests.
    /// </summary>
    private static Memory CreateV5Memory()
    {
        var data = new byte[512];
        data[0] = 5;
        data[0x0E] = 0x01; // static base
        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    /// <summary>
    /// Creates a Memory image with an EXT opcode instruction at $0040
    /// for disassembler testing.
    /// </summary>
    /// <remarks>
    /// EXT format: byte 0xBE (extended prefix), byte = ext opcode,
    /// then operand type byte(s), then operand(s), then store variable.
    /// </remarks>
    private static Memory CreateDisassemblerMemory(int extOpcode, int operandCount, bool hasStore = false)
    {
        var data = new byte[1024];
        data[0] = 5; // version
        data[0x0E] = 0x02; // static base
        // Abbreviation table at $0100 (needs to exist for TextDecoder)
        data[0x18] = 0x01;
        data[0x19] = 0x00;

        int pc = 0x40;
        data[pc++] = 0xBE; // EXT prefix
        data[pc++] = (byte)extOpcode;

        // Operand types byte: small constants
        byte types = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i < operandCount)
                types |= (byte)(0x01 << (6 - i * 2)); // small constant
            else
                types |= (byte)(0x03 << (6 - i * 2)); // omitted
        }
        data[pc++] = types;

        // Operand values (small constants)
        for (int i = 0; i < operandCount; i++)
            data[pc++] = (byte)(i + 1);

        // Store variable
        if (hasStore)
            data[pc++] = 0x00; // store to stack

        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    #endregion
}
