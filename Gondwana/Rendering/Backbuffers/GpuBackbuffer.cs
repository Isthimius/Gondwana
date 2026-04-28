using System.Drawing;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Provides a fully GPU-rendered backbuffer implementation using SkiaSharp and OpenGL.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture (Option A — GL-thread rendering):</strong>
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
/// <strong>Thread safety:</strong> After <see cref="Initialize"/> has been called, ALL methods on
/// this class must be invoked from the GL thread (the thread on which the
/// <see cref="GRContext"/> is current).  The engine loop guarantees this by checking
/// <see cref="IsGlThreadRendered"/> before touching the backbuffer.
/// </para>
/// </remarks>
public class GpuBackbuffer : BackbufferBase
{
    // Surface state.  All access MUST occur on the GL thread after Initialize() is called.
    private SKBitmap? _cpuBitmap;   // temporary CPU surface used before GRContext is ready
    private SKSurface? _surface;
    private bool _disposed;

    private int _targetFps = 60;

    // Frame counter used to compute the actual rendered FPS.
    // Incremented on the GL thread by RecordFrame(); consumed atomically by the engine's CPS sampler.
    private long _frameCount;

    /// <summary>
    /// Gets or sets the target frame rate for the render timer that drives this GPU backbuffer.
    /// </summary>
    /// <remarks>
    /// This value is read by the platform adapter (e.g. <c>WinFormGpuRenderSurfaceAdapter</c>) on
    /// each timer tick and used to update the invalidation timer interval.  Set to <c>0</c> for an
    /// uncapped frame rate (the adapter fires its timer as fast as WinForms allows, and the actual
    /// frame rate is then limited only by vsync and GPU throughput).
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
    /// This value is read by the platform adapter (e.g. <c>WinFormGpuRenderSurfaceAdapter</c>)
    /// at the start of each <c>PaintSurface</c> call and applied to the underlying
    /// <c>GLControl.VSync</c> property when it changes.  Enabling vsync prevents screen tearing
    /// but caps the frame rate to the monitor refresh rate.  Disabling vsync allows higher frame
    /// rates at the cost of potential tearing.
    /// </remarks>
    /// <value>
    /// <see langword="true"/> to synchronise presentation with the monitor refresh; otherwise
    /// <see langword="false"/>.  The default is <see langword="true"/>.
    /// </value>
    public bool VSync { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuBackbuffer"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The initial width of the backbuffer in pixels.</param>
    /// <param name="height">The initial height of the backbuffer in pixels.</param>
    public GpuBackbuffer(int width, int height)
        : base(width, height)
    {
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
        _surface = SKSurface.Create(grContext, budgeted: true, info);
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
