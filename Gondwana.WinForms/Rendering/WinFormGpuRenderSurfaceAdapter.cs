using Gondwana.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public sealed class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;

    // The image we’ll draw this frame (should be created/snapshotted off the SAME GRContext)
    private SKImage? _currentImage;

    // Previous image to dispose AFTER it has been drawn on the GL thread
    private SKImage? _prevToDispose;

    private SKRectI _sourceRect;
    private SKRect _destRect;

    public SKColor ClearColor { get; set; } = SKColors.Purple;

    /// <summary>
    /// The GL/Skia GPU context you MUST share with your GpuBackbuffer.
    /// </summary>
    public GRContext? GrContext { get; private set; }

    public WinFormGpuRenderSurfaceAdapter(SKGLControl gl)
        : base(gl.Width, gl.Height)
    {
        _glControl = gl;

        // Wire events
        _glControl.PaintSurface += OnPaintSurface;
        _glControl.Resize += (_, _) => SetDestinationSize(_glControl.Width, _glControl.Height);

        // On modern SkiaSharp, SKGLControl exposes GRContext; otherwise capture it in the first paint.
        GrContext = _glControl.GRContext; // may be null until first paint; we also set in OnPaintSurface
    }

    /// <summary>
    /// Render gets an SKImage from your GpuBackbuffer (ideally texture-backed off the same GRContext).
    /// </summary>
    public override void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        // If the control can't paint, dispose immediately to avoid a leak.
        if (_glControl.IsDisposed || !_glControl.IsHandleCreated)
        {
            bufferImage.Dispose();
            return;
        }

        // Swap
        var old = _currentImage;
        _currentImage = bufferImage;
        if (!ReferenceEquals(old, _currentImage) && old is not null)
            _prevToDispose = old;

        _sourceRect = bufferRect;
        _destRect = destRect;

        // Kick the GL paint; this runs on the GL thread/context
        _glControl.Invalidate();
    }

    private static SKRect ToRect(SKRectI r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        // Capture/refresh the GRContext so callers can wire the backbuffer to the same one.
        GrContext ??= _glControl.GRContext;

        var canvas = e.Surface.Canvas;

        // Clear with your configured clear color
        canvas.Clear(ClearColor);

        var img = _currentImage;
        if (img != null)
        {
            // IMPORTANT: For zero-copy, img MUST belong to this same GRContext.
            // If you hand us a raster image, Skia will upload it each frame (works, but costs bandwidth).
            var imgBoundsI = new SKRectI(0, 0, img.Width, img.Height);
            var srcI = SKRectI.Intersect(_sourceRect, imgBoundsI);

            if (!srcI.IsEmpty)
            {
                var src = ToRect(srcI);
                var dest = _destRect;

                var bounds = SKRect.Create(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
                if (!bounds.Contains(dest))
                    dest = SKRect.Intersect(dest, bounds);

                canvas.DrawImage(img, src, dest);
            }
        }

        // Safe to free the previously drawn GPU image NOW (on GL thread)
        _prevToDispose?.Dispose();
        _prevToDispose = null;

        // Optional: flush to ensure work is queued to GPU before we hand new images next frame
        _glControl.GRContext?.Flush();
    }

    public void Dispose()
    {
        if (!_glControl.IsDisposed) _glControl.PaintSurface -= OnPaintSurface;

        // These MUST be disposed on the GL thread; do a last paint if needed.
        // If you’re disposing off the UI thread at shutdown, it’s usually fine
        // because the context is being torn down; still, we null them here.
        _prevToDispose?.Dispose();
        _currentImage?.Dispose();
        _prevToDispose = null;
        _currentImage = null;
    }
}
