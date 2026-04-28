using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a GPU-accelerated render surface adapter for Windows Forms using OpenGL and SKGLControl.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Option A — GL-thread rendering:</strong>
/// When a <see cref="RenderSurfaceHostBase"/> is registered via <see cref="SetHost"/>, all scene
/// rendering and presentation are driven from within <c>PaintSurface</c> on the GL thread.  A
/// <see cref="System.Windows.Forms.Timer"/> periodically calls <see cref="SKGLControl.Invalidate"/>
/// to sustain the paint loop.  The engine's background render loop skips GPU-rendered surfaces
/// entirely (see <see cref="GpuBackbuffer.IsGlThreadRendered"/>).
/// </para>
/// <para>
/// If <see cref="SetHost"/> has not been called, the adapter falls back to the legacy
/// <see cref="Present"/> path where the engine's background thread renders to a CPU surface and
/// uploads it to the GPU inside <c>PaintSurface</c>.
/// </para>
/// </remarks>
public sealed class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;
    private readonly EventHandler _resizeHandler;

    // ── Legacy (non-host) path ───────────────────────────────────────────────
    // The image we'll draw this frame (should be created/snapshotted off the SAME GRContext)
    private SKImage? _currentImage;

    // Previous image to dispose AFTER it has been drawn on the GL thread
    private SKImage? _prevToDispose;

    private SKRectI _sourceRect;
    private SKRect _destRect;

    // ── Option A (GL-thread) path ────────────────────────────────────────────
    private RenderSurfaceHostBase? _host;
    private GpuBackbuffer? _gpuBackbuffer;
    private readonly System.Windows.Forms.Timer _renderTimer = new();

    // Tracks the last TargetFps value that was applied to the timer so we can detect changes.
    private int _appliedTargetFps;

    // Tracks the last VSync value applied to the GL control so we can detect changes.
    // Null means "not yet applied" and forces an apply on the first PaintSurface call.
    private bool? _appliedVSync;

    // ── Shared state ─────────────────────────────────────────────────────────
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
    /// Refreshes the destination size based on the current client size of the GL control.
    /// </summary>
    public void RefreshDestinationSize()
    {
        if (_glControl.IsDisposed || !_glControl.IsHandleCreated) return;

        var sz = _glControl.ClientSize;
        if (sz.Width > 0 && sz.Height > 0)
        {
            SetDestinationSize(sz.Width, sz.Height);
            Interlocked.Exchange(ref _pendingResize, 1);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormGpuRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="gl">The SKGLControl to use as the render target.</param>
    public WinFormGpuRenderSurfaceAdapter(SKGLControl gl)
        : base(gl.Width, gl.Height)
    {
        _glControl = gl;

        // Store the resize handler in a field so it can be unsubscribed in Dispose().
        // WinForms can transiently report 0×0 during minimize/layout; avoid committing that size
        // so downstream resize code (which divides by the previous width/height) never observes a
        // zero denominator.  We skip both the dimension update and the pending-resize flag when the
        // control reports non-positive dimensions.
        _resizeHandler = (_, _) =>
        {
            var width = _glControl.Width;
            var height = _glControl.Height;

            if (width > 0 && height > 0)
            {
                SetDestinationSize(width, height);
                Interlocked.Exchange(ref _pendingResize, 1);
            }
        };

        // Wire events
        _glControl.PaintSurface += OnPaintSurface;
        _glControl.Resize += _resizeHandler;

        // On modern SkiaSharp, SKGLControl exposes GRContext; otherwise capture it in the first paint.
        GrContext = _glControl.GRContext; // may be null until first paint; we also set in OnPaintSurface
    }

    /// <summary>
    /// Registers the <see cref="RenderSurfaceHostBase"/> whose scene this adapter should render
    /// and starts the GL paint loop.
    /// </summary>
    /// <remarks>
    /// After this call all rendering is driven from <c>PaintSurface</c> on the GL thread.
    /// A <see cref="System.Windows.Forms.Timer"/> fires at approximately
    /// <paramref name="targetFps"/> frames per second and calls
    /// <see cref="SKGLControl.Invalidate"/> to sustain the loop.  The initial
    /// <paramref name="targetFps"/> value is written to
    /// <see cref="GpuBackbuffer.TargetFps"/> when the host's backbuffer is a
    /// <see cref="GpuBackbuffer"/>; subsequently, mutating
    /// <see cref="GpuBackbuffer.TargetFps"/> directly is the preferred way to change the
    /// frame rate at run time.
    /// </remarks>
    /// <param name="host">The render surface host to render each frame.</param>
    /// <param name="targetFps">
    /// Initial target frame rate for the render timer.  Defaults to 60 fps.  Actual frame rate is
    /// bounded by the GL driver and monitor refresh rate.  Set to <c>0</c> for uncapped.
    /// </param>
    public void SetHost(RenderSurfaceHostBase host, int targetFps = 60)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        // Cache the GpuBackbuffer so the timer tick and OnPaintSurface can read its settings.
        _gpuBackbuffer = _host.Backbuffer as GpuBackbuffer;

        // Seed TargetFps on the backbuffer from the parameter (0 = uncapped).
        int safeFps = Math.Max(0, targetFps);
        if (_gpuBackbuffer != null)
            _gpuBackbuffer.TargetFps = safeFps;

        int intervalMs = TargetFpsToIntervalMs(safeFps);
        _appliedTargetFps = safeFps;

        _renderTimer.Interval = intervalMs;
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        // Lazily sync TargetFps → timer interval whenever the backbuffer value changes.
        int desiredFps = _gpuBackbuffer?.TargetFps ?? _appliedTargetFps;
        if (desiredFps != _appliedTargetFps)
        {
            _renderTimer.Interval = TargetFpsToIntervalMs(desiredFps);
            _appliedTargetFps = desiredFps;
        }

        _glControl.Invalidate();
    }

    private static int TargetFpsToIntervalMs(int fps) =>
        fps <= 0 ? 1 : Math.Max(1, (int)Math.Round(1000.0 / fps));

    /// <summary>
    /// Presents the specified GPU buffer image to the render surface (legacy path).
    /// Used when <see cref="SetHost"/> has not been called.
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
        // Lazily sync VSync → GLControl.VSync whenever the backbuffer value changes.
        if (_gpuBackbuffer != null)
        {
            bool desiredVSync = _gpuBackbuffer.VSync;
            if (_appliedVSync != desiredVSync)
            {
                _glControl.VSync = desiredVSync;
                _appliedVSync = desiredVSync;
            }
        }

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

        canvas.Clear(ClearColor);

        if (_host != null)
        {
            // ── Option A: render + blit entirely on the GL thread ────────────
            // GlRenderAndSnapshot drives RenderToBackbuffer on the GPU surface then returns a
            // lightweight GPU-backed snapshot.  Both the snapshot texture and e.Surface share the
            // same GRContext, so DrawImage is a zero-copy GPU blit.
            using var img = _host.GlRenderAndSnapshot();
            if (img != null)
            {
                var dst = SKRect.Create(0, 0,
                    e.BackendRenderTarget.Width,
                    e.BackendRenderTarget.Height);
                canvas.DrawImage(img, dst);
            }
        }
        else
        {
            // ── Legacy path: draw image set by Present() ─────────────────────
            // IMPORTANT: For zero-copy, img MUST belong to this same GRContext.
            // If you hand us a raster image, Skia will upload it each frame (works, but costs bandwidth).
            var img = _currentImage;
            if (img != null)
            {
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
        }

        // Optional: flush to ensure work is queued to GPU before we hand new images next frame
        _glControl.GRContext?.Flush();

        // Record the completed frame so the engine's CPS sampler can compute actual GPU FPS.
        _gpuBackbuffer?.RecordFrame();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="WinFormGpuRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        _renderTimer.Stop();
        _renderTimer.Dispose();

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