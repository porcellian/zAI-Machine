using ZMachine.Core;

namespace ZMachine.Tests;

/// <summary>
/// Tests for V6Window and V6WindowManager covering the 18-property
/// window model and all V6 window opcodes.
/// ZSpec S8.8, ZSpec11 "Version 6 windows".
/// </summary>
public class V6WindowTests
{
    #region V6Window — Property Access

    /// <summary>
    /// Verifies that GetProperty returns the correct value for each
    /// of the 18 property indices (0–17). ZSpec S8.8 property table.
    /// </summary>
    [Fact]
    public void GetProperty_ReturnsCorrectValueForAllIndices()
    {
        var w = new V6Window(0);
        w.Y = 10; w.X = 20; w.Height = 100; w.Width = 200;
        w.CursorY = 3; w.CursorX = 5;
        w.LeftMargin = 4; w.RightMargin = 6;
        w.NewlineInterrupt = 0x1234; w.InterruptCountdown = 7;
        w.TextStyle = 2; w.ColourData = 0x0209;
        w.Font = 3; w.FontSize = 0x0C08;
        w.Attributes = 0x0B; w.LineCount = 15;
        w.TrueForeground = 0x7FFF; w.TrueBackground = 0x0000;

        Assert.Equal(10, w.GetProperty(0));
        Assert.Equal(20, w.GetProperty(1));
        Assert.Equal(100, w.GetProperty(2));
        Assert.Equal(200, w.GetProperty(3));
        Assert.Equal(3, w.GetProperty(4));
        Assert.Equal(5, w.GetProperty(5));
        Assert.Equal(4, w.GetProperty(6));
        Assert.Equal(6, w.GetProperty(7));
        Assert.Equal(0x1234, w.GetProperty(8));
        Assert.Equal(7, w.GetProperty(9));
        Assert.Equal(2, w.GetProperty(10));
        Assert.Equal(0x0209, w.GetProperty(11));
        Assert.Equal(3, w.GetProperty(12));
        Assert.Equal(0x0C08, w.GetProperty(13));
        Assert.Equal(0x0B, w.GetProperty(14));
        Assert.Equal(15, w.GetProperty(15));
        Assert.Equal(0x7FFF, w.GetProperty(16));
        Assert.Equal(0x0000, w.GetProperty(17));
    }

    /// <summary>
    /// Verifies that GetProperty returns 0 for out-of-range indices.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(18)]
    [InlineData(100)]
    public void GetProperty_OutOfRange_ReturnsZero(int index)
    {
        var w = new V6Window(0);
        Assert.Equal(0, w.GetProperty(index));
    }

    /// <summary>
    /// Verifies that SetProperty writes to indices 0–15 and returns true.
    /// </summary>
    [Theory]
    [InlineData(0, 42)]
    [InlineData(5, 99)]
    [InlineData(14, 0x0F)]
    [InlineData(15, 25)]
    public void SetProperty_WritableIndex_ReturnsTrueAndSetsValue(int index, int value)
    {
        var w = new V6Window(0);
        bool result = w.SetProperty(index, value);

        Assert.True(result);
        Assert.Equal(value, w.GetProperty(index));
    }

    /// <summary>
    /// Verifies that SetProperty returns false for read-only properties 16 and 17
    /// (true colours). ZSpec11 "@get_wind_prop".
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    public void SetProperty_ReadOnlyIndex_ReturnsFalse(int index)
    {
        var w = new V6Window(0);
        w.TrueForeground = 0x1111;
        w.TrueBackground = 0x2222;

        bool result = w.SetProperty(index, 0x9999);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that the window number is stored and accessible.
    /// </summary>
    [Fact]
    public void Number_ReturnsConstructorValue()
    {
        var w = new V6Window(5);
        Assert.Equal(5, w.Number);
    }

    /// <summary>
    /// Verifies that cursor positions default to (1, 1).
    /// ZSpec S8.8 — cursor is 1-based within the window.
    /// </summary>
    [Fact]
    public void CursorDefaults_AreOneOne()
    {
        var w = new V6Window(0);
        Assert.Equal(1, w.CursorY);
        Assert.Equal(1, w.CursorX);
    }

    /// <summary>
    /// Verifies that font defaults to 1 (normal font).
    /// </summary>
    [Fact]
    public void Font_DefaultsToOne()
    {
        var w = new V6Window(0);
        Assert.Equal(1, w.Font);
    }

    #endregion

    #region V6Window — Attribute Convenience Properties

    /// <summary>
    /// Verifies that Wrapping gets/sets bit 0 of Attributes.
    /// ZSpec S8.8 — attribute bit 0.
    /// </summary>
    [Fact]
    public void Wrapping_GetSet_ManipulatesBitZero()
    {
        var w = new V6Window(0);
        Assert.False(w.Wrapping);

        w.Wrapping = true;
        Assert.True(w.Wrapping);
        Assert.Equal(0x01, w.Attributes & 0x01);

        w.Wrapping = false;
        Assert.False(w.Wrapping);
        Assert.Equal(0x00, w.Attributes & 0x01);
    }

    /// <summary>
    /// Verifies that Scrolling gets/sets bit 1 of Attributes.
    /// ZSpec S8.8 — attribute bit 1.
    /// </summary>
    [Fact]
    public void Scrolling_GetSet_ManipulatesBitOne()
    {
        var w = new V6Window(0);
        w.Scrolling = true;
        Assert.True(w.Scrolling);
        Assert.Equal(0x02, w.Attributes & 0x02);
    }

    /// <summary>
    /// Verifies that Transcript gets/sets bit 2 of Attributes.
    /// ZSpec S8.8 — attribute bit 2.
    /// </summary>
    [Fact]
    public void Transcript_GetSet_ManipulatesBitTwo()
    {
        var w = new V6Window(0);
        w.Transcript = true;
        Assert.True(w.Transcript);
        Assert.Equal(0x04, w.Attributes & 0x04);
    }

    /// <summary>
    /// Verifies that Buffered gets/sets bit 3 of Attributes.
    /// ZSpec S8.8 — attribute bit 3.
    /// </summary>
    [Fact]
    public void Buffered_GetSet_ManipulatesBitThree()
    {
        var w = new V6Window(0);
        w.Buffered = true;
        Assert.True(w.Buffered);
        Assert.Equal(0x08, w.Attributes & 0x08);
    }

    /// <summary>
    /// Verifies that setting one attribute does not disturb others.
    /// </summary>
    [Fact]
    public void Attributes_SetMultiple_PreservesOtherBits()
    {
        var w = new V6Window(0);
        w.Wrapping = true;
        w.Buffered = true;
        Assert.True(w.Wrapping);
        Assert.True(w.Buffered);
        Assert.False(w.Scrolling);
        Assert.False(w.Transcript);

        w.Scrolling = true;
        Assert.True(w.Wrapping);
        Assert.True(w.Scrolling);
        Assert.True(w.Buffered);
    }

    #endregion

    #region V6WindowManager — Initialization

    /// <summary>
    /// Verifies that the manager creates exactly 8 windows.
    /// ZSpec S8.8 — 8 independent windows.
    /// </summary>
    [Fact]
    public void Constructor_CreatesEightWindows()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        Assert.Equal(8, mgr.Windows.Count);
    }

    /// <summary>
    /// Verifies that window 0 fills the screen with wrapping, scrolling,
    /// and buffered attributes set. ZSpec S8.8 defaults.
    /// </summary>
    [Fact]
    public void Constructor_Window0_FillsScreenWithAttributes()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        var w0 = mgr.GetWindow(0);

        Assert.Equal(1, w0.Y);
        Assert.Equal(1, w0.X);
        Assert.Equal(640, w0.Width);
        Assert.Equal(400, w0.Height);
        Assert.True(w0.Wrapping);
        Assert.True(w0.Scrolling);
        Assert.True(w0.Buffered);
    }

    /// <summary>
    /// Verifies that window 1 defaults to full width, zero height, at top.
    /// ZSpec S8.8 — window 1 is the status/upper window.
    /// </summary>
    [Fact]
    public void Constructor_Window1_FullWidthZeroHeight()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        var w1 = mgr.GetWindow(1);

        Assert.Equal(1, w1.Y);
        Assert.Equal(1, w1.X);
        Assert.Equal(640, w1.Width);
        Assert.Equal(0, w1.Height);
    }

    /// <summary>
    /// Verifies that windows 2–7 default to zero size at position (1,1).
    /// </summary>
    [Fact]
    public void Constructor_Windows2To7_ZeroSizeAtOrigin()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        for (int i = 2; i < 8; i++)
        {
            var w = mgr.GetWindow(i);
            Assert.Equal(1, w.Y);
            Assert.Equal(1, w.X);
            Assert.Equal(0, w.Width);
            Assert.Equal(0, w.Height);
        }
    }

    /// <summary>
    /// Verifies that all windows get the correct font size encoding.
    /// FontSize = (height << 8) | width.
    /// </summary>
    [Fact]
    public void Constructor_AllWindows_FontSizeSet()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        int expected = (12 << 8) | 8;
        for (int i = 0; i < 8; i++)
            Assert.Equal(expected, mgr.GetWindow(i).FontSize);
    }

    /// <summary>
    /// Verifies that all windows get the default colour data
    /// (foreground=white/9, background=black/2). ZSpec11.
    /// </summary>
    [Fact]
    public void Constructor_AllWindows_DefaultColours()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        int expected = (2 << 8) | 9;
        for (int i = 0; i < 8; i++)
            Assert.Equal(expected, mgr.GetWindow(i).ColourData);
    }

    /// <summary>
    /// Verifies that the default selected window is 0.
    /// </summary>
    [Fact]
    public void Constructor_DefaultSelectedWindow_IsZero()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        Assert.Equal(0, mgr.SelectedWindow);
    }

    /// <summary>
    /// Verifies that the default mouse window is 1.
    /// ZSpec11 "Version 6 windows".
    /// </summary>
    [Fact]
    public void Constructor_DefaultMouseWindow_IsOne()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        Assert.Equal(1, mgr.MouseWindow);
    }

    #endregion

    #region V6WindowManager — SetWindow

    /// <summary>
    /// Verifies that SetWindow changes the selected window.
    /// VAR:11 @set_window for V6.
    /// </summary>
    [Fact]
    public void SetWindow_ValidWindow_ChangesSelection()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetWindow(3);
        Assert.Equal(3, mgr.SelectedWindow);
        Assert.Equal(3, mgr.Current.Number);
    }

    /// <summary>
    /// Verifies that SetWindow with an out-of-range value does not change selection.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void SetWindow_OutOfRange_NoChange(int window)
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetWindow(2);
        mgr.SetWindow(window);
        Assert.Equal(2, mgr.SelectedWindow);
    }

    #endregion

    #region V6WindowManager — MoveWindow

    /// <summary>
    /// Verifies that MoveWindow sets the window's Y and X position.
    /// EXT:16 @move_window.
    /// </summary>
    [Fact]
    public void MoveWindow_SetsPosition()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.MoveWindow(2, 50, 100);

        Assert.Equal(50, mgr.GetWindow(2).Y);
        Assert.Equal(100, mgr.GetWindow(2).X);
    }

    #endregion

    #region V6WindowManager — WindowSize

    /// <summary>
    /// Verifies that WindowSize sets the window's height and width.
    /// EXT:17 @window_size.
    /// </summary>
    [Fact]
    public void WindowSize_SetsDimensions()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.WindowSize(3, 200, 320);

        Assert.Equal(200, mgr.GetWindow(3).Height);
        Assert.Equal(320, mgr.GetWindow(3).Width);
    }

    #endregion

    #region V6WindowManager — WindowStyle

    /// <summary>
    /// Verifies that WindowStyle with operation 0 replaces all attribute bits.
    /// EXT:18 @window_style, operation 0 = set.
    /// </summary>
    [Fact]
    public void WindowStyle_Set_ReplacesAttributes()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.WindowStyle(0, 0x0F, 0); // set all 4 bits
        Assert.Equal(0x0F, mgr.GetWindow(0).Attributes);

        mgr.WindowStyle(0, 0x02, 0); // replace with just scrolling
        Assert.Equal(0x02, mgr.GetWindow(0).Attributes);
    }

    /// <summary>
    /// Verifies that WindowStyle with operation 1 ORs bits into attributes.
    /// EXT:18 @window_style, operation 1 = set bits.
    /// </summary>
    [Fact]
    public void WindowStyle_SetBits_ORsIntoAttributes()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.WindowStyle(2, 0x01, 0); // start with wrapping
        mgr.WindowStyle(2, 0x08, 1); // OR in buffered

        Assert.Equal(0x09, mgr.GetWindow(2).Attributes);
    }

    /// <summary>
    /// Verifies that WindowStyle with operation 2 clears specified bits.
    /// EXT:18 @window_style, operation 2 = clear bits.
    /// </summary>
    [Fact]
    public void WindowStyle_ClearBits_ANDsOutOfAttributes()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.WindowStyle(0, 0x0F, 0); // all bits set
        mgr.WindowStyle(0, 0x05, 2); // clear wrapping + transcript

        Assert.Equal(0x0A, mgr.GetWindow(0).Attributes);
    }

    #endregion

    #region V6WindowManager — GetWindProp / PutWindProp

    /// <summary>
    /// Verifies that GetWindProp reads a window property by index.
    /// EXT:19 @get_wind_prop.
    /// </summary>
    [Fact]
    public void GetWindProp_ReadsProperty()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.MoveWindow(1, 42, 84);

        Assert.Equal(42, mgr.GetWindProp(1, 0)); // Y
        Assert.Equal(84, mgr.GetWindProp(1, 1)); // X
    }

    /// <summary>
    /// Verifies that PutWindProp writes a writable window property.
    /// EXT:20 @put_wind_prop.
    /// </summary>
    [Fact]
    public void PutWindProp_WritesProperty()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.PutWindProp(3, 6, 16); // left margin = 16

        Assert.Equal(16, mgr.GetWindow(3).LeftMargin);
        Assert.Equal(16, mgr.GetWindProp(3, 6));
    }

    /// <summary>
    /// Verifies that PutWindProp silently ignores read-only properties 16–17.
    /// ZSpec11 "@get_wind_prop" — true colour properties are read-only.
    /// </summary>
    [Fact]
    public void PutWindProp_ReadOnlyProperty_Ignored()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.PutWindProp(0, 16, 0x7FFF);
        Assert.Equal(0, mgr.GetWindProp(0, 16));
    }

    #endregion

    #region V6WindowManager — SetMargins

    /// <summary>
    /// Verifies that SetMargins sets left and right margins on the specified window.
    /// EXT:8 @set_margins.
    /// </summary>
    [Fact]
    public void SetMargins_SetsLeftAndRight()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetMargins(10, 20, 0);

        Assert.Equal(10, mgr.GetWindow(0).LeftMargin);
        Assert.Equal(20, mgr.GetWindow(0).RightMargin);
    }

    #endregion

    #region V6WindowManager — SetMouseWindow

    /// <summary>
    /// Verifies that SetMouseWindow changes the mouse window.
    /// EXT:22 @mouse_window.
    /// </summary>
    [Fact]
    public void SetMouseWindow_ChangesMouseWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetMouseWindow(3);
        Assert.Equal(3, mgr.MouseWindow);
    }

    /// <summary>
    /// Verifies that SetMouseWindow accepts -1 for "any window".
    /// EXT:22 — -1 means mouse clicks accepted in any window.
    /// </summary>
    [Fact]
    public void SetMouseWindow_NegativeOne_AcceptsAnyWindow()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SetMouseWindow(-1);
        Assert.Equal(-1, mgr.MouseWindow);
    }

    #endregion

    #region V6WindowManager — SplitWindow

    /// <summary>
    /// Verifies that SplitWindow resizes windows 0 and 1 correctly.
    /// VAR:10 @split_window V6 behaviour: window 1 gets the top portion,
    /// window 0 fills the remainder. ZSpec11 "@split_window".
    /// </summary>
    [Fact]
    public void SplitWindow_ResizesWindowsZeroAndOne()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SplitWindow(5, 12); // 5 lines × 12px = 60px

        var w1 = mgr.GetWindow(1);
        Assert.Equal(1, w1.Y);
        Assert.Equal(1, w1.X);
        Assert.Equal(640, w1.Width);
        Assert.Equal(60, w1.Height);

        var w0 = mgr.GetWindow(0);
        Assert.Equal(61, w0.Y); // 1 + 60
        Assert.Equal(1, w0.X);
        Assert.Equal(640, w0.Width);
        Assert.Equal(340, w0.Height); // 400 - 60
    }

    /// <summary>
    /// Verifies that SplitWindow with 0 lines gives all space to window 0.
    /// </summary>
    [Fact]
    public void SplitWindow_ZeroLines_Window0FillsScreen()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.SplitWindow(5, 12); // split first
        mgr.SplitWindow(0, 12); // then unsplit

        var w1 = mgr.GetWindow(1);
        Assert.Equal(0, w1.Height);

        var w0 = mgr.GetWindow(0);
        Assert.Equal(1, w0.Y);
        Assert.Equal(400, w0.Height);
    }

    /// <summary>
    /// Verifies that SplitWindow does not affect windows 2–7.
    /// </summary>
    [Fact]
    public void SplitWindow_DoesNotAffectOtherWindows()
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        mgr.MoveWindow(2, 100, 100);
        mgr.WindowSize(2, 50, 50);

        mgr.SplitWindow(5, 12);

        Assert.Equal(100, mgr.GetWindow(2).Y);
        Assert.Equal(100, mgr.GetWindow(2).X);
        Assert.Equal(50, mgr.GetWindow(2).Height);
        Assert.Equal(50, mgr.GetWindow(2).Width);
    }

    #endregion

    #region V6WindowManager — GetWindow Bounds

    /// <summary>
    /// Verifies that GetWindow clamps out-of-range numbers to window 0.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(100)]
    public void GetWindow_OutOfRange_ReturnsWindowZero(int window)
    {
        var mgr = new V6WindowManager(640, 400, 8, 12);
        var w = mgr.GetWindow(window);
        Assert.Equal(0, w.Number);
    }

    #endregion
}
