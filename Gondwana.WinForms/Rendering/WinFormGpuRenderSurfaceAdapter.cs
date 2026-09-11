using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a GPU-accelerated render surface adapter for Windows Forms using OpenGL and SKGLControl.
/// </summary>
/// <remarks>
/// All scene rendering and presentation are driven from within <c>PaintSurface</c> on the GL thread
/// <c>Invalidate()</c> is posted to the UI thread via <see cref="Engine.UiDispatcher"/>
/// at the end of each <c>Engine.DoForegroundTasks</c> cycle (via <c>Engine.AfterFrameRender</c>), so
/// the paint loop stays in lockstep with the engine's own frame rate.  The engine's background render
/// loop skips GPU-rendered surfaces entirely (see <see cref="GpuBackbuffer.IsGlThreadRendered"/>).
/// </remarks>
public sealed class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;
    private readonly Control _presentationControl;
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

    // Set to 1 when the presentation area is resized so OnPaintSurface can fire ResizeRequested.
    private int _pendingResize;

    // Set to 1 while an invalidate callback is queued on the UI dispatcher. The callback clears
    // the flag whether or not WinForms ultimately produces a paint, so a zero-sized presentation
    // cannot permanently latch rendering off.
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
    /// width and height in adapter pixels. This event is for presentation resources;
    /// it must not be used to resize the logical Backbuffer.
    /// </summary>
    public event Action<GRContext, int, int>? ResizeRequested;

    /// <summary>
    /// Refreshes the destination size based on the current client size of the presentation control.
    /// A non-positive size marks presentation as unavailable without resizing the logical Backbuffer.
    /// </summary>
    public void RefreshDestinationSize()
    {
        if (_presentationControl.IsDisposed || !_presentationControl.IsHandleCreated) return;

        var sz = _presentationControl.ClientSize;
        SetDestinationSize(sz.Width, sz.Height);

        if (sz.Width > 0 && sz.Height > 0)
            Interlocked.Exchange(ref _pendingResize, 1);
        else
            Interlocked.Exchange(ref _pendingResize, 0);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormGpuRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="gl">The SKGLControl to use as the render target and presentation control.</param>
    public WinFormGpuRenderSurfaceAdapter(SKGLControl gl)
        : this(gl, gl)
    {
    }

    /// <summary>
    /// Initializes an adapter whose physical GL control may be kept at a non-zero minimum size
    /// while a separate control supplies the actual presentation dimensions.
    /// </summary>
    internal WinFormGpuRenderSurfaceAdapter(SKGLControl gl, Control presentationControl)
        : base(
            Math.Max(1, presentationControl.ClientSize.Width),
            Math.Max(1, presentationControl.ClientSize.Height),
            presentationControl.ClientSize.Width > 0 && presentationControl.ClientSize.Height > 0)
    {
        _glControl = gl ?? throw new ArgumentNullException(nameof(gl));
        _presentationControl = presentationControl ?? throw new ArgumentNullException(nameof(presentationControl));

        // Track the actual presentation control rather than the GL drawable. The wrapper may keep
        // the drawable at 1×1 while its own client area is 0×0 so OpenTK/GLFW never receives a
        // zero-sized native surface.
        _resizeHandler = (_, _) => RefreshDestinationSize();

        // Wire events
        _glControl.PaintSurface += OnPaintSurface;
        _presentationControl.Resize += _resizeHandler;

        // On modern SkiaSharp, SKGLControl exposes GRContext; otherwise capture it in the first paint.
        GrContext = _glControl.GRContext; // may be null until first paint; we also set in OnPaintSurface
    }

    /// <summary>
    /// Registers the <see cref="RenderSurfaceHostBase"/> whose scene this adapter should render
    /// and wires the GL paint loop to the engine's foreground cycle.
    /// </summary>
    /// <remarks>
    /// After this call all rendering is driven from <c>PaintSurface</c> on the GL thread.
    /// <c>Invalidate()</c> is posted to the UI thread via <see cref="Engine.UiDispatcher"/> at
    /// the end of each <c>Engine.DoForegroundTasks</c> call (via <c>Engine.AfterFrameRender</c>),
    /// so the frame rate is governed entirely by
    /// <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>.
    /// </remarks>
    /// <param name="host">The render surface host to render each frame.</param>
    public void SetHost(RenderSurfaceHostBase host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        // Cache the GpuBackbuffer so OnPaintSurface can read its settings.
        _gpuBackbuffer = _host.Backbuffer as GpuBackbuffer;

        // Invalidate the GL control on the UI thread after every engine foreground cycle.
        // _pendingInvalidate tracks the queued dispatcher callback, not PaintSurface itself.
        // Invalidate() is only a paint request and can be dropped while the presentation has no
        // area, so tying this latch to PaintSurface can permanently starve rendering after restore.
        _afterFrameRenderHandler = QueueInvalidate;
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

    private void QueueInvalidate()
    {
        if (_glControl.IsDisposed || _presentationControl.IsDisposed) return;
        if (Interlocked.CompareExchange(ref _pendingInvalidate, 1, 0) != 0) return;

        Engine.Instance.UiDispatcher!.Post(() =>
        {
            try
            {
                if (!_glControl.IsDisposed
                    && !_presentationControl.IsDisposed
                    && _presentationControl.ClientSize.Width > 0
                    && _presentationControl.ClientSize.Height > 0)
                {
                    _glControl.Invalidate();
                }
            }
            finally
            {
                // The dispatcher callback completed. Do not wait for PaintSurface: WinForms is
                // allowed to coalesce or discard an invalidation, especially at zero size.
                Interlocked.Exchange(ref _pendingInvalidate, 0);
            }
        });
    }

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
                // Notify consumers about presentation resource resize on the GL thread.
                // WinForms controls can report zero size while minimized or before layout completes;
                // suppress invalid resize requests so downstream GPU initialization is not attempted
                // with non-positive dimensions.
                var width = Width;
                var height = Height;
                if (width > 0 && height > 0)
                    ResizeRequested?.Invoke(GrContext, width, height);
            }
        }

        if (GrContext != null) _gpuBackbuffer?.EnsureInitialized(GrContext);

        var canvas = e.Surface.Canvas;

        // The physical GL drawable is intentionally kept at least 1×1, even when the wrapper has
        // no presentation area. Avoid doing scene work for that clipped placeholder surface.
        if (Width <= 0 || Height <= 0)
        {
            canvas.Clear(ClearColor);
            return;
        }

        // Render + blit entirely on the GL thread.
        // GlRenderAndSnapshot drives RenderToBackbuffer on the GPU surface then returns a
        // lightweight GPU-backed snapshot.  Both the snapshot texture and e.Surface share the
        // same GRContext, so DrawImage is a zero-copy GPU blit.
        if (_host != null)
        {
            using var img = _host.GlRenderAndSnapshot();
            if (img != null)
            {
                DrawImage(canvas, img, ClearColor);
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
            _glControl.PaintSurface -= OnPaintSurface;

        if (!_presentationControl.IsDisposed)
            _presentationControl.Resize -= _resizeHandler;
    }
}
