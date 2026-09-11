namespace ZMachine.IO;

using SkiaSharp;
using ZMachine.Core;

/// <summary>
/// Loads picture resources from a Blorb file and renders them via
/// SkiaSharp. Decodes PNG and JPEG images on demand, caches decoded
/// bitmaps, and handles Rect placeholders per Blorb spec.
/// </summary>
/// <remarks>
/// Blorb "Picture Resource Chunks" — PNG ('PNG '), JPEG ('JPEG'), Rect ('Rect').
/// ZSpec11 "@picture_data" — queries picture dimensions and availability.
/// </remarks>
public class PictureManager : IPictureProvider, IDisposable
{
    private readonly BlorbReader? _blorb;
    private readonly IRenderer _renderer;
    private readonly ThemeConfig _theme;
    private readonly Dictionary<int, SKBitmap> _cache = new();
    private readonly Dictionary<int, (int Width, int Height)> _sizeCache = new();

    /// <summary>
    /// Creates a PictureManager backed by an optional Blorb resource file.
    /// When blorb is null, no pictures are available.
    /// </summary>
    public PictureManager(BlorbReader? blorb, IRenderer renderer, ThemeConfig theme)
    {
        _blorb = blorb;
        _renderer = renderer;
        _theme = theme;
    }

    /// <inheritdoc />
    public bool HasPictures => _blorb != null && _blorb.PictureCount > 0;

    /// <inheritdoc />
    public int PictureCount => _blorb?.PictureCount ?? 0;

    /// <inheritdoc />
    public int ReleaseNumber => _blorb?.ReleaseNumber ?? 0;

    /// <inheritdoc />
    public bool HasPicture(int number)
    {
        return _blorb != null && _blorb.HasResource(BlorbUsage.Picture, number);
    }

    /// <inheritdoc />
    public bool IsPlaceholder(int number)
    {
        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Picture, number))
            return false;
        return _blorb.GetResourceType(BlorbUsage.Picture, number) == "Rect";
    }

    /// <inheritdoc />
    public (int Width, int Height) GetPictureSize(int number)
    {
        if (_sizeCache.TryGetValue(number, out var cached))
            return cached;

        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Picture, number))
            return (0, 0);

        string type = _blorb.GetResourceType(BlorbUsage.Picture, number);

        if (type == "Rect")
        {
            var size = ParseRectSize(number);
            _sizeCache[number] = size;
            return size;
        }

        var bitmap = DecodePicture(number);
        if (bitmap == null)
            return (0, 0);

        var result = (bitmap.Width, bitmap.Height);
        _sizeCache[number] = result;
        return result;
    }

    /// <inheritdoc />
    public bool DrawPicture(int number, int y, int x)
    {
        // Blorb "Placeholder Pictures" — @draw_picture on Rect is an error
        if (IsPlaceholder(number))
            return false;

        var bitmap = DecodePicture(number);
        if (bitmap == null)
            return false;

        _renderer.DrawImage(x, y, bitmap, bitmap.Width, bitmap.Height);
        _renderer.Refresh();
        return true;
    }

    /// <inheritdoc />
    public bool ErasePicture(int number, int y, int x)
    {
        var (w, h) = GetPictureSize(number);
        if (w == 0 && h == 0)
            return false;

        var bg = _theme.GetColor(_theme.DefaultBackground);
        _renderer.DrawRegion(x, y, w, h, bg);
        _renderer.Refresh();
        return true;
    }

    /// <summary>
    /// Decodes a picture resource (PNG or JPEG) to an SKBitmap.
    /// Returns null for Rect placeholders or missing resources.
    /// </summary>
    private SKBitmap? DecodePicture(int number)
    {
        if (_cache.TryGetValue(number, out var cached))
            return cached;

        if (_blorb == null || !_blorb.HasResource(BlorbUsage.Picture, number))
            return null;

        if (IsPlaceholder(number))
            return null;

        byte[] data = _blorb.GetResource(BlorbUsage.Picture, number);
        var bitmap = SKBitmap.Decode(data);

        if (bitmap != null)
            _cache[number] = bitmap;

        return bitmap;
    }

    /// <summary>
    /// Parses Rect placeholder dimensions from chunk data.
    /// Blorb "Placeholder Pictures" — 4-byte width + 4-byte height, big-endian.
    /// </summary>
    private (int Width, int Height) ParseRectSize(int number)
    {
        byte[] data = _blorb!.GetResource(BlorbUsage.Picture, number);
        if (data.Length < 8)
            return (0, 0);

        int w = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        int h = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
        return (w, h);
    }

    /// <summary>
    /// Releases all cached bitmaps.
    /// </summary>
    public void Dispose()
    {
        foreach (var bitmap in _cache.Values)
            bitmap.Dispose();
        _cache.Clear();
        _sizeCache.Clear();
    }
}
