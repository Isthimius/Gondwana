using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a GPU-accelerated render surface adapter for Windows Forms using OpenGL and SKGLControl.
/// </summary>
/// <remarks>
/// All scene rendering and presentation are driven from within <c>PaintSurface</c> on the GL thread
/// (Option A).  <c>Invalidate()</c> is called on the UI thread at the end of each
/// <c>Engine.DoForegroundTasks</c> cycle (via <c>Engine.AfterFrameRender</c>), so the paint loop
/// stays in lockstep with the engine's own frame rate.  The engine's background render loop skips
/// GPU-rendered surfaces entirely (see <see cref="GpuBackbuffer.IsGlThreadRendered"/>).
/// </remarks>
public sealed class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;
    private readonly EventHandler _resizeHandler;

    // ── GL-thread path ───────────────────────────────────────────────────────
    private RenderSurfaceHostBase? _host;
    private GpuBackbuffer? _gpuBackbuffer;
    private Action? _afterFrameRenderHandler;

    // Tracks the last VSync value applied to the GL control so we can detect changes.
    // Null means "not yet applied" and forces an apply on the first PaintSurface call.
    private bool? _appliedVSync;

    // ── Shared state ─────────────────────────────────────────────────────────
    // Tracks whether GrContext has been captured and the first-available event fired.
    private bool _grContextReady;

    // Set to 1 when the GL control is resized so OnPaintSurface can fire ResizeRequested.
    private int _pendingResize;

    // Set to 1 when a BeginInvoke(Invalidate) is already queued; prevents queuing more than one
    // at a time when the engine cycle rate exceeds the GPU render rate.
    private int _pendingInvalidate;

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
    /// and wires the GL paint loop to the engine's foreground cycle.
    /// </summary>
    /// <remarks>
    /// After this call all rendering is driven from <c>PaintSurface</c> on the GL thread.
    /// <c>Invalidate()</c> is called on the UI thread at the end of each
    /// <c>Engine.DoForegroundTasks</c> call (via <c>Engine.AfterFrameRender</c>), so the frame
    /// rate is governed entirely by <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>.
    /// </remarks>
    /// <param name="host">The render surface host to render each frame.</param>
    public void SetHost(RenderSurfaceHostBase host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        // Cache the GpuBackbuffer so OnPaintSurface can read its settings.
        _gpuBackbuffer = _host.Backbuffer as GpuBackbuffer;

        // Invalidate the GL control on the UI thread after every engine foreground cycle.
        // Use _pendingInvalidate to ensure at most one BeginInvoke is queued: when the engine
        // cycle rate exceeds the GPU render rate, excess calls are dropped rather than piling
        // up in the message queue.  The flag is cleared at the start of each paint.
        _afterFrameRenderHandler = () =>
        {
            if (!_glControl.IsDisposed && _glControl.IsHandleCreated
                && Interlocked.CompareExchange(ref _pendingInvalidate, 1, 0) == 0)
                _glControl.BeginInvoke((Action)_glControl.Invalidate);
        };
        Engine.Instance.AfterFrameRender += _afterFrameRenderHandler;
    }

    /// <summary>
    /// Not used in the GL-thread path.  Rendering is driven by <see cref="SetHost"/> and
    /// <c>SKGLControl.PaintSurface</c>.  The image is disposed immediately to avoid a leak.
    /// </summary>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        bufferImage.Dispose();
    }

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)    {
        // Clear the pending-invalidate flag so the next AfterFrameRender can queue a new one.
        Interlocked.Exchange(ref _pendingInvalidate, 0);

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

        // Render + blit entirely on the GL thread.
        // GlRenderAndSnapshot drives RenderToBackbuffer on the GPU surface then returns a
        // lightweight GPU-backed snapshot.  Both the snapshot texture and e.Surface share the
        // same GRContext, so DrawImage is a zero-copy GPU blit.
        if (_host != null)
        {
            using var img = _host.GlRenderAndSnapshot();
            if (img != null)
            {
                // DrawImage covers the full render target, so no pre-clear is needed.
                // Clearing the window surface before blitting would cause a black flash
                // that is visible when VSync is off (the monitor may scan between the
                // clear and the blit, seeing the cleared back buffer).
                var dst = SKRect.Create(0, 0,
                    e.BackendRenderTarget.Width,
                    e.BackendRenderTarget.Height);
                canvas.DrawImage(img, dst);
            }
            else
            {
                canvas.Clear(ClearColor);
            }
        }
        else
        {
            canvas.Clear(ClearColor);
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
        if (_afterFrameRenderHandler != null)
        {
            Engine.Instance.AfterFrameRender -= _afterFrameRenderHandler;
            _afterFrameRenderHandler = null;
        }

        if (!_glControl.IsDisposed)
        {
            _glControl.PaintSurface -= OnPaintSurface;
            _glControl.Resize -= _resizeHandler;
        }
    }
}