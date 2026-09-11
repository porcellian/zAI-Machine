namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Tests for Task 12.1 — Picture Resource Loading and Display:
/// PictureManager decodes PNG/JPEG from Blorb, handles Rect placeholders,
/// and provides dimensions for @picture_data.
/// </summary>
public class PictureManagerTests
{
    /// <summary>Creates a minimal valid PNG file of the given dimensions.</summary>
    private static byte[] CreateTestPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Creates a Rect placeholder chunk data (8 bytes, big-endian).</summary>
    private static byte[] CreateRectData(int width, int height)
    {
        byte[] data = new byte[8];
        data[0] = (byte)(width >> 24); data[1] = (byte)(width >> 16);
        data[2] = (byte)(width >> 8);  data[3] = (byte)width;
        data[4] = (byte)(height >> 24); data[5] = (byte)(height >> 16);
        data[6] = (byte)(height >> 8);  data[7] = (byte)height;
        return data;
    }

    /// <summary>
    /// Builds a minimal Blorb byte array with the given picture resources.
    /// Each entry is (number, chunkType, data).
    /// </summary>
    private static byte[] BuildBlorb(params (int Number, string Type, byte[] Data)[] pictures)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // We'll build chunks first, then assemble the FORM
        var chunks = new List<(string TypeId, byte[] ChunkData)>();

        // Build RIdx chunk data
        int count = pictures.Length;
        byte[] ridxData = new byte[4 + count * 12];
        WriteBE32(ridxData, 0, count);

        // We need to compute offsets: FORM header(12) + RIdx chunk(8+ridxData.Length+pad)
        int ridxChunkLen = ridxData.Length;
        int ridxTotalLen = 8 + ridxChunkLen;
        if (ridxChunkLen % 2 != 0) ridxTotalLen++;

        int currentOffset = 12 + ridxTotalLen; // after FORM header + RIdx chunk

        var pictureChunks = new List<(string Type, byte[] Data)>();
        for (int i = 0; i < count; i++)
        {
            var (number, type, data) = pictures[i];

            // RIdx entry: usage(4) + number(4) + offset(4)
            int entryOffset = 4 + i * 12;
            byte[] usage = System.Text.Encoding.ASCII.GetBytes("Pict");
            Array.Copy(usage, 0, ridxData, entryOffset, 4);
            WriteBE32(ridxData, entryOffset + 4, number);
            WriteBE32(ridxData, entryOffset + 8, currentOffset);

            pictureChunks.Add((type, data));

            int chunkTotalLen = 8 + data.Length;
            if (data.Length % 2 != 0) chunkTotalLen++;
            currentOffset += chunkTotalLen;
        }

        // Write FORM header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        int formContentLen = 4 + ridxTotalLen;
        foreach (var (_, data) in pictureChunks)
        {
            formContentLen += 8 + data.Length;
            if (data.Length % 2 != 0) formContentLen++;
        }
        WriteBE32(bw, formContentLen);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("IFRS"));

        // Write RIdx chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIdx"));
        WriteBE32(bw, ridxData.Length);
        bw.Write(ridxData);
        if (ridxData.Length % 2 != 0) bw.Write((byte)0);

        // Write picture chunks
        for (int i = 0; i < pictureChunks.Count; i++)
        {
            var (type, data) = pictureChunks[i];
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type.PadRight(4));
            bw.Write(typeBytes);
            WriteBE32(bw, data.Length);
            bw.Write(data);
            if (data.Length % 2 != 0) bw.Write((byte)0);
        }

        return ms.ToArray();
    }

    private static void WriteBE32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteBE32(BinaryWriter bw, int value)
    {
        bw.Write((byte)(value >> 24));
        bw.Write((byte)(value >> 16));
        bw.Write((byte)(value >> 8));
        bw.Write((byte)value);
    }

    private PictureManager CreateManager(BlorbReader? blorb)
    {
        var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);
        return new PictureManager(blorb, renderer, theme);
    }

    #region No Blorb

    [Fact]
    public void NoBlorb_HasPictures_False()
    {
        using var mgr = CreateManager(null);
        Assert.False(mgr.HasPictures);
    }

    [Fact]
    public void NoBlorb_PictureCount_Zero()
    {
        using var mgr = CreateManager(null);
        Assert.Equal(0, mgr.PictureCount);
    }

    [Fact]
    public void NoBlorb_HasPicture_False()
    {
        using var mgr = CreateManager(null);
        Assert.False(mgr.HasPicture(1));
    }

    [Fact]
    public void NoBlorb_GetPictureSize_Zero()
    {
        using var mgr = CreateManager(null);
        Assert.Equal((0, 0), mgr.GetPictureSize(1));
    }

    [Fact]
    public void NoBlorb_DrawPicture_ReturnsFalse()
    {
        using var mgr = CreateManager(null);
        Assert.False(mgr.DrawPicture(1, 0, 0));
    }

    #endregion

    #region PNG Loading

    [Fact]
    public void LoadPng_HasPicture()
    {
        byte[] png = CreateTestPng(32, 16);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.True(mgr.HasPictures);
        Assert.True(mgr.HasPicture(1));
        Assert.Equal(1, mgr.PictureCount);
    }

    [Fact]
    public void LoadPng_GetPictureSize()
    {
        byte[] png = CreateTestPng(64, 48);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        var (w, h) = mgr.GetPictureSize(1);
        Assert.Equal(64, w);
        Assert.Equal(48, h);
    }

    [Fact]
    public void LoadPng_NotPlaceholder()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.False(mgr.IsPlaceholder(1));
    }

    [Fact]
    public void LoadPng_DrawPicture_ReturnsTrue()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.True(mgr.DrawPicture(1, 0, 0));
    }

    [Fact]
    public void LoadPng_MultiplePictures()
    {
        byte[] png1 = CreateTestPng(20, 10);
        byte[] png2 = CreateTestPng(40, 30);
        byte[] blorb = BuildBlorb((1, "PNG ", png1), (2, "PNG ", png2));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.Equal(2, mgr.PictureCount);
        Assert.Equal((20, 10), mgr.GetPictureSize(1));
        Assert.Equal((40, 30), mgr.GetPictureSize(2));
    }

    [Fact]
    public void LoadPng_CachesDecodedBitmap()
    {
        byte[] png = CreateTestPng(16, 16);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        var size1 = mgr.GetPictureSize(1);
        var size2 = mgr.GetPictureSize(1);
        Assert.Equal(size1, size2);
    }

    #endregion

    #region Rect Placeholders

    [Fact]
    public void Rect_IsPlaceholder()
    {
        byte[] rect = CreateRectData(100, 50);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.True(mgr.IsPlaceholder(1));
    }

    [Fact]
    public void Rect_GetPictureSize()
    {
        byte[] rect = CreateRectData(200, 150);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.Equal((200, 150), mgr.GetPictureSize(1));
    }

    [Fact]
    public void Rect_HasPicture_True()
    {
        byte[] rect = CreateRectData(10, 10);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.True(mgr.HasPicture(1));
    }

    /// <summary>
    /// Blorb "Placeholder Pictures" — @draw_picture on a Rect is an error.
    /// PictureManager returns false instead of crashing.
    /// </summary>
    [Fact]
    public void Rect_DrawPicture_ReturnsFalse()
    {
        byte[] rect = CreateRectData(10, 10);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.False(mgr.DrawPicture(1, 0, 0));
    }

    /// <summary>
    /// Blorb spec: Rect is valid for @erase_picture.
    /// </summary>
    [Fact]
    public void Rect_ErasePicture_ReturnsTrue()
    {
        byte[] rect = CreateRectData(10, 10);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.True(mgr.ErasePicture(1, 0, 0));
    }

    [Fact]
    public void Rect_ZeroDimensions()
    {
        byte[] rect = CreateRectData(0, 0);
        byte[] blorb = BuildBlorb((1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.Equal((0, 0), mgr.GetPictureSize(1));
    }

    #endregion

    #region Mixed PNG and Rect

    [Fact]
    public void MixedResources_PngAndRect()
    {
        byte[] png = CreateTestPng(32, 24);
        byte[] rect = CreateRectData(100, 80);
        byte[] blorb = BuildBlorb((1, "PNG ", png), (2, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.Equal(2, mgr.PictureCount);
        Assert.False(mgr.IsPlaceholder(1));
        Assert.True(mgr.IsPlaceholder(2));
        Assert.Equal((32, 24), mgr.GetPictureSize(1));
        Assert.Equal((100, 80), mgr.GetPictureSize(2));
        Assert.True(mgr.DrawPicture(1, 0, 0));
        Assert.False(mgr.DrawPicture(2, 0, 0));
    }

    #endregion

    #region Nonexistent Resources

    [Fact]
    public void NonexistentPicture_HasPicture_False()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.False(mgr.HasPicture(999));
    }

    [Fact]
    public void NonexistentPicture_Size_Zero()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.Equal((0, 0), mgr.GetPictureSize(999));
    }

    [Fact]
    public void NonexistentPicture_DrawPicture_False()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.False(mgr.DrawPicture(999, 0, 0));
    }

    [Fact]
    public void NonexistentPicture_IsPlaceholder_False()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        Assert.False(mgr.IsPlaceholder(999));
    }

    #endregion

    #region IPictureProvider Contract

    [Fact]
    public void ImplementsIPictureProvider()
    {
        using var mgr = CreateManager(null);
        Assert.IsAssignableFrom<IPictureProvider>(mgr);
    }

    [Fact]
    public void ReleaseNumber_DefaultsToZero()
    {
        using var mgr = CreateManager(null);
        Assert.Equal(0, mgr.ReleaseNumber);
    }

    #endregion

    #region Dispose

    /// <summary>
    /// Dispose doesn't throw and can be called after use.
    /// </summary>
    [Fact]
    public void Dispose_DoesNotThrow()
    {
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorb((1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        var mgr = CreateManager(reader);

        mgr.GetPictureSize(1);
        mgr.DrawPicture(1, 0, 0);
        mgr.Dispose();
    }

    #endregion
}
