using Gondwana.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public class WinFormBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKControl _control;

    private SKImage? _currentImage;
    private SKImage? _prevToDispose;   // dispose AFTER paint finishes
    private SKRectI _sourceRect;
    private SKRect _destRect;

    public SKColor ClearColor { get; set; } = SKColors.Purple;

    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(control.Width, control.Height)
    {
        _control = control;
        _control.PaintSurface += OnPaintSurface;
        _control.Resize += (_, _) => SetDestinationSize(_control.Width, _control.Height);
    }

    public override void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        if (_control.IsDisposed || !_control.IsHandleCreated)
        {
            bufferImage.Dispose(); // avoid leaking if host is gone
            return;
        }

        if (_control.InvokeRequired)
        {
            // keep the exact object; it's just a wrapper over persistent pixels
            _control.BeginInvoke(new Action(() => Render(bufferImage, bufferRect, destRect)));
            return;
        }

        // Swap without disposing yet; paint may still be using the old image
        var old = _currentImage;
        _currentImage = bufferImage;
        if (!ReferenceEquals(old, _currentImage))
            _prevToDispose = old;

        _sourceRect = bufferRect;
        _destRect = destRect;

        _control.Invalidate();
    }

    private static SKRect ToRect(SKRectI r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(ClearColor);

        var img = _currentImage;
        if (img != null && img.Handle != IntPtr.Zero)
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
        _prevToDispose?.Dispose();
        _prevToDispose = null;
    }

    public void Dispose()
    {
        if (!_control.IsDisposed) _control.PaintSurface -= OnPaintSurface;
        _prevToDispose?.Dispose();
        _currentImage?.Dispose();
        _prevToDispose = null;
        _currentImage = null;
    }
}
