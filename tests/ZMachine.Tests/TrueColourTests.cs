using ZMachine.Core;

namespace ZMachine.Tests;

/// <summary>
/// Tests for TrueColourManager covering 15-bit sRGB colour handling,
/// standard-to-true-colour equivalences, non-standard colour tracking,
/// magic values, and V6 transparency.
/// </summary>
/// <remarks>
/// ZSpec11 "@set_true_colour", "Colour numbers", "@set_colour".
/// </remarks>
public class TrueColourTests
{
    /// <summary>Creates a minimal Memory for TrueColourManager tests.</summary>
    private static Memory CreateMemory(int version = 5)
    {
        var data = new byte[512];
        data[0] = (byte)version;
        // Static memory base at 0x100
        data[0x0E] = 0x01;
        var mem = new Memory();
        mem.LoadStory(data);
        return mem;
    }

    /// <summary>
    /// Creates a Memory with a header extension table containing
    /// true default colours and Flags 3.
    /// </summary>
    private static (Memory mem, HeaderExtension ext) CreateMemoryWithExtension(
        int version = 5, ushort trueFg = 0, ushort trueBg = 0, ushort flags3 = 0)
    {
        var data = new byte[1024];
        data[0] = (byte)version;
        data[0x0E] = 0x01;

        // Header extension table at 0x200
        int extAddr = 0x200;
        data[0x36] = (byte)(extAddr >> 8);
        data[0x37] = (byte)(extAddr & 0xFF);

        // Word 0: number of further words = 4
        data[extAddr] = 0x00;
        data[extAddr + 1] = 0x04;

        // Word 1: Unicode table address (0 = none)
        // Word 2: Flags 3
        data[extAddr + 4] = (byte)(flags3 >> 8);
        data[extAddr + 5] = (byte)(flags3 & 0xFF);

        // Word 3: true default foreground
        data[extAddr + 6] = (byte)(trueFg >> 8);
        data[extAddr + 7] = (byte)(trueFg & 0xFF);

        // Word 4: true default background
        data[extAddr + 8] = (byte)(trueBg >> 8);
        data[extAddr + 9] = (byte)(trueBg & 0xFF);

        var mem = new Memory();
        mem.LoadStory(data);
        var ext = new HeaderExtension(mem, extAddr);
        return (mem, ext);
    }

    #region Standard Colour Equivalences

    /// <summary>
    /// Verifies the 15-bit sRGB equivalences for all standard colours 2–12.
    /// ZSpec11 "Colour numbers" — gamma-adjusted Amiga colour set.
    /// </summary>
    [Theory]
    [InlineData(2, 0x0000)]   // black
    [InlineData(3, 0x001D)]   // red
    [InlineData(4, 0x0340)]   // green
    [InlineData(5, 0x03BD)]   // yellow
    [InlineData(6, 0x59A0)]   // blue
    [InlineData(7, 0x7C1F)]   // magenta
    [InlineData(8, 0x77A0)]   // cyan
    [InlineData(9, 0x7FFF)]   // white
    [InlineData(10, 0x5AD6)]  // light grey
    [InlineData(11, 0x4631)]  // medium grey
    [InlineData(12, 0x2D6B)]  // dark grey
    public void GetStandardTrueColour_ReturnsCorrectValue(int colourNumber, int expected)
    {
        Assert.Equal(expected, TrueColourManager.GetStandardTrueColour(colourNumber));
    }

    /// <summary>
    /// Verifies that out-of-range colour numbers return 0.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(15)]
    public void GetStandardTrueColour_OutOfRange_ReturnsZero(int colourNumber)
    {
        Assert.Equal(0, TrueColourManager.GetStandardTrueColour(colourNumber));
    }

    #endregion

    #region SetTrueColour — Basic

    /// <summary>
    /// Verifies that SetTrueColour with positive values sets the foreground
    /// and background true colours.
    /// </summary>
    [Fact]
    public void SetTrueColour_PositiveValues_SetsFgAndBg()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        mgr.SetTrueColour(0x001D, 0x0000); // red on black

        Assert.Equal(0x001D, mgr.TrueForeground);
        Assert.Equal(0x0000, mgr.TrueBackground);
    }

    /// <summary>
    /// Verifies that magic value -1 resets to default colour.
    /// ZSpec11 "@set_true_colour" — -1 = default setting.
    /// </summary>
    [Fact]
    public void SetTrueColour_MinusOne_ResetsToDefault()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        mgr.SetTrueColour(0x001D, 0x59A0);
        mgr.SetTrueColour(-1, -1);

        // Default: white fg, black bg
        Assert.Equal(0x7FFF, mgr.TrueForeground);
        Assert.Equal(0x0000, mgr.TrueBackground);
    }

    /// <summary>
    /// Verifies that magic value -2 keeps the current colour unchanged.
    /// ZSpec11 "@set_true_colour" — -2 = current setting.
    /// </summary>
    [Fact]
    public void SetTrueColour_MinusTwo_KeepsCurrent()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        mgr.SetTrueColour(0x001D, 0x59A0);
        mgr.SetTrueColour(-2, -2);

        Assert.Equal(0x001D, mgr.TrueForeground);
        Assert.Equal(0x59A0, mgr.TrueBackground);
    }

    /// <summary>
    /// Verifies that magic value -4 sets transparent background in V6.
    /// ZSpec11 "@set_true_colour" — -4 = transparent (V6 only).
    /// </summary>
    [Fact]
    public void SetTrueColour_MinusFour_TransparentBackground_V6()
    {
        var mem = CreateMemory(6);
        var mgr = new TrueColourManager(mem, 6, null);

        mgr.SetTrueColour(-2, -4);

        Assert.Equal(-4, mgr.TrueBackground);
        Assert.True(TrueColourManager.IsTransparent(mgr.TrueBackground));
    }

    /// <summary>
    /// Verifies that transparent foreground (-4) produces a diagnostic.
    /// ZSpec11 "@set_colour" — transparent is only valid as background.
    /// </summary>
    [Fact]
    public void SetTrueColour_TransparentForeground_ReturnsDiagnostic()
    {
        var mem = CreateMemory(6);
        var mgr = new TrueColourManager(mem, 6, null);

        string? diagnostic = mgr.SetTrueColour(-4, -2);

        Assert.NotNull(diagnostic);
        Assert.Contains("Transparent foreground", diagnostic);
    }

    /// <summary>
    /// Verifies that -4 background on non-V6 is ignored (not transparent).
    /// </summary>
    [Fact]
    public void SetTrueColour_MinusFour_NonV6_IgnoredForBackground()
    {
        var mem = CreateMemory(5);
        var mgr = new TrueColourManager(mem, 5, null);

        int originalBg = mgr.TrueBackground;
        mgr.SetTrueColour(-2, -4);

        Assert.Equal(originalBg, mgr.TrueBackground);
    }

    #endregion

    #region Default Colours from Header Extension

    /// <summary>
    /// Verifies that TrueColourManager reads default true colours
    /// from the header extension table.
    /// ZSpec11 "Header Extension" — words 5/6 = true default colours.
    /// </summary>
    [Fact]
    public void Constructor_HeaderExtensionDefaults_UsedAsDefaults()
    {
        var (mem, ext) = CreateMemoryWithExtension(trueFg: 0x001D, trueBg: 0x59A0);
        var mgr = new TrueColourManager(mem, 5, ext);

        Assert.Equal(0x001D, mgr.TrueForeground);
        Assert.Equal(0x59A0, mgr.TrueBackground);

        // Reset to defaults
        mgr.SetTrueColour(0x7FFF, 0x0000);
        mgr.SetTrueColour(-1, -1);

        Assert.Equal(0x001D, mgr.TrueForeground);
        Assert.Equal(0x59A0, mgr.TrueBackground);
    }

    /// <summary>
    /// Verifies that zero defaults in header extension fall back to
    /// white-on-black.
    /// </summary>
    [Fact]
    public void Constructor_ZeroHeaderDefaults_FallsBackToWhiteOnBlack()
    {
        var (mem, ext) = CreateMemoryWithExtension(trueFg: 0, trueBg: 0);
        var mgr = new TrueColourManager(mem, 5, ext);

        Assert.Equal(0x7FFF, mgr.TrueForeground);
        Assert.Equal(0x0000, mgr.TrueBackground);
    }

    /// <summary>
    /// Verifies that Flags 3 bit 0 is detected as transparency requested.
    /// ZSpec11 "Header Extension" — Flags 3 bit 0 = wants transparency.
    /// </summary>
    [Fact]
    public void Constructor_Flags3Bit0_TransparencyRequested()
    {
        var (mem, ext) = CreateMemoryWithExtension(version: 6, flags3: 0x0001);
        var mgr = new TrueColourManager(mem, 6, ext);

        Assert.True(mgr.TransparencyRequested);
    }

    /// <summary>
    /// Verifies that Flags 3 = 0 means transparency is not requested.
    /// </summary>
    [Fact]
    public void Constructor_Flags3Zero_NoTransparency()
    {
        var (mem, ext) = CreateMemoryWithExtension(version: 6, flags3: 0x0000);
        var mgr = new TrueColourManager(mem, 6, ext);

        Assert.False(mgr.TransparencyRequested);
    }

    #endregion

    #region Non-Standard Colour Tracking

    /// <summary>
    /// Verifies that a true colour matching a standard colour (2–12)
    /// returns that standard number. ZSpec11 "Colour numbers" rule 1.
    /// </summary>
    [Fact]
    public void TrueColourToNumber_StandardColour_ReturnsStandardNumber()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        Assert.Equal(3, mgr.TrueColourToNumber(0x001D)); // red
        Assert.Equal(9, mgr.TrueColourToNumber(0x7FFF)); // white
        Assert.Equal(2, mgr.TrueColourToNumber(0x0000)); // black
    }

    /// <summary>
    /// Verifies that a non-standard true colour gets assigned a number >= 16.
    /// ZSpec11 "Colour numbers" rule 2.
    /// </summary>
    [Fact]
    public void TrueColourToNumber_NonStandard_ReturnsAbove15()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        int num = mgr.TrueColourToNumber(0x1234);
        Assert.True(num >= 16 && num <= 255);
    }

    /// <summary>
    /// Verifies that the same non-standard colour always returns the same number.
    /// </summary>
    [Fact]
    public void TrueColourToNumber_SameColour_ReturnsSameNumber()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        int first = mgr.TrueColourToNumber(0x1234);
        int second = mgr.TrueColourToNumber(0x1234);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Verifies that different non-standard colours get different numbers.
    /// </summary>
    [Fact]
    public void TrueColourToNumber_DifferentColours_DifferentNumbers()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        int a = mgr.TrueColourToNumber(0x1234);
        int b = mgr.TrueColourToNumber(0x5678);

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies that after 240 distinct non-standard colours, slot reuse
    /// occurs (wrap-around). ZSpec11 "Colour numbers".
    /// </summary>
    [Fact]
    public void TrueColourToNumber_After240_WrapsAround()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        // Fill 240 slots with distinct colours (avoid standard colour values)
        for (int i = 0; i < 240; i++)
            mgr.TrueColourToNumber(0x4000 + i);

        // The 241st colour should reuse slot 0 (number 16)
        int num = mgr.TrueColourToNumber(0x3FFF);
        Assert.Equal(16, num);
    }

    /// <summary>
    /// Verifies that NumberToTrueColour reverses TrueColourToNumber for
    /// non-standard colours.
    /// </summary>
    [Fact]
    public void NumberToTrueColour_NonStandard_ReversesMapping()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        int num = mgr.TrueColourToNumber(0x1234);
        int result = mgr.NumberToTrueColour(num);

        Assert.Equal(0x1234, result);
    }

    /// <summary>
    /// Verifies that NumberToTrueColour returns standard values for 2–12.
    /// </summary>
    [Theory]
    [InlineData(2, 0x0000)]
    [InlineData(9, 0x7FFF)]
    public void NumberToTrueColour_StandardNumber_ReturnsEquivalent(int number, int expected)
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        Assert.Equal(expected, mgr.NumberToTrueColour(number));
    }

    /// <summary>
    /// Verifies that NumberToTrueColour returns -4 for colour 15 (transparent).
    /// </summary>
    [Fact]
    public void NumberToTrueColour_Colour15_ReturnsTransparent()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        Assert.Equal(-4, mgr.NumberToTrueColour(15));
    }

    /// <summary>
    /// Verifies that transparent true colour (-4) maps to colour number 15.
    /// </summary>
    [Fact]
    public void TrueColourToNumber_Transparent_Returns15()
    {
        var mem = CreateMemory();
        var mgr = new TrueColourManager(mem, 5, null);

        Assert.Equal(15, mgr.TrueColourToNumber(-4));
    }

    #endregion

    #region IsTransparent

    /// <summary>
    /// Verifies that IsTransparent returns true for -4 and false otherwise.
    /// </summary>
    [Theory]
    [InlineData(-4, true)]
    [InlineData(0, false)]
    [InlineData(0x7FFF, false)]
    [InlineData(-1, false)]
    public void IsTransparent_ReturnsCorrectResult(int trueColour, bool expected)
    {
        Assert.Equal(expected, TrueColourManager.IsTransparent(trueColour));
    }

    #endregion
}
