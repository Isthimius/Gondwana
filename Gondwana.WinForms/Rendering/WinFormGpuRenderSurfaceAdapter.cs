using Gondwana.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a GPU-accelerated render surface adapter for Windows Forms using OpenGL and SKGLControl.
/// </summary>
public sealed class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;
    private readonly EventHandler _resizeHandler;

    // The image we'll draw this frame (should be created/snapshotted off the SAME GRContext)
    private SKImage? _currentImage;

    // Previous image to dispose AFTER it has been drawn on the GL thread
    private SKImage? _prevToDispose;

    private SKRectI _sourceRect;
    private SKRect _destRect;

    // Tracks whether GrContext has been captured and the first-available event fired.
    private bool _grContextReady;

    // Set to 1 when the GL control is resized so OnPaintSurface can fire ResizeRequested.
    private int _pendingResize;

    /// <summary>
    /// Gets or sets the color used to clear the surface before rendering.
    /// </summary>
    public SKColor ClearColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Gets the GL/Skia GPU context that should be shared with the GpuBackbuffer.
    /// </summary>
    public GRContext? GrContext { get; private set; }

    /// <summary>
    /// Raised once, the first time the <see cref="GRContext"/> becomes available in
    /// <see cref="OnPaintSurface"/>.  Subscribe to this event to call
    /// <see cref="GpuBackbuffer.Initialize"/> on the GL thread.
    /// </summary>
    public event Action<GRContext>? GrContextFirstAvailable;

    /// <summary>
    /// Raised on the GL thread when the control has been resized and a valid
    /// <see cref="GRContext"/> is available.  The arguments are the context and the new
    /// width and height in pixels.  Subscribe to call
    /// <see cref="GpuBackbuffer.Initialize"/> with the new dimensions.
    /// </summary>
    public event Action<GRContext, int, int>? ResizeRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormGpuRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="gl">The SKGLControl to use as the render target.</param>
    public WinFormGpuRenderSurfaceAdapter(SKGLControl gl)
        : base(gl.Width, gl.Height)
    {
        _glControl = gl;

        // Store the resize handler in a field so it can be unsubscribed in Dispose().
        _resizeHandler = (_, _) =>
        {
            SetDestinationSize(_glControl.Width, _glControl.Height);
            Interlocked.Exchange(ref _pendingResize, 1);
        };

        // Wire events
        _glControl.PaintSurface += OnPaintSurface;
        _glControl.Resize += _resizeHandler;

        // On modern SkiaSharp, SKGLControl exposes GRContext; otherwise capture it in the first paint.
        GrContext = _glControl.GRContext; // may be null until first paint; we also set in OnPaintSurface
    }

    /// <summary>
    /// Presents the specified GPU buffer image to the render surface.
    /// The image should be texture-backed and created from the same GRContext for optimal zero-copy rendering.
    /// </summary>
    /// <param name="bufferImage">The GPU image to present.</param>
    /// <param name="bufferRect">The source rectangle within the buffer image.</param>
    /// <param name="destRect">The destination rectangle on the render surface.</param>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
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

        if (GrContext != null)
        {
            if (!_grContextReady)
            {
                // First time the context is available: clear any resize that happened before the
                // first paint (the Initialize call will use the current adapter dimensions).
                _grContextReady = true;
                Interlocked.Exchange(ref _pendingResize, 0);
                GrContextFirstAvailable?.Invoke(GrContext);
            }
            else if (Interlocked.Exchange(ref _pendingResize, 0) == 1)
            {
                // Subsequent resize: reinitialize the backbuffer's GPU resources on the GL thread.
                // WinForms controls can report zero size while minimized or before layout completes;
                // suppress invalid resize requests so downstream GPU initialization is not attempted
                // with non-positive dimensions.
                var width = Width;
                var height = Height;
                if (width > 0 && height > 0)
                    ResizeRequested?.Invoke(GrContext, width, height);
            }
        }

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

    /// <summary>
    /// Releases all resources used by the <see cref="WinFormGpuRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_glControl.IsDisposed)
        {
            _glControl.PaintSurface -= OnPaintSurface;
            _glControl.Resize -= _resizeHandler;
        }

        // These MUST be disposed on the GL thread; do a last paint if needed.
        // If you're disposing off the UI thread at shutdown, it's usually fine
        // because the context is being torn down; still, we null them here.
        _prevToDispose?.Dispose();
        _currentImage?.Dispose();
        _prevToDispose = null;
        _currentImage = null;
    }
}