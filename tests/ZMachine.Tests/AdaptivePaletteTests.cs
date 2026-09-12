using ZMachine.Core;

namespace ZMachine.Tests;

/// <summary>
/// Tests for APal chunk parsing (<see cref="BlorbReader"/>) and the
/// <see cref="AdaptivePaletteManager"/> current palette tracking.
/// </summary>
/// <remarks>
/// Blorb "The Adaptive Palette Chunk" — adaptive pictures render with
/// the current palette; non-adaptive pictures update it.
/// </remarks>
public class AdaptivePaletteTests
{
    #region BlorbReader — APal Parsing

    /// <summary>
    /// Verifies that a Blorb with no APal chunk reports
    /// HasAdaptivePalette = false and empty AdaptivePictures.
    /// </summary>
    [Fact]
    public void NoAPalChunk_HasAdaptivePalette_False()
    {
        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            []);
        Assert.False(blorb.HasAdaptivePalette);
        Assert.Empty(blorb.AdaptivePictures);
    }

    /// <summary>
    /// Verifies that an empty APal chunk (Shogun, Journey pattern)
    /// sets HasAdaptivePalette = true but AdaptivePictures is empty.
    /// Blorb "The Adaptive Palette Chunk" — empty APal signals
    /// palette-changing behaviour.
    /// </summary>
    [Fact]
    public void EmptyAPalChunk_HasAdaptivePalette_True_NoPictures()
    {
        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            [new IffChunk("APal", [])]);

        Assert.True(blorb.HasAdaptivePalette);
        Assert.Empty(blorb.AdaptivePictures);
    }

    /// <summary>
    /// Verifies that an APal chunk with two entries parses both picture
    /// resource numbers correctly.
    /// </summary>
    [Fact]
    public void APalWithEntries_ParsesPictureNumbers()
    {
        byte[] apalData = new byte[8];
        WriteInt32BE(apalData, 0, 3);  // picture 3
        WriteInt32BE(apalData, 4, 7);  // picture 7

        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            [new IffChunk("APal", apalData)]);

        Assert.True(blorb.HasAdaptivePalette);
        Assert.Equal(2, blorb.AdaptivePictures.Count);
        Assert.Contains(3, blorb.AdaptivePictures);
        Assert.Contains(7, blorb.AdaptivePictures);
    }

    /// <summary>
    /// Verifies that a single-entry APal chunk works correctly.
    /// </summary>
    [Fact]
    public void APalSingleEntry_ParsesCorrectly()
    {
        byte[] apalData = new byte[4];
        WriteInt32BE(apalData, 0, 42);

        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            [new IffChunk("APal", apalData)]);

        Assert.Single(blorb.AdaptivePictures);
        Assert.Contains(42, blorb.AdaptivePictures);
    }

    /// <summary>
    /// Verifies that APal with non-multiple-of-4 length generates a
    /// warning but still parses complete entries.
    /// </summary>
    [Fact]
    public void APalOddLength_GeneratesWarning()
    {
        byte[] apalData = new byte[5]; // 1 full entry + 1 extra byte
        WriteInt32BE(apalData, 0, 10);

        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            [new IffChunk("APal", apalData)]);

        Assert.True(blorb.HasAdaptivePalette);
        Assert.Contains(10, blorb.AdaptivePictures);
        Assert.Contains(blorb.Warnings, w => w.Contains("not a multiple of 4"));
    }

    /// <summary>
    /// Verifies that CreateAdaptivePaletteManager returns null when
    /// no APal chunk is present.
    /// </summary>
    [Fact]
    public void CreateAdaptivePaletteManager_NoAPal_ReturnsNull()
    {
        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            []);
        Assert.Null(blorb.CreateAdaptivePaletteManager());
    }

    /// <summary>
    /// Verifies that CreateAdaptivePaletteManager returns a manager
    /// when APal is present (even if empty).
    /// </summary>
    [Fact]
    public void CreateAdaptivePaletteManager_WithAPal_ReturnsManager()
    {
        var blorb = LoadBlorbWithExtra(
            [("Pict", 1, 0)],
            [new IffChunk("PNG ", CreateMinimalPng())],
            [new IffChunk("APal", [])]);

        var mgr = blorb.CreateAdaptivePaletteManager();
        Assert.NotNull(mgr);
    }

    #endregion

    #region AdaptivePaletteManager — IsAdaptive

    /// <summary>
    /// Verifies that IsAdaptive returns true for listed pictures
    /// and false for others.
    /// </summary>
    [Fact]
    public void IsAdaptive_ListedPicture_True()
    {
        var mgr = new AdaptivePaletteManager([3, 7, 12]);
        Assert.True(mgr.IsAdaptive(3));
        Assert.True(mgr.IsAdaptive(7));
        Assert.True(mgr.IsAdaptive(12));
        Assert.False(mgr.IsAdaptive(1));
        Assert.False(mgr.IsAdaptive(5));
    }

    /// <summary>
    /// Verifies that an empty adaptive set (Shogun/Journey) has no
    /// adaptive pictures.
    /// </summary>
    [Fact]
    public void IsAdaptive_EmptySet_AllFalse()
    {
        var mgr = new AdaptivePaletteManager([]);
        Assert.False(mgr.IsAdaptive(1));
        Assert.False(mgr.IsAdaptive(0));
        Assert.Equal(0, mgr.AdaptivePictureCount);
    }

    #endregion

    #region AdaptivePaletteManager — Current Palette

    /// <summary>
    /// Verifies that the default palette is all black (0, 0, 0).
    /// </summary>
    [Fact]
    public void DefaultPalette_AllBlack()
    {
        var mgr = new AdaptivePaletteManager([1]);
        var pal = mgr.GetCurrentPalette();
        Assert.Equal(16, pal.Length);
        for (int i = 0; i < 16; i++)
            Assert.Equal((byte)0, pal[i].R);
    }

    /// <summary>
    /// Verifies that UpdateFromNonAdaptivePicture copies all 16
    /// entries when given a full palette.
    /// Blorb "The Adaptive Palette Chunk" — copy PLTE into current palette.
    /// </summary>
    [Fact]
    public void UpdatePalette_Full16Entries_AllCopied()
    {
        var mgr = new AdaptivePaletteManager([5]);
        var plte = new (byte R, byte G, byte B)[16];
        for (int i = 0; i < 16; i++)
            plte[i] = ((byte)(i * 10), (byte)(i * 15), (byte)(i * 5));

        mgr.UpdateFromNonAdaptivePicture(plte);

        var pal = mgr.GetCurrentPalette();
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(plte[i].R, pal[i].R);
            Assert.Equal(plte[i].G, pal[i].G);
            Assert.Equal(plte[i].B, pal[i].B);
        }
    }

    /// <summary>
    /// Verifies that a partial palette (fewer than 16 entries) only
    /// updates those entries, leaving the rest unchanged.
    /// Blorb "The Adaptive Palette Chunk" — "If its palette has fewer
    /// than 16 entries, then only those entries of the Current Palette
    /// are changed."
    /// </summary>
    [Fact]
    public void UpdatePalette_PartialEntries_OnlyUpdatesThose()
    {
        var mgr = new AdaptivePaletteManager([1]);

        // First, set a full palette
        var full = new (byte R, byte G, byte B)[16];
        for (int i = 0; i < 16; i++)
            full[i] = (255, 255, 255);
        mgr.UpdateFromNonAdaptivePicture(full);

        // Then update with only 4 entries
        var partial = new (byte R, byte G, byte B)[]
        {
            (10, 20, 30), (40, 50, 60), (70, 80, 90), (100, 110, 120)
        };
        mgr.UpdateFromNonAdaptivePicture(partial);

        var pal = mgr.GetCurrentPalette();
        // Entries 0–3 should be updated
        Assert.Equal((byte)10, pal[0].R);
        Assert.Equal((byte)100, pal[3].R);
        // Entries 4–15 should remain (255, 255, 255)
        Assert.Equal((byte)255, pal[4].R);
        Assert.Equal((byte)255, pal[15].R);
    }

    /// <summary>
    /// Verifies that GetColor returns the correct value at a specific index.
    /// </summary>
    [Fact]
    public void GetColor_ReturnsCorrectEntry()
    {
        var mgr = new AdaptivePaletteManager([]);
        var plte = new (byte R, byte G, byte B)[16];
        plte[5] = (0xAA, 0xBB, 0xCC);
        mgr.UpdateFromNonAdaptivePicture(plte);

        var c = mgr.GetColor(5);
        Assert.Equal(0xAA, c.R);
        Assert.Equal(0xBB, c.G);
        Assert.Equal(0xCC, c.B);
    }

    /// <summary>
    /// Verifies that GetColor with out-of-range index returns black.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(100)]
    public void GetColor_OutOfRange_ReturnsBlack(int index)
    {
        var mgr = new AdaptivePaletteManager([]);
        var c = mgr.GetColor(index);
        Assert.Equal((byte)0, c.R);
        Assert.Equal((byte)0, c.G);
        Assert.Equal((byte)0, c.B);
    }

    #endregion

    #region AdaptivePaletteManager — Palette Version

    /// <summary>
    /// Verifies that PaletteVersion starts at 0 and increments on update.
    /// </summary>
    [Fact]
    public void PaletteVersion_IncreasesOnUpdate()
    {
        var mgr = new AdaptivePaletteManager([1]);
        Assert.Equal(0, mgr.PaletteVersion);

        mgr.UpdateFromNonAdaptivePicture(new (byte, byte, byte)[4]);
        Assert.Equal(1, mgr.PaletteVersion);

        mgr.UpdateFromNonAdaptivePicture(new (byte, byte, byte)[4]);
        Assert.Equal(2, mgr.PaletteVersion);
    }

    /// <summary>
    /// Verifies that Reset clears the palette and increments version.
    /// </summary>
    [Fact]
    public void Reset_ClearsPaletteAndIncrementsVersion()
    {
        var mgr = new AdaptivePaletteManager([]);
        var plte = new (byte R, byte G, byte B)[16];
        plte[5] = (255, 128, 64);
        mgr.UpdateFromNonAdaptivePicture(plte);

        int versionBefore = mgr.PaletteVersion;
        mgr.Reset();

        Assert.Equal(versionBefore + 1, mgr.PaletteVersion);
        Assert.Equal((byte)0, mgr.GetColor(5).R);
    }

    #endregion

    #region AdaptivePaletteManager — Draw Workflow

    /// <summary>
    /// Simulates the correct draw workflow: draw a non-adaptive picture
    /// (updates palette), then draw an adaptive picture (uses palette).
    /// Blorb "The Adaptive Palette Chunk" — this is the intended usage.
    /// </summary>
    [Fact]
    public void DrawWorkflow_NonAdaptive_ThenAdaptive()
    {
        var mgr = new AdaptivePaletteManager([5, 6]);

        // Draw non-adaptive picture 1 → updates palette
        Assert.False(mgr.IsAdaptive(1));
        var plte = new (byte R, byte G, byte B)[16];
        plte[2] = (255, 0, 0);   // index 2 = red
        plte[3] = (0, 255, 0);   // index 3 = green
        plte[4] = (0, 0, 255);   // index 4 = blue
        mgr.UpdateFromNonAdaptivePicture(plte);

        // Draw adaptive picture 5 → should use current palette
        Assert.True(mgr.IsAdaptive(5));
        var pal = mgr.GetCurrentPalette();
        Assert.Equal((byte)255, pal[2].R);  // red from picture 1
        Assert.Equal((byte)255, pal[3].G);  // green from picture 1
        Assert.Equal((byte)255, pal[4].B);  // blue from picture 1
    }

    /// <summary>
    /// Verifies that a second non-adaptive picture replaces the palette
    /// set by the first.
    /// </summary>
    [Fact]
    public void DrawWorkflow_TwoNonAdaptive_SecondReplacesFirst()
    {
        var mgr = new AdaptivePaletteManager([10]);

        // Draw first non-adaptive picture
        var plte1 = new (byte R, byte G, byte B)[16];
        plte1[2] = (255, 0, 0);
        mgr.UpdateFromNonAdaptivePicture(plte1);
        Assert.Equal((byte)255, mgr.GetColor(2).R);

        // Draw second non-adaptive picture with different colours
        var plte2 = new (byte R, byte G, byte B)[16];
        plte2[2] = (0, 128, 0);
        mgr.UpdateFromNonAdaptivePicture(plte2);
        Assert.Equal((byte)0, mgr.GetColor(2).R);
        Assert.Equal((byte)128, mgr.GetColor(2).G);
    }

    /// <summary>
    /// Verifies that GetCurrentPalette returns a copy, not a reference.
    /// </summary>
    [Fact]
    public void GetCurrentPalette_ReturnsCopy()
    {
        var mgr = new AdaptivePaletteManager([]);
        var plte = new (byte R, byte G, byte B)[16];
        plte[5] = (100, 200, 50);
        mgr.UpdateFromNonAdaptivePicture(plte);

        var copy = mgr.GetCurrentPalette();
        copy[5] = (0, 0, 0); // mutate the copy

        // Original should be unchanged
        Assert.Equal((byte)100, mgr.GetColor(5).R);
    }

    #endregion

    #region Helpers

    private static BlorbReader LoadBlorbWithExtra(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks,
        IffChunk[] extraChunks)
    {
        byte[] data = BuildBlorbBytes(entries, resourceChunks, extraChunks);
        return BlorbReader.Load(data);
    }

    private static byte[] BuildBlorbBytes(
        (string Usage, int Number, int ChunkIndex)[] entries,
        IffChunk[] resourceChunks,
        IffChunk[]? extraChunks = null)
    {
        int ridxDataLen = 4 + entries.Length * 12;
        int offset = 12 + 8 + ridxDataLen;

        int[] chunkOffsets = new int[resourceChunks.Length];
        for (int i = 0; i < resourceChunks.Length; i++)
        {
            chunkOffsets[i] = offset;
            uint chunkLen = resourceChunks[i].Length;
            offset += 8 + (int)chunkLen;
            if (chunkLen % 2 != 0)
                offset++;
        }

        byte[] ridxData = new byte[ridxDataLen];
        WriteInt32BE(ridxData, 0, entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            int entryOff = 4 + i * 12;
            WriteAscii(ridxData, entryOff, entries[i].Usage);
            WriteInt32BE(ridxData, entryOff + 4, entries[i].Number);
            WriteInt32BE(ridxData, entryOff + 8, chunkOffsets[entries[i].ChunkIndex]);
        }

        var allChunks = new List<IffChunk> { new("RIdx", ridxData) };
        allChunks.AddRange(resourceChunks);
        if (extraChunks != null)
            allChunks.AddRange(extraChunks);

        return IffWriter.WriteToArray("IFRS", allChunks);
    }

    private static byte[] CreateMinimalPng()
    {
        // Minimal valid PNG: 8-byte signature + IHDR + IEND
        // (just enough to be accepted as PNG data by the Blorb reader)
        return [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR length
            0x49, 0x48, 0x44, 0x52, // "IHDR"
            0x00, 0x00, 0x00, 0x01, // width = 1
            0x00, 0x00, 0x00, 0x01, // height = 1
            0x08, 0x03,             // 8-bit indexed color
            0x00, 0x00, 0x00,       // compression, filter, interlace
            0x28, 0xCB, 0x34, 0xBB, // IHDR CRC (approximate)
            0x00, 0x00, 0x00, 0x00, // IEND length
            0x49, 0x45, 0x4E, 0x44, // "IEND"
            0xAE, 0x42, 0x60, 0x82  // IEND CRC
        ];
    }

    private static void WriteInt32BE(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteAscii(byte[] buf, int offset, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, 0, buf, offset, 4);
    }

    #endregion
}
