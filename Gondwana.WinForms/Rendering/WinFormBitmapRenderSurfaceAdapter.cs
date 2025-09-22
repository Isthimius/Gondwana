using Gondwana.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public class WinFormBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKControl _control;

    private SKImage? _currentImage;
    private readonly Queue<SKImage> _toDispose = new();
    private SKRectI _sourceRect;

    public SKColor ClearColor { get; set; } = SKColors.Black;

    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(control.ClientSize.Width, control.ClientSize.Height)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        _control.PaintSurface += OnPaintSurface;
        _control.HandleCreated += OnHandleCreated;
        _control.ClientSizeChanged += OnSizeChanged;

        // If the handle already exists, schedule one initial sync
        if (_control.IsHandleCreated)
            _control.BeginInvoke((Action)RefreshDestinationSize);
    }

    private void OnHandleCreated(object? s, EventArgs e) => RefreshDestinationSize();
    private void OnSizeChanged(object? s, EventArgs e) => RefreshDestinationSize();

    public void RefreshDestinationSize()
    {
        if (_control.IsDisposed || !_control.IsHandleCreated) return;

        var sz = _control.ClientSize;                     // ← ClientSize, not Width/Height
        SetDestinationSize(sz.Width, sz.Height);          // ← base will invoke Resized
    }

    public override void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        // No UI target — dispose immediately to avoid leak
        if (_control.IsDisposed || !_control.IsHandleCreated)
        {
            bufferImage.Dispose();
            return;
        }

        // Swap into current; old one queued for disposal after paint
        var old = _currentImage;
        _currentImage = bufferImage;
        if (!ReferenceEquals(old, _currentImage) && old is not null)
            _toDispose.Enqueue(old);

        _sourceRect = bufferRect;

        _control.Invalidate();
    }

    private static SKRect ToRect(SKRectI r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var img = _currentImage;
        if (img == null) return;

        // clamp source to the image
        var srcI = SKRectI.Intersect(_sourceRect, new SKRectI(0, 0, img.Width, img.Height));
        if (srcI.IsEmpty) return;

        // recompute destination against current control size
        var cw = e.Info.Width;
        var ch = e.Info.Height;

        // 1:1 full-frame mapping (stretch); if you prefer aspect-preserving, adjust here
        SKRect dest;
        if (srcI.Left == 0 && srcI.Top == 0 && srcI.Width == img.Width && srcI.Height == img.Height)
        {
            // full-frame path
            dest = SKRect.Create(0, 0, cw, ch);
        }
        else
        {
            // dirty-rect path: scale the sub-rect proportionally to the current control size
            float scaleX = (float)cw / img.Width;
            float scaleY = (float)ch / img.Height;
            dest = SKRect.Create(
                srcI.Left * scaleX,
                srcI.Top * scaleY,
                srcI.Width * scaleX,
                srcI.Height * scaleY
            );
        }

        canvas.DrawImage(img, new SKRect(srcI.Left, srcI.Top, srcI.Right, srcI.Bottom), dest);

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();
    }

    public void Dispose()
    {
        if (!_control.IsDisposed)
        {
            _control.PaintSurface -= OnPaintSurface;
            _control.HandleCreated -= OnHandleCreated;
            _control.ClientSizeChanged -= OnSizeChanged;
        }

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();

        _currentImage?.Dispose();
        _currentImage = null;
    }
}
