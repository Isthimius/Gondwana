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
    private SKRect _destRect;

    public SKColor ClearColor { get; set; } = SKColors.Black;

    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(control.ClientSize.Width, control.ClientSize.Height)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        _control.PaintSurface += OnPaintSurface;
        _control.HandleCreated += OnHandleCreated;
        _control.SizeChanged += OnSizeChanged;
        _control.ClientSizeChanged += OnSizeChanged;
        _control.Layout += OnSizeChanged;

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
        _destRect = destRect;

        _control.Invalidate();
    }

    private static SKRect ToRect(SKRectI r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        
        var img = _currentImage;
        if (img != null)
        {
            var srcI = SKRectI.Intersect(_sourceRect, new SKRectI(0, 0, img.Width, img.Height));
            if (!srcI.IsEmpty)
            {
                var src = ToRect(srcI);
                var dest = _destRect;
                var bounds = SKRect.Create(e.Info.Width, e.Info.Height);

                if (!bounds.Contains(dest))
                    dest = SKRect.Intersect(dest, bounds);

                canvas.DrawImage(img, src, dest);
            }
        }

        // safe to free the previously drawn wrapper now
        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();
    }

    public void Dispose()
    {
        if (!_control.IsDisposed)
        {
            _control.PaintSurface -= OnPaintSurface;
            _control.HandleCreated -= OnHandleCreated;
            _control.SizeChanged -= OnSizeChanged;
            _control.ClientSizeChanged -= OnSizeChanged;
            _control.Layout -= OnSizeChanged;
        }

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();

        _currentImage?.Dispose();
        _currentImage = null;
    }
}
