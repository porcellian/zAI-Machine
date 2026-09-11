using ZMachine.Core;

namespace ZMachine.Tests;

/// <summary>
/// Tests for MouseState covering position tracking, button state,
/// array serialization (<c>@read_mouse</c>), header coordinate writes
/// during input, and ZSCII click codes.
/// </summary>
/// <remarks>
/// ZSpec11 "@read_mouse", "Mouse clicks", "Mouse co-ordinates".
/// </remarks>
public class MouseTests
{
    /// <summary>Creates a minimal Memory for mouse tests.</summary>
    private static Memory CreateMemory()
    {
        var data = new byte[512];
        data[0] = 5;
        data[0x0E] = 0x01; // static base
        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    /// <summary>
    /// Creates a Memory with a header extension table at $0100 that has
    /// at least 2 words for mouse coordinates.
    /// </summary>
    private static Memory CreateMemoryWithExtension()
    {
        var data = new byte[1024];
        data[0] = 5;
        data[0x0E] = 0x02; // static base at $0200

        // Header extension table at $0100
        int extAddr = 0x0100;
        data[0x36] = (byte)(extAddr >> 8);
        data[0x37] = (byte)(extAddr & 0xFF);
        // Word 0: number of further words = 6
        data[extAddr] = 0x00;
        data[extAddr + 1] = 0x06;

        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    #region MouseState — Position and Buttons

    /// <summary>
    /// Verifies that the default mouse position is (1, 1).
    /// ZSpec11 "Mouse co-ordinates" — 1-based from top-left.
    /// </summary>
    [Fact]
    public void DefaultPosition_IsOneOne()
    {
        var mouse = new MouseState();
        Assert.Equal(1, mouse.Y);
        Assert.Equal(1, mouse.X);
    }

    /// <summary>
    /// Verifies that SetPosition updates Y and X.
    /// </summary>
    [Fact]
    public void SetPosition_UpdatesCoordinates()
    {
        var mouse = new MouseState();
        mouse.SetPosition(42, 100);

        Assert.Equal(42, mouse.Y);
        Assert.Equal(100, mouse.X);
    }

    /// <summary>
    /// Verifies that the default button state is 0 (no buttons pressed).
    /// </summary>
    [Fact]
    public void DefaultButtons_IsZero()
    {
        var mouse = new MouseState();
        Assert.Equal(0, mouse.Buttons);
    }

    /// <summary>
    /// Verifies that SetButtons updates the button bitfield.
    /// </summary>
    [Fact]
    public void SetButtons_UpdatesState()
    {
        var mouse = new MouseState();
        mouse.SetButtons(0x03); // primary + secondary

        Assert.Equal(0x03, mouse.Buttons);
    }

    /// <summary>
    /// Verifies that menu defaults to 0.
    /// </summary>
    [Fact]
    public void DefaultMenu_IsZero()
    {
        var mouse = new MouseState();
        Assert.Equal(0, mouse.Menu);
    }

    #endregion

    #region MouseState — WriteToArray (@read_mouse)

    /// <summary>
    /// Verifies that WriteToArray writes y, x, buttons, and menu as
    /// four consecutive words at the given address.
    /// ZSpec11 "@read_mouse" — array: word 0=y, 1=x, 2=buttons, 3=menu.
    /// </summary>
    [Fact]
    public void WriteToArray_WritesAllFourWords()
    {
        var mem = CreateMemory();
        var mouse = new MouseState();
        mouse.SetPosition(50, 120);
        mouse.SetButtons(0x01);
        mouse.Menu = 0x0203;

        int addr = 0x80;
        mouse.WriteToArray(mem, addr);

        Assert.Equal(50, mem.ReadWord(addr));       // y
        Assert.Equal(120, mem.ReadWord(addr + 2));   // x
        Assert.Equal(0x01, mem.ReadWord(addr + 4));  // buttons
        Assert.Equal(0x0203, mem.ReadWord(addr + 6)); // menu
    }

    /// <summary>
    /// Verifies that WriteToArray with default state writes all zeros
    /// except position (1, 1).
    /// </summary>
    [Fact]
    public void WriteToArray_DefaultState_WritesDefaultValues()
    {
        var mem = CreateMemory();
        var mouse = new MouseState();

        int addr = 0x80;
        mouse.WriteToArray(mem, addr);

        Assert.Equal(1, mem.ReadWord(addr));       // y = 1
        Assert.Equal(1, mem.ReadWord(addr + 2));   // x = 1
        Assert.Equal(0, mem.ReadWord(addr + 4));   // buttons = 0
        Assert.Equal(0, mem.ReadWord(addr + 6));   // menu = 0
    }

    #endregion

    #region MouseState — WriteClickToHeader

    /// <summary>
    /// Verifies that WriteClickToHeader writes the mouse position to the
    /// header extension table: word 1 = X, word 2 = Y.
    /// ZSpec S11 — extension table layout; ZSpec11 "Mouse co-ordinates".
    /// </summary>
    [Fact]
    public void WriteClickToHeader_WritesToExtensionTable()
    {
        var mem = CreateMemoryWithExtension();
        var mouse = new MouseState();
        mouse.SetPosition(30, 75);

        mouse.WriteClickToHeader(mem);

        int extAddr = mem.ReadWord(0x36);
        Assert.Equal(75, mem.ReadWord(extAddr + 2));  // word 1 = X
        Assert.Equal(30, mem.ReadWord(extAddr + 4));  // word 2 = Y
    }

    /// <summary>
    /// Verifies that WriteClickToHeader does not clobber base header
    /// fields at $24 (screen height) or $26 (font size).
    /// </summary>
    [Fact]
    public void WriteClickToHeader_DoesNotClobberBaseHeader()
    {
        var mem = CreateMemoryWithExtension();
        // Set known values at $24 and $26
        mem.WriteWord(0x24, 0xABCD);
        mem.WriteByte(0x26, 0x08); // font width
        mem.WriteByte(0x27, 0x0C); // font height

        var mouse = new MouseState();
        mouse.SetPosition(50, 120);
        mouse.WriteClickToHeader(mem);

        Assert.Equal(0xABCD, mem.ReadWord(0x24));
        Assert.Equal(0x08, mem.ReadByte(0x26));
        Assert.Equal(0x0C, mem.ReadByte(0x27));
    }

    /// <summary>
    /// Verifies that WriteClickToHeader is a no-op when no header
    /// extension table exists (word at $36 = 0).
    /// </summary>
    [Fact]
    public void WriteClickToHeader_NoExtensionTable_NoOp()
    {
        var mem = CreateMemory(); // no extension table
        var mouse = new MouseState();
        mouse.SetPosition(50, 120);

        // Should not throw or write anywhere dangerous
        mouse.WriteClickToHeader(mem);

        // Base header fields untouched
        Assert.Equal(0, mem.ReadWord(0x24));
        Assert.Equal(0, mem.ReadByte(0x26));
    }

    #endregion

    #region MouseState — GetClickZscii

    /// <summary>
    /// Verifies that V5 always returns ZSCII 254 for all clicks.
    /// ZSpec11 "Mouse clicks" — V5: all clicks → 254.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetClickZscii_V5_Always254(bool isDouble)
    {
        Assert.Equal(254, MouseState.GetClickZscii(5, isDouble));
    }

    /// <summary>
    /// Verifies that V6 returns 254 for single/first click.
    /// ZSpec11 "Mouse clicks" — V6: single/first → 254.
    /// </summary>
    [Fact]
    public void GetClickZscii_V6_SingleClick_Returns254()
    {
        Assert.Equal(254, MouseState.GetClickZscii(6, false));
    }

    /// <summary>
    /// Verifies that V6 returns 253 for the second click of a double-click.
    /// ZSpec11 "Mouse clicks" — V6: second of double → 253.
    /// </summary>
    [Fact]
    public void GetClickZscii_V6_DoubleClickSecond_Returns253()
    {
        Assert.Equal(253, MouseState.GetClickZscii(6, true));
    }

    /// <summary>
    /// Verifies that non-V6 versions return 254 even for double-click flag.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void GetClickZscii_NonV6_Always254(int version)
    {
        Assert.Equal(254, MouseState.GetClickZscii(version, true));
    }

    #endregion

    #region MouseState — Position Update

    /// <summary>
    /// Verifies that position can be set to negative values (outside screen).
    /// ZSpec11 "@read_mouse" — interpreters may report negative values
    /// if the pointer is outside the screen.
    /// </summary>
    [Fact]
    public void SetPosition_NegativeValues_Allowed()
    {
        var mouse = new MouseState();
        mouse.SetPosition(-5, -10);

        Assert.Equal(-5, mouse.Y);
        Assert.Equal(-10, mouse.X);
    }

    /// <summary>
    /// Verifies that position updates do not affect button state.
    /// </summary>
    [Fact]
    public void SetPosition_DoesNotAffectButtons()
    {
        var mouse = new MouseState();
        mouse.SetButtons(0x07);
        mouse.SetPosition(100, 200);

        Assert.Equal(0x07, mouse.Buttons);
    }

    /// <summary>
    /// Verifies that button updates do not affect position.
    /// </summary>
    [Fact]
    public void SetButtons_DoesNotAffectPosition()
    {
        var mouse = new MouseState();
        mouse.SetPosition(42, 84);
        mouse.SetButtons(0x03);

        Assert.Equal(42, mouse.Y);
        Assert.Equal(84, mouse.X);
    }

    #endregion

    #region MouseState — Multiple Button Bits

    /// <summary>
    /// Verifies that individual button bits can be read.
    /// ZSpec11 "@read_mouse" — bit 0=primary, bit 1=secondary, etc.
    /// </summary>
    [Fact]
    public void Buttons_IndividualBits_Readable()
    {
        var mouse = new MouseState();
        mouse.SetButtons(0x05); // primary + tertiary

        Assert.NotEqual(0, mouse.Buttons & 0x01); // primary
        Assert.Equal(0, mouse.Buttons & 0x02);     // secondary not pressed
        Assert.NotEqual(0, mouse.Buttons & 0x04); // tertiary
    }

    #endregion

    #region Mouse Window Filtering

    /// <summary>
    /// Verifies that ShouldDeliverClick returns true when no window
    /// manager exists (V5 — no mouse window concept).
    /// </summary>
    [Fact]
    public void ShouldDeliverClick_NoWindowManager_AlwaysTrue()
    {
        var mouse = new MouseState();
        mouse.SetPosition(100, 200);

        Assert.True(mouse.ShouldDeliverClick(null));
    }

    /// <summary>
    /// Verifies that clicks inside the designated mouse window are delivered.
    /// ZSpec11 "Mouse clicks" — clicks within the mouse window are accepted.
    /// </summary>
    [Fact]
    public void ShouldDeliverClick_InsideMouseWindow_True()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        // Window 1 defaults to Y=1, X=1, Width=640, Height=0
        // Resize it to have actual area
        mgr.WindowSize(1, 100, 640);

        var mouse = new MouseState();
        mouse.SetPosition(50, 300); // inside window 1

        Assert.True(mouse.ShouldDeliverClick(mgr));
    }

    /// <summary>
    /// Verifies that clicks outside the designated mouse window are suppressed.
    /// ZSpec11 "Mouse clicks" — clicks outside are ignored for input.
    /// </summary>
    [Fact]
    public void ShouldDeliverClick_OutsideMouseWindow_False()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.WindowSize(1, 100, 640);
        // Mouse window defaults to 1 (Y=1, X=1, H=100, W=640)

        var mouse = new MouseState();
        mouse.SetPosition(200, 300); // below window 1

        Assert.False(mouse.ShouldDeliverClick(mgr));
    }

    /// <summary>
    /// Verifies that mouse window -1 accepts clicks anywhere.
    /// EXT:22 — -1 means any window.
    /// </summary>
    [Fact]
    public void ShouldDeliverClick_MouseWindowMinusOne_AlwaysTrue()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetMouseWindow(-1);

        var mouse = new MouseState();
        mouse.SetPosition(999, 999); // way outside any window

        Assert.True(mouse.ShouldDeliverClick(mgr));
    }

    /// <summary>
    /// Verifies that IsClickInMouseWindow checks the correct window
    /// after SetMouseWindow changes the target.
    /// </summary>
    [Fact]
    public void IsClickInMouseWindow_AfterChange_ChecksNewWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        // Set up window 3 at a specific location
        mgr.MoveWindow(3, 100, 100);
        mgr.WindowSize(3, 50, 50);
        mgr.SetMouseWindow(3);

        // Inside window 3
        Assert.True(mgr.IsClickInMouseWindow(120, 120));
        // Outside window 3
        Assert.False(mgr.IsClickInMouseWindow(50, 50));
    }

    /// <summary>
    /// Verifies that a click exactly at the window boundary (top-left corner)
    /// is considered inside.
    /// </summary>
    [Fact]
    public void IsClickInMouseWindow_AtTopLeftBoundary_Inside()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.MoveWindow(2, 50, 100);
        mgr.WindowSize(2, 80, 200);
        mgr.SetMouseWindow(2);

        Assert.True(mgr.IsClickInMouseWindow(50, 100));
    }

    /// <summary>
    /// Verifies that a click at the bottom-right boundary (exclusive) is
    /// considered outside — window extends from (Y, X) to (Y+H-1, X+W-1).
    /// </summary>
    [Fact]
    public void IsClickInMouseWindow_AtBottomRightBoundary_Outside()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.MoveWindow(2, 50, 100);
        mgr.WindowSize(2, 80, 200);
        mgr.SetMouseWindow(2);

        // Y+H = 130, X+W = 300 — these are just past the edge
        Assert.False(mgr.IsClickInMouseWindow(130, 100));
        Assert.False(mgr.IsClickInMouseWindow(50, 300));
    }

    #endregion
}
