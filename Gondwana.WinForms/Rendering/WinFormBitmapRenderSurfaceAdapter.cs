using Gondwana.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public class WinFormBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKControl _control;

    private SKImage? _currentImage;
    private readonly Queue<SKImage> _toDispose = new();
    private SKRectI _sourceRect;

    //public SKColor ClearColor { get; set; } = SKColors.Black;

    private readonly SKPaint _presentPaint = new()
    {
        BlendMode = SKBlendMode.Src,
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false
    };

    private readonly SKPaint _clearPaint = new()
    {
        BlendMode = SKBlendMode.Src,
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false,
        Color = SKColors.Black
    };

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

    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
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

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        var img = _currentImage;
        if (img == null) return;

        var srcI = SKRectI.Intersect(_sourceRect, new SKRectI(0, 0, img.Width, img.Height));
        if (srcI.IsEmpty) return;

        var destI = new SKRectI(srcI.Left, srcI.Top, srcI.Right, srcI.Bottom);

        var boundsI = new SKRectI(0, 0, e.Info.Width, e.Info.Height);
        var clippedDestI = SKRectI.Intersect(destI, boundsI);
        if (clippedDestI.IsEmpty) return;

        var dx = clippedDestI.Left - destI.Left;
        var dy = clippedDestI.Top - destI.Top;
        var clippedSrcI = new SKRectI(
            srcI.Left + dx, srcI.Top + dy,
            srcI.Left + dx + clippedDestI.Width,
            srcI.Top + dy + clippedDestI.Height);

        // clear the destination patch (overwrite)
        canvas.DrawRect(clippedDestI, _clearPaint);

        // blit the updated patch (overwrite)
        canvas.DrawImage(img, clippedSrcI, clippedDestI, _presentPaint);

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