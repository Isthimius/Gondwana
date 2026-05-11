using System.Drawing;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;
using SkiaSharp;
using Gondwana;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Provides a fully GPU-rendered backbuffer implementation using SkiaSharp and OpenGL.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture (GL-thread rendering):</strong>
/// All rendering operations (tile drawing, canvas operations) and presentation happen on the GL
/// thread, driven by <c>WinFormGpuRenderSurfaceAdapter</c> from within
/// <c>SKGLControl.PaintSurface</c>.  The engine's background loop skips this surface entirely
/// (see <see cref="IsGlThreadRendered"/>).  No CPU↔GPU data transfer occurs for the backbuffer
/// contents; tiles are rasterised directly into an off-screen GPU render target and then blitted
/// to the window surface in a single GPU draw call.
/// </para>
/// <para>
/// Before the first <see cref="Initialize"/> call the backbuffer uses a temporary CPU raster
/// surface (identical to <see cref="BitmapBackbuffer"/>) so that the host can always be in a
/// valid state.  Once a <see cref="GRContext"/> becomes available on the GL thread,
/// <see cref="Initialize"/> replaces that with a proper GPU surface.
/// </para>
/// <para>
/// <strong>Thread safety:</strong> After <see cref="Initialize"/> has been called, all methods and
/// properties that access or mutate GL state must be invoked from the GL thread (the thread on
/// which the <see cref="GRContext"/> is current).  The engine loop guarantees this by checking
/// <see cref="IsGlThreadRendered"/> before touching the backbuffer.  The simple configuration
/// properties <see cref="VSync"/>, <see cref="MsaaSampleCount"/>, and <see cref="TargetFps"/> are
/// exempt from this restriction: they only write to plain fields and may be set from any thread
/// (for example, from <see cref="Gondwana.Configuration.EngineConfiguration"/> setters running on
/// the engine background thread).
/// </para>
/// </remarks>
public class GpuBackbuffer : BackbufferBase
{
    // Surface state.  All access MUST occur on the GL thread after Initialize() is called.
    private SKBitmap? _cpuBitmap;   // temporary CPU surface used before GRContext is ready
    private SKSurface? _surface;
    private bool _disposed;

    private int _targetFps = 60;
    private int _msaaSampleCount = 1;

    // Frame counter used to compute the actual rendered FPS.
    // Incremented on the GL thread by RecordFrame(); consumed atomically by the engine's CPS sampler.
    private long _frameCount;

    /// <summary>
    /// Gets or sets the target frame rate associated with this GPU backbuffer.
    /// </summary>
    /// <remarks>
    /// This value is kept in sync with <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>
    /// (which propagates its value to all registered GPU backbuffers).  The engine's own foreground
    /// cycle — not the backbuffer — is responsible for throttling the frame rate; see
    /// <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/> for details.
    /// </remarks>
    /// <value>
    /// The desired frames per second.  Negative values are clamped to zero.  The default is 60.
    /// </value>
    public int TargetFps
    {
        get => _targetFps;
        set => _targetFps = value < 0 ? 0 : value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether vertical synchronisation (vsync) is enabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property only applies to <see cref="GpuBackbuffer"/>; other backbuffer types ignore it.
    /// </para>
    /// <para>
    /// This value is kept in sync with <see cref="Gondwana.Configuration.EngineConfiguration.VSync"/>
    /// (which propagates its value to all registered GPU backbuffers).  The value is read by the
    /// platform adapter (e.g. <c>WinFormGpuRenderSurfaceAdapter</c>) at the start of each
    /// <c>PaintSurface</c> call and applied to the underlying <c>GLControl.VSync</c> property when
    /// it changes.  Enabling vsync prevents screen tearing but caps the frame rate to the monitor
    /// refresh rate.  Disabling vsync allows higher frame rates at the cost of potential tearing.
    /// </para>
    /// </remarks>
    /// <value>
    /// <see langword="true"/> to synchronise presentation with the monitor refresh; otherwise
    /// <see langword="false"/>.  The default is <see langword="true"/>.
    /// </value>
    public bool VSync { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of MSAA (multisample anti-aliasing) samples used when creating the
    /// GPU render-target surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property only applies to <see cref="GpuBackbuffer"/>; other backbuffer types ignore it.
    /// </para>
    /// <para>
    /// This value is kept in sync with
    /// <see cref="Gondwana.Configuration.EngineConfiguration.MsaaSampleCount"/> (which propagates
    /// its value to all registered GPU backbuffers).  A value of <c>1</c> disables MSAA.  Common
    /// values are <c>2</c>, <c>4</c>, or <c>8</c>, subject to hardware support.
    /// </para>
    /// <para>
    /// Changing this property on an already-initialized backbuffer takes effect the next time
    /// <see cref="Initialize"/> is called (e.g. on the next window resize), because the GPU
    /// render-target surface must be recreated with the new sample count.
    /// </para>
    /// <para>
    /// If the requested sample count is not supported by the hardware or driver,
    /// <see cref="Initialize"/> will automatically fall back to a sample count of <c>1</c>
    /// (no MSAA) so the surface is always valid.
    /// </para>
    /// </remarks>
    /// <value>
    /// The MSAA sample count.  Values less than <c>1</c> are clamped to <c>1</c>.  The default is <c>1</c>.
    /// </value>
    public int MsaaSampleCount
    {
        get => _msaaSampleCount;
        set => _msaaSampleCount = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuBackbuffer"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The initial width of the backbuffer in pixels.</param>
    /// <param name="height">The initial height of the backbuffer in pixels.</param>
    public GpuBackbuffer(int width, int height)
        : base(width, height)
    {
        // Apply the current engine configuration defaults for the settings this backbuffer
        // explicitly copies at construction time: TargetFps, VSync, MsaaSampleCount,
        // GpuDirtyRectanglesEnabled, and ContinuousRender.
        var config = Engine.Instance.Configuration;
        TargetFps = config.TargetFPS;
        VSync = config.VSync;
        MsaaSampleCount = config.MsaaSampleCount;
        GpuDirtyRectanglesEnabled = config.GpuDirtyRectangles;
        ContinuousRender = config.ContinuousGpuRender;

        CreateCpuSurface(width, height);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns <see langword="true"/>.  The engine loop will skip this surface; the
    /// platform adapter drives rendering from <c>SKGLControl.PaintSurface</c>.
    /// </remarks>
    public override bool IsGlThreadRendered => true;

    /// <summary>
    /// Creates (or recreates) the GPU render-target surface for this backbuffer.
    /// </summary>
    /// <remarks>
    /// Called from the GL thread via the adapter's <c>GrContextFirstAvailable</c> and
    /// <c>ResizeRequested</c> events.  Replaces the temporary CPU raster surface with a
    /// hardware-accelerated off-screen render target backed by <paramref name="grContext"/>.
    /// </remarks>
    /// <param name="grContext">The active Skia GPU context.  Must not be <see langword="null"/>.</param>
    /// <param name="width">The new surface width in pixels.</param>
    /// <param name="height">The new surface height in pixels.</param>
    public void Initialize(GRContext grContext, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        if (_disposed) return;

        DisposeSurface();
        CreateGpuSurface(grContext, width, height);
        UpdateSize(width, height);

        // Set canvas into a known state for the first frame on the new surface.
        BeginFrame();
    }

    /// <summary>
    /// No-op for <see cref="GpuBackbuffer"/>: resize is driven by <see cref="Initialize"/> which is
    /// called from the GL thread via the adapter's <c>ResizeRequested</c> event.
    /// </summary>
    protected internal override void RequestResize(int width, int height) { }

    /// <summary>
    /// Gets the SkiaSharp canvas for drawing operations.
    /// </summary>
    public override SKCanvas Canvas => _surface?.Canvas
        ?? throw new InvalidOperationException($"{nameof(GpuBackbuffer)} surface is not initialized.");

    /// <summary>
    /// Prepares the backbuffer canvas for a new rendering frame.
    /// </summary>
    protected internal override void BeginFrame()
    {
        if (_disposed || _surface is null) return;
        var c = _surface.Canvas;
        c.RestoreToCount(1);
        c.Save();
        c.ResetMatrix();
        c.ClipRect(new SKRect(0, 0, Width, Height));
    }

    /// <summary>
    /// Completes the current frame and flushes all pending drawing operations.
    /// </summary>
    protected internal override void EndFrame()
    {
        if (_disposed || _surface is null) return;
        _surface.Flush();
    }

    /// <summary>
    /// Draws a single tile frame to the backbuffer at the specified screen location.
    /// </summary>
    /// <param name="tile">The tile to render.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates.</param>
    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image is null || _surface is null) return;
        _surface.Canvas.DrawImage(image, destRectScreen.ToSKRect());
    }

    /// <summary>
    /// Creates an immutable snapshot of the current backbuffer contents.
    /// </summary>
    /// <remarks>
    /// For the GPU surface this returns a lightweight GPU-backed image that shares the underlying
    /// texture.  The caller must dispose the returned image after consuming it (typically within
    /// the same <c>PaintSurface</c> call).
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the backbuffer has been disposed.</exception>
    protected internal override SKImage Snapshot()
    {
        if (_disposed || _surface is null)
            throw new ObjectDisposedException(nameof(GpuBackbuffer));

        return _surface.Snapshot();
    }

    // ── GPU dirty-rectangle feature flags ────────────────────────────────────

    /// <summary>
    /// Gets or sets a value indicating whether partial GPU redraws via dirty-rectangle
    /// tracking are enabled for this backbuffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="false"/> (the default) the GPU path always clears and redraws the
    /// full surface on every paint callback — identical to the pre-dirty-rect behaviour.
    /// </para>
    /// <para>
    /// Set to <see langword="true"/> to enable partial GPU redraws.  Only the screen regions
    /// covered by the current <see cref="GpuDirtyFrame"/> are cleared and repainted; unchanged
    /// pixels remain intact on the GPU surface.  Enable this flag after performance and
    /// correctness validation in your target environment.
    /// </para>
    /// <para>
    /// This value is kept in sync with
    /// <see cref="Gondwana.Configuration.EngineConfiguration.GpuDirtyRectangles"/> (which
    /// propagates its value to all registered GPU backbuffers).
    /// </para>
    /// </remarks>
    public bool GpuDirtyRectanglesEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether a GPU repaint is requested on every engine
    /// cycle regardless of dirty-rectangle state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/> (the default) the adapter posts an invalidation request
    /// after every <c>AfterFrameRender</c> event, preserving the original behaviour.
    /// </para>
    /// <para>
    /// Set to <see langword="false"/> to suppress repaint requests when no dirty regions
    /// exist, reducing GPU load during idle frames.  Only meaningful when
    /// <see cref="GpuDirtyRectanglesEnabled"/> is also <see langword="true"/>.
    /// </para>
    /// <para>
    /// This value is kept in sync with
    /// <see cref="Gondwana.Configuration.EngineConfiguration.ContinuousGpuRender"/> (which
    /// propagates its value to all registered GPU backbuffers).
    /// </para>
    /// </remarks>
    public bool ContinuousRender { get; set; } = true;

    // ── Dirty-frame slot (engine thread → GL thread) ─────────────────────────

    // Holds the latest uncommitted dirty-frame snapshot.  Merges successive engine-side
    // publishes so that slow GL paints never lose dirty regions.
    private GpuDirtyFrame _pendingDirtyFrame = GpuDirtyFrame.Empty;
    private readonly object _pendingDirtyFrameLock = new();

    /// <summary>
    /// <see langword="true"/> when at least one non-empty <see cref="GpuDirtyFrame"/> has
    /// been published but not yet consumed by the GL thread.
    /// </summary>
    public bool HasNewDirtyFrame
    {
        get { lock (_pendingDirtyFrameLock) return !_pendingDirtyFrame.IsEmpty; }
    }

    /// <summary>
    /// Merges <paramref name="frame"/> into the pending dirty-frame slot.
    /// </summary>
    /// <remarks>
    /// Must be called on the engine thread.  Thread-safe with respect to
    /// <see cref="ConsumeDirtyFrame"/> running concurrently on the GL thread.
    /// </remarks>
    internal void PublishDirtyFrame(GpuDirtyFrame frame)
    {
        lock (_pendingDirtyFrameLock)
            _pendingDirtyFrame = _pendingDirtyFrame.MergeWith(frame);
    }

    /// <summary>
    /// Atomically retrieves and resets the pending dirty frame, returning
    /// <see cref="GpuDirtyFrame.Empty"/> if no new frame has been published.
    /// </summary>
    /// <remarks>
    /// Must be called on the GL thread.  Thread-safe with respect to
    /// <see cref="PublishDirtyFrame"/> running concurrently on the engine thread.
    /// </remarks>
    internal GpuDirtyFrame ConsumeDirtyFrame()
    {
        lock (_pendingDirtyFrameLock)
        {
            var frame = _pendingDirtyFrame;
            _pendingDirtyFrame = GpuDirtyFrame.Empty;
            return frame;
        }
    }

    // ── Rendering telemetry ───────────────────────────────────────────────────

    private long _skippedFrameCount;
    private long _dirtyRectFrameCount;
    private long _fullRedrawFrameCount;

    /// <summary>Records that a GPU paint callback was suppressed (no dirty regions, no continuous render).</summary>
    public void RecordSkippedFrame()    => Interlocked.Increment(ref _skippedFrameCount);

    /// <summary>Records that the GL thread rendered only dirty rectangles for this frame.</summary>
    internal void RecordDirtyRectFrame()  => Interlocked.Increment(ref _dirtyRectFrameCount);

    /// <summary>Records that the GL thread performed a full-surface redraw for this frame.</summary>
    internal void RecordFullRedrawFrame() => Interlocked.Increment(ref _fullRedrawFrameCount);

    /// <summary>
    /// Returns a point-in-time snapshot of rendering telemetry counters and atomically resets
    /// all counters to zero.
    /// </summary>
    public GpuRenderTelemetry ConsumeTelemetry() => new(
        Interlocked.Exchange(ref _skippedFrameCount,    0),
        Interlocked.Exchange(ref _dirtyRectFrameCount,  0),
        Interlocked.Exchange(ref _fullRedrawFrameCount, 0));

    // ── Actual FPS tracking ──────────────────────────────────────────────────

    /// <summary>
    /// Records that one frame has been rendered.  Called by the platform adapter on every
    /// <c>PaintSurface</c> callback so the engine can compute the actual rendered FPS.
    /// </summary>
    public void RecordFrame() => Interlocked.Increment(ref _frameCount);

    /// <summary>
    /// Returns the number of frames recorded since the last call and atomically resets the
    /// counter to zero.  Called by the engine's CPS sampler on the background thread.
    /// </summary>
    internal long ConsumeFrameCount() => Interlocked.Exchange(ref _frameCount, 0);

    // ── Surface creation helpers ─────────────────────────────────────────────

    private void CreateGpuSurface(GRContext grContext, int width, int height)
    {
        // Rgba8888 / Premul is the natural format for an OpenGL render target.
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(grContext, budgeted: true, info, _msaaSampleCount);

        // SKSurface.Create returns null when the requested MSAA sample count is not supported
        // by the hardware or driver.  Fall back to no MSAA (sample count 1) so the surface
        // is always valid after Initialize() completes.
        if (_surface is null && _msaaSampleCount > 1)
            _surface = SKSurface.Create(grContext, budgeted: true, info, sampleCount: 1);

        if (_surface is null)
            throw new InvalidOperationException(
                $"Failed to create a {nameof(GpuBackbuffer)} GPU surface ({width}x{height}). " +
                "The GRContext may be invalid or the pixel format is not renderable.");
    }

    private void CreateCpuSurface(int width, int height)
    {
        // Bgra8888 matches the native Windows GDI pixel format; used only before
        // GRContext becomes available so rendering is always valid from frame one.
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _cpuBitmap = new SKBitmap(info);
        _surface = SKSurface.Create(info, _cpuBitmap.GetPixels(), _cpuBitmap.Info.RowBytes);
    }

    private void DisposeSurface()
    {
        _surface?.Dispose();
        _surface = null;
        _cpuBitmap?.Dispose();
        _cpuBitmap = null;
    }

    /// <summary>
    /// Releases all resources used by this <see cref="GpuBackbuffer"/>.
    /// </summary>
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose();
        DisposeSurface();
    }
}

/// <summary>
/// A point-in-time snapshot of GPU rendering telemetry counters consumed from a
/// <see cref="GpuBackbuffer"/> via <see cref="GpuBackbuffer.ConsumeTelemetry"/>.
/// </summary>
public readonly struct GpuRenderTelemetry
{
    /// <summary>
    /// Number of GPU paint callbacks that were suppressed because there were no dirty regions
    /// and <see cref="GpuBackbuffer.ContinuousRender"/> was <see langword="false"/>.
    /// </summary>
    public long SkippedFrames { get; }

    /// <summary>
    /// Number of GPU frames where only dirty rectangles were rendered
    /// (partial redraw via <see cref="GpuBackbuffer.GpuDirtyRectanglesEnabled"/>).
    /// </summary>
    public long DirtyRectFrames { get; }

    /// <summary>Number of GPU frames where the full surface was redrawn.</summary>
    public long FullRedrawFrames { get; }

    internal GpuRenderTelemetry(long skipped, long dirtyRect, long fullRedraw)
    {
        SkippedFrames    = skipped;
        DirtyRectFrames  = dirtyRect;
        FullRedrawFrames = fullRedraw;
    }
}
