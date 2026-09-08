using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using ZMachine.IO;

namespace ZMachine.App;

/// <summary>
/// Custom Avalonia control that renders a SkiaSharp back buffer bitmap.
/// The SkiaRenderer draws to an SKBitmap; this control copies it to an
/// Avalonia WriteableBitmap on each frame for display.
/// </summary>
public class SkiaCanvasControl : Control
{
    private WriteableBitmap? _writeableBitmap;
    private SkiaRenderer? _renderer;

    /// <summary>
    /// Connects this control to a SkiaRenderer. The control subscribes
    /// to Refresh events and invalidates itself when the back buffer changes.
    /// </summary>
    public void Attach(SkiaRenderer renderer)
    {
        _renderer = renderer;
        renderer.OnRefresh += OnRendererRefresh;
    }

    private void OnRendererRefresh()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var backBuffer = _renderer?.BackBuffer;
        if (backBuffer == null) return;

        int w = backBuffer.Width;
        int h = backBuffer.Height;

        if (_writeableBitmap == null ||
            _writeableBitmap.PixelSize.Width != w ||
            _writeableBitmap.PixelSize.Height != h)
        {
            _writeableBitmap?.Dispose();
            _writeableBitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888,
                AlphaFormat.Premul);
        }

        using (var locked = _writeableBitmap.Lock())
        {
            var srcPixels = backBuffer.GetPixels();
            int byteCount = w * h * 4;
            unsafe
            {
                Buffer.MemoryCopy(
                    srcPixels.ToPointer(),
                    locked.Address.ToPointer(),
                    byteCount,
                    byteCount);
            }
        }

        // Scale to fill the control while maintaining aspect ratio
        double scaleX = Bounds.Width / w;
        double scaleY = Bounds.Height / h;
        double scale = Math.Min(scaleX, scaleY);

        double renderW = w * scale;
        double renderH = h * scale;
        double offsetX = (Bounds.Width - renderW) / 2;
        double offsetY = (Bounds.Height - renderH) / 2;

        var destRect = new Rect(offsetX, offsetY, renderW, renderH);
        var srcRect = new Rect(0, 0, w, h);

        context.DrawImage(_writeableBitmap, srcRect, destRect);
    }
}
