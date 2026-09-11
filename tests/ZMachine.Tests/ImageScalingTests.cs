namespace ZMachine.Tests;

using SkiaSharp;
using ZMachine.Core;
using ZMachine.IO;

/// <summary>
/// Tests for Task 12.2 — Image Scaling and Resolution System:
/// ImageScaler ERF/ratio calculations, BlorbReader 'Reso' chunk parsing,
/// and PictureManager scaled dimension reporting.
/// </summary>
public class ImageScalingTests
{
    #region ImageScalingEntry

    [Fact]
    public void Entry_StandardRatio_ComputedCorrectly()
    {
        var entry = new ImageScalingEntry { StandardNum = 3, StandardDen = 2 };
        Assert.Equal(1.5, entry.StandardRatio);
    }

    [Fact]
    public void Entry_StandardRatio_ZeroDen_ReturnsOne()
    {
        var entry = new ImageScalingEntry { StandardNum = 5, StandardDen = 0 };
        Assert.Equal(1.0, entry.StandardRatio);
    }

    [Fact]
    public void Entry_MinRatio_ZeroZero_Null()
    {
        var entry = new ImageScalingEntry { MinNum = 0, MinDen = 0 };
        Assert.Null(entry.MinRatio);
    }

    [Fact]
    public void Entry_MinRatio_NonZero_HasValue()
    {
        var entry = new ImageScalingEntry { MinNum = 1, MinDen = 2 };
        Assert.Equal(0.5, entry.MinRatio);
    }

    [Fact]
    public void Entry_MaxRatio_ZeroZero_Null()
    {
        var entry = new ImageScalingEntry { MaxNum = 0, MaxDen = 0 };
        Assert.Null(entry.MaxRatio);
    }

    [Fact]
    public void Entry_MaxRatio_NonZero_HasValue()
    {
        var entry = new ImageScalingEntry { MaxNum = 3, MaxDen = 1 };
        Assert.Equal(3.0, entry.MaxRatio);
    }

    [Fact]
    public void Entry_IsFixed_WhenMinEqualsMax()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 2, MinDen = 1,
            MaxNum = 2, MaxDen = 1,
        };
        Assert.True(entry.IsFixed);
    }

    [Fact]
    public void Entry_NotFixed_WhenMinDiffersFromMax()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 1, MinDen = 2,
            MaxNum = 3, MaxDen = 1,
        };
        Assert.False(entry.IsFixed);
    }

    [Fact]
    public void Entry_NotFixed_WhenMinIsNull()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 0, MinDen = 0,
            MaxNum = 2, MaxDen = 1,
        };
        Assert.False(entry.IsFixed);
    }

    #endregion

    #region ImageScaler.ComputeERF

    [Fact]
    public void ERF_ExactMatch_ReturnsOne()
    {
        double erf = ImageScaler.ComputeERF(640, 480, 640, 480);
        Assert.Equal(1.0, erf);
    }

    [Fact]
    public void ERF_DoubleSize_ReturnsTwo()
    {
        double erf = ImageScaler.ComputeERF(1280, 960, 640, 480);
        Assert.Equal(2.0, erf);
    }

    [Fact]
    public void ERF_HalfSize_ReturnsHalf()
    {
        double erf = ImageScaler.ComputeERF(320, 240, 640, 480);
        Assert.Equal(0.5, erf);
    }

    [Fact]
    public void ERF_WiderWindow_ConstrainedByHeight()
    {
        // Window is 2× wide but only 1× tall — ERF limited by height
        double erf = ImageScaler.ComputeERF(1280, 480, 640, 480);
        Assert.Equal(1.0, erf);
    }

    [Fact]
    public void ERF_TallerWindow_ConstrainedByWidth()
    {
        // Window is 1× wide but 2× tall — ERF limited by width
        double erf = ImageScaler.ComputeERF(640, 960, 640, 480);
        Assert.Equal(1.0, erf);
    }

    [Fact]
    public void ERF_ZeroStandard_ReturnsOne()
    {
        double erf = ImageScaler.ComputeERF(640, 480, 0, 0);
        Assert.Equal(1.0, erf);
    }

    #endregion

    #region ImageScaler.ComputeRatio

    [Fact]
    public void Ratio_NoLimits_ReturnsErfTimesStandard()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 0, MinDen = 0,
            MaxNum = 0, MaxDen = 0,
        };
        double r = ImageScaler.ComputeRatio(2.0, entry);
        Assert.Equal(2.0, r);
    }

    [Fact]
    public void Ratio_ClampedToMin()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 1, MinDen = 1,
            MaxNum = 0, MaxDen = 0,
        };
        // ERF=0.5 × stdratio=1.0 = 0.5, clamped to min=1.0
        double r = ImageScaler.ComputeRatio(0.5, entry);
        Assert.Equal(1.0, r);
    }

    [Fact]
    public void Ratio_ClampedToMax()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 0, MinDen = 0,
            MaxNum = 3, MaxDen = 2,
        };
        // ERF=5.0 × stdratio=1.0 = 5.0, clamped to max=1.5
        double r = ImageScaler.ComputeRatio(5.0, entry);
        Assert.Equal(1.5, r);
    }

    [Fact]
    public void Ratio_Fixed_IgnoresERF()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 1,
            MinNum = 2, MinDen = 1,
            MaxNum = 2, MaxDen = 1,
        };
        // Fixed at 2.0 regardless of ERF
        Assert.Equal(2.0, ImageScaler.ComputeRatio(0.1, entry));
        Assert.Equal(2.0, ImageScaler.ComputeRatio(10.0, entry));
    }

    [Fact]
    public void Ratio_NonUnitStandard()
    {
        var entry = new ImageScalingEntry
        {
            StandardNum = 1, StandardDen = 2,
            MinNum = 0, MinDen = 0,
            MaxNum = 0, MaxDen = 0,
        };
        // ERF=2.0 × stdratio=0.5 = 1.0
        double r = ImageScaler.ComputeRatio(2.0, entry);
        Assert.Equal(1.0, r);
    }

    #endregion

    #region ImageScaler.ComputeScaledSize

    [Fact]
    public void ScaledSize_NoReso_ReturnsRawSize()
    {
        var (w, h) = ImageScaler.ComputeScaledSize(100, 50, 640, 480, null, 1);
        Assert.Equal(100, w);
        Assert.Equal(50, h);
    }

    [Fact]
    public void ScaledSize_NoEntry_ReturnsRawSize()
    {
        var reso = new ResolutionInfo
        {
            StandardWidth = 640,
            StandardHeight = 480,
            Entries = new Dictionary<int, ImageScalingEntry>(),
        };
        var (w, h) = ImageScaler.ComputeScaledSize(100, 50, 640, 480, reso, 1);
        Assert.Equal(100, w);
        Assert.Equal(50, h);
    }

    [Fact]
    public void ScaledSize_DoubleWindow_DoubleSize()
    {
        var entries = new Dictionary<int, ImageScalingEntry>
        {
            [1] = new ImageScalingEntry
            {
                StandardNum = 1, StandardDen = 1,
                MinNum = 0, MinDen = 0,
                MaxNum = 0, MaxDen = 0,
            },
        };
        var reso = new ResolutionInfo
        {
            StandardWidth = 640,
            StandardHeight = 480,
            Entries = entries,
        };
        // Window is 2× standard → ERF=2.0, ratio=2.0
        var (w, h) = ImageScaler.ComputeScaledSize(100, 50, 1280, 960, reso, 1);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }

    [Fact]
    public void ScaledSize_MinimumOnePx()
    {
        var entries = new Dictionary<int, ImageScalingEntry>
        {
            [1] = new ImageScalingEntry
            {
                StandardNum = 1, StandardDen = 1000,
                MinNum = 0, MinDen = 0,
                MaxNum = 0, MaxDen = 0,
            },
        };
        var reso = new ResolutionInfo
        {
            StandardWidth = 640,
            StandardHeight = 480,
            Entries = entries,
        };
        // Very tiny ratio → dimensions clamped to 1
        var (w, h) = ImageScaler.ComputeScaledSize(10, 10, 640, 480, reso, 1);
        Assert.True(w >= 1);
        Assert.True(h >= 1);
    }

    [Fact]
    public void ScaledSize_FixedRatio()
    {
        var entries = new Dictionary<int, ImageScalingEntry>
        {
            [1] = new ImageScalingEntry
            {
                StandardNum = 1, StandardDen = 1,
                MinNum = 1, MinDen = 2,
                MaxNum = 1, MaxDen = 2,
            },
        };
        var reso = new ResolutionInfo
        {
            StandardWidth = 640,
            StandardHeight = 480,
            Entries = entries,
        };
        // Fixed at 0.5 regardless of window size
        var (w, h) = ImageScaler.ComputeScaledSize(100, 80, 1280, 960, reso, 1);
        Assert.Equal(50, w);
        Assert.Equal(40, h);
    }

    #endregion

    #region BlorbReader Reso Chunk Parsing

    private static byte[] BuildBlorbWithReso(byte[] resoData, params (int Number, string Type, byte[] Data)[] pictures)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        var chunks = new List<(string TypeId, byte[] ChunkData)>();

        // Build RIdx
        int count = pictures.Length;
        byte[] ridxData = new byte[4 + count * 12];
        WriteBE32(ridxData, 0, count);

        int ridxChunkLen = ridxData.Length;
        int ridxTotalLen = 8 + ridxChunkLen;
        if (ridxChunkLen % 2 != 0) ridxTotalLen++;

        int resoTotalLen = 8 + resoData.Length;
        if (resoData.Length % 2 != 0) resoTotalLen++;

        int currentOffset = 12 + ridxTotalLen + resoTotalLen;

        var pictureChunks = new List<(string Type, byte[] Data)>();
        for (int i = 0; i < count; i++)
        {
            var (number, type, data) = pictures[i];
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

        // Compute FORM content length
        int formContentLen = 4 + ridxTotalLen + resoTotalLen;
        foreach (var (_, data) in pictureChunks)
        {
            formContentLen += 8 + data.Length;
            if (data.Length % 2 != 0) formContentLen++;
        }

        // Write FORM header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        WriteBE32(bw, formContentLen);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("IFRS"));

        // Write RIdx
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIdx"));
        WriteBE32(bw, ridxData.Length);
        bw.Write(ridxData);
        if (ridxData.Length % 2 != 0) bw.Write((byte)0);

        // Write Reso
        bw.Write(System.Text.Encoding.ASCII.GetBytes("Reso"));
        WriteBE32(bw, resoData.Length);
        bw.Write(resoData);
        if (resoData.Length % 2 != 0) bw.Write((byte)0);

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

    private static byte[] BuildResoData(int px, int py, int minx, int miny,
        int maxx, int maxy, params (int Num, int StdN, int StdD, int MinN, int MinD, int MaxN, int MaxD)[] entries)
    {
        byte[] data = new byte[24 + entries.Length * 28];
        WriteBE32(data, 0, px);
        WriteBE32(data, 4, py);
        WriteBE32(data, 8, minx);
        WriteBE32(data, 12, miny);
        WriteBE32(data, 16, maxx);
        WriteBE32(data, 20, maxy);

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            int offset = 24 + i * 28;
            WriteBE32(data, offset, e.Num);
            WriteBE32(data, offset + 4, e.StdN);
            WriteBE32(data, offset + 8, e.StdD);
            WriteBE32(data, offset + 12, e.MinN);
            WriteBE32(data, offset + 16, e.MinD);
            WriteBE32(data, offset + 20, e.MaxN);
            WriteBE32(data, offset + 24, e.MaxD);
        }

        return data;
    }

    private static byte[] CreateTestPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateRectData(int width, int height)
    {
        byte[] data = new byte[8];
        WriteBE32(data, 0, width);
        WriteBE32(data, 4, height);
        return data;
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

    [Fact]
    public void Reso_HeaderOnly_ParsedCorrectly()
    {
        byte[] resoData = BuildResoData(640, 480, 320, 240, 1280, 960);
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);

        Assert.NotNull(reader.Resolution);
        Assert.Equal(640, reader.Resolution!.StandardWidth);
        Assert.Equal(480, reader.Resolution.StandardHeight);
        Assert.Equal(320, reader.Resolution.MinWidth);
        Assert.Equal(240, reader.Resolution.MinHeight);
        Assert.Equal(1280, reader.Resolution.MaxWidth);
        Assert.Equal(960, reader.Resolution.MaxHeight);
        Assert.Empty(reader.Resolution.Entries);
    }

    [Fact]
    public void Reso_WithEntry_ParsedCorrectly()
    {
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (1, 1, 1, 1, 2, 3, 1));
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);

        Assert.NotNull(reader.Resolution);
        Assert.True(reader.Resolution!.Entries.ContainsKey(1));
        var entry = reader.Resolution.Entries[1];
        Assert.Equal(1, entry.StandardNum);
        Assert.Equal(1, entry.StandardDen);
        Assert.Equal(1, entry.MinNum);
        Assert.Equal(2, entry.MinDen);
        Assert.Equal(3, entry.MaxNum);
        Assert.Equal(1, entry.MaxDen);
    }

    [Fact]
    public void Reso_MultipleEntries()
    {
        byte[] resoData = BuildResoData(800, 600, 0, 0, 0, 0,
            (1, 1, 1, 0, 0, 0, 0),
            (2, 2, 1, 1, 1, 3, 1));
        byte[] png1 = CreateTestPng(10, 10);
        byte[] png2 = CreateTestPng(20, 20);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png1), (2, "PNG ", png2));
        var reader = BlorbReader.Load(blorb);

        Assert.NotNull(reader.Resolution);
        Assert.Equal(2, reader.Resolution!.Entries.Count);
        Assert.True(reader.Resolution.Entries.ContainsKey(1));
        Assert.True(reader.Resolution.Entries.ContainsKey(2));
    }

    [Fact]
    public void Reso_ZeroStandardSize_Warning()
    {
        byte[] resoData = BuildResoData(0, 480, 0, 0, 0, 0);
        byte[] png = CreateTestPng(10, 10);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);

        Assert.Null(reader.Resolution);
        Assert.Contains(reader.Warnings, w => w.Contains("non-zero"));
    }

    [Fact]
    public void NoBlorb_NoReso()
    {
        // A Blorb without a Reso chunk
        byte[] png = CreateTestPng(10, 10);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        byte[] ridxData = new byte[4 + 12];
        WriteBE32(ridxData, 0, 1);
        byte[] usage = System.Text.Encoding.ASCII.GetBytes("Pict");
        Array.Copy(usage, 0, ridxData, 4, 4);
        WriteBE32(ridxData, 8, 1);

        int ridxTotal = 8 + ridxData.Length;
        int pngTotal = 8 + png.Length;
        if (png.Length % 2 != 0) pngTotal++;

        // The offset points to the PNG chunk at position 12 + ridxTotal
        WriteBE32(ridxData, 12, 12 + ridxTotal);

        int formContent = 4 + ridxTotal + pngTotal;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        WriteBE32(bw, formContent);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("IFRS"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIdx"));
        WriteBE32(bw, ridxData.Length);
        bw.Write(ridxData);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("PNG "));
        WriteBE32(bw, png.Length);
        bw.Write(png);
        if (png.Length % 2 != 0) bw.Write((byte)0);

        var reader = BlorbReader.Load(ms.ToArray());
        Assert.Null(reader.Resolution);
    }

    #endregion

    #region PictureManager Scaled Dimensions

    private PictureManager CreateManager(BlorbReader? blorb)
    {
        var renderer = new SkiaRenderer();
        var theme = new ThemeConfig();
        renderer.Initialize(theme.PixelWidth, theme.PixelHeight, theme);
        return new PictureManager(blorb, renderer, theme);
    }

    [Fact]
    public void PictureManager_NoReso_ReturnsRawSize()
    {
        byte[] png = CreateTestPng(64, 48);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        byte[] ridxData = new byte[4 + 12];
        WriteBE32(ridxData, 0, 1);
        byte[] usage = System.Text.Encoding.ASCII.GetBytes("Pict");
        Array.Copy(usage, 0, ridxData, 4, 4);
        WriteBE32(ridxData, 8, 1);

        int ridxTotal = 8 + ridxData.Length;
        WriteBE32(ridxData, 12, 12 + ridxTotal);

        int pngTotal = 8 + png.Length;
        if (png.Length % 2 != 0) pngTotal++;

        int formContent = 4 + ridxTotal + pngTotal;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        WriteBE32(bw, formContent);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("IFRS"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIdx"));
        WriteBE32(bw, ridxData.Length);
        bw.Write(ridxData);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("PNG "));
        WriteBE32(bw, png.Length);
        bw.Write(png);
        if (png.Length % 2 != 0) bw.Write((byte)0);

        var reader = BlorbReader.Load(ms.ToArray());
        using var mgr = CreateManager(reader);

        var (w, h) = mgr.GetPictureSize(1);
        Assert.Equal(64, w);
        Assert.Equal(48, h);
    }

    [Fact]
    public void PictureManager_WithReso_ReturnsScaledSize()
    {
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (1, 1, 1, 0, 0, 0, 0));
        byte[] png = CreateTestPng(100, 50);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        // Set window to 2× standard
        mgr.WindowWidth = 1280;
        mgr.WindowHeight = 960;

        var (w, h) = mgr.GetPictureSize(1);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }

    [Fact]
    public void PictureManager_WindowResize_ChangesScaledSize()
    {
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (1, 1, 1, 0, 0, 0, 0));
        byte[] png = CreateTestPng(100, 50);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        mgr.WindowWidth = 640;
        mgr.WindowHeight = 480;
        var (w1, h1) = mgr.GetPictureSize(1);

        mgr.WindowWidth = 1280;
        mgr.WindowHeight = 960;
        var (w2, h2) = mgr.GetPictureSize(1);

        Assert.Equal(100, w1);
        Assert.Equal(50, h1);
        Assert.Equal(200, w2);
        Assert.Equal(100, h2);
    }

    [Fact]
    public void PictureManager_ImageWithoutEntry_ReturnsRawSize()
    {
        // Reso exists but pic 1 has no entry — should return raw size
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (99, 1, 1, 0, 0, 0, 0));
        byte[] png = CreateTestPng(80, 60);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);
        mgr.WindowWidth = 1280;
        mgr.WindowHeight = 960;

        var (w, h) = mgr.GetPictureSize(1);
        Assert.Equal(80, w);
        Assert.Equal(60, h);
    }

    [Fact]
    public void PictureManager_FixedRatio_IgnoresWindowSize()
    {
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (1, 1, 1, 1, 2, 1, 2));
        byte[] png = CreateTestPng(100, 100);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "PNG ", png));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        mgr.WindowWidth = 200;
        mgr.WindowHeight = 200;
        var (w1, h1) = mgr.GetPictureSize(1);

        mgr.WindowWidth = 2000;
        mgr.WindowHeight = 2000;
        var (w2, h2) = mgr.GetPictureSize(1);

        // Fixed ratio 1/2 = 0.5 → 50×50 regardless
        Assert.Equal(50, w1);
        Assert.Equal(50, h1);
        Assert.Equal(w1, w2);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void PictureManager_Rect_ScaledSize()
    {
        byte[] resoData = BuildResoData(640, 480, 0, 0, 0, 0,
            (1, 1, 1, 0, 0, 0, 0));
        byte[] rect = CreateRectData(200, 100);
        byte[] blorb = BuildBlorbWithReso(resoData, (1, "Rect", rect));
        var reader = BlorbReader.Load(blorb);
        using var mgr = CreateManager(reader);

        mgr.WindowWidth = 1280;
        mgr.WindowHeight = 960;

        var (w, h) = mgr.GetPictureSize(1);
        Assert.Equal(400, w);
        Assert.Equal(200, h);
    }

    #endregion
}
