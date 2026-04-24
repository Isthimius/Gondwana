using System.Drawing;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Provides a GPU-accelerated backbuffer implementation using SkiaSharp and OpenGL.
/// </summary>
/// <remarks>
/// <para>
/// GPU resources (<see cref="GRContext"/>, <see cref="GRBackendRenderTarget"/>, and <see cref="SKSurface"/>)
/// cannot be created in the constructor because the OpenGL context is not yet current on this thread
/// when <see cref="RenderSurfaceHost{TBackbuffer}"/> instantiates the backbuffer.
/// Call <see cref="Initialize"/> from the GL thread (e.g. from a <c>GrContextFirstAvailable</c>
/// or <c>ResizeRequested</c> handler on the adapter) before any rendering can take place.
/// </para>
/// <para>
/// Until <see cref="Initialize"/> has been called, all rendering methods are no-ops and
/// <see cref="Canvas"/> and <see cref="Snapshot"/> throw <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public class GpuBackbuffer : BackbufferBase
{
    // GL resources — null until Initialize() is called on the GL thread.
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;

    // Set to true once Initialize() succeeds; reset to false on Dispose().
    private volatile bool _initialized;

    // Set to true once Dispose() has been called; guards against double-disposal.
    private bool _disposed;

    // Resize-pending state (written by UI/render thread, cleared by GL thread in Initialize).
    private int _resizeFlag;           // 0 = none, 1 = pending
    private int _reqW, _reqH;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuBackbuffer"/> class with the specified dimensions.
    /// </summary>
    /// <remarks>
    /// No GPU resources are allocated here.  Call <see cref="Initialize"/> from the GL thread
    /// once the OpenGL context is available.
    /// </remarks>
    /// <param name="width">The initial width of the backbuffer in pixels.</param>
    /// <param name="height">The initial height of the backbuffer in pixels.</param>
    public GpuBackbuffer(int width, int height)
        : base(width, height)
    {
        // Intentionally empty: GPU resources must be created on the GL thread via Initialize().
    }

    /// <summary>
    /// Creates (or recreates) the GPU surface.  Must be called on the GL thread while the
    /// OpenGL context is current.
    /// </summary>
    /// <remarks>
    /// Any previously allocated <see cref="SKSurface"/> and <see cref="GRBackendRenderTarget"/>
    /// are disposed before new resources are created.  The supplied <paramref name="grContext"/>
    /// is borrowed (not owned) and will not be disposed when this backbuffer is disposed.
    /// </remarks>
    /// <param name="grContext">
    /// The <see cref="GRContext"/> obtained from the <c>SKGLControl</c>.  Must not be null.
    /// </param>
    /// <param name="width">The new surface width in pixels.</param>
    /// <param name="height">The new surface height in pixels.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when SkiaSharp cannot create the GPU surface from the supplied context.
    /// </exception>
    public void Initialize(GRContext grContext, int width, int height)
    {
        // Dispose old GL resources (surface and render-target only — GRContext is not owned here).
        _surface?.Dispose(); _surface = null;
        _renderTarget?.Dispose(); _renderTarget = null;

        _grContext = grContext;

        var glInfo = new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat());

        _renderTarget = new GRBackendRenderTarget(
            width, height,
            sampleCount: 0,
            stencilBits: 8,
            glInfo: glInfo);

        _surface = SKSurface.Create(
            _grContext,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888,
            (SKColorSpace?)null                    // null = sRGB color space
        ) ?? throw new InvalidOperationException("Could not create GPU surface.");

        // Clear any pending-resize flag and mark as ready.
        Interlocked.Exchange(ref _resizeFlag, 0);
        _initialized = true;
        UpdateSize(width, height);
    }

    /// <summary>
    /// Requests a resize of the backbuffer.  Thread-safe; the actual GPU resource recreation
    /// is deferred to the GL thread via the adapter's <c>ResizeRequested</c> event.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    protected internal override void RequestResize(int width, int height)
    {
        Volatile.Write(ref _reqW, width);
        Volatile.Write(ref _reqH, height);
        Interlocked.Exchange(ref _resizeFlag, 1); // coalesce requests
    }

    /// <summary>
    /// Gets the SkiaSharp canvas for drawing operations.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Initialize"/> has not been called yet.
    /// </exception>
    public override SKCanvas Canvas =>
        _surface?.Canvas
        ?? throw new InvalidOperationException(
            "GpuBackbuffer has not been initialized. Call Initialize() from the GL thread first.");

    /// <summary>
    /// Prepares the backbuffer for a new rendering frame.
    /// </summary>
    /// <remarks>
    /// Returns immediately (no-op) if the backbuffer has not yet been initialized or if a resize
    /// is pending and the GL thread has not yet called <see cref="Initialize"/> with the new dimensions.
    /// </remarks>
    protected internal override void BeginFrame()
    {
        // Skip if not ready: either never initialized, or a resize is outstanding.
        if (!_initialized || _surface is null) return;
        if (Volatile.Read(ref _resizeFlag) == 1) return;

        var c = _surface.Canvas;
        c.RestoreToCount(1);
        c.Save();
        c.ResetMatrix();
        c.ClipRect(new SKRect(0, 0, Width, Height));
    }

    /// <summary>
    /// Flushes all pending draw operations to the GPU and submits the current frame.
    /// </summary>
    protected internal override void EndFrame()
    {
        if (!_initialized || _surface is null) return;
        _surface.Flush();
        _grContext!.Submit(true);
    }

    /// <summary>
    /// Draws a single tile frame to the backbuffer at the specified screen location.
    /// </summary>
    /// <param name="tile">The tile to render.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates.</param>
    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image != null)
            Canvas.DrawImage(image, destRectScreen.ToSKRect());
    }

    /// <summary>
    /// Creates an immutable GPU-backed snapshot of the current backbuffer contents.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the backbuffer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="Initialize"/> has not been called yet.
    /// </exception>
    protected internal override SKImage Snapshot()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuBackbuffer));
        if (_surface is null)
            throw new InvalidOperationException(
                "GpuBackbuffer has not been initialized. Call Initialize() from the GL thread first.");
        return _surface.Snapshot();
    }

    /// <summary>
    /// Releases all resources owned by this <see cref="GpuBackbuffer"/>.
    /// </summary>
    /// <remarks>
    /// The borrowed <see cref="GRContext"/> (passed to <see cref="Initialize"/>) is not disposed
    /// because it is owned by the <c>SKGLControl</c>.
    /// </remarks>
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _initialized = false;

        _surface?.Dispose(); _surface = null;
        _renderTarget?.Dispose(); _renderTarget = null;
        _grContext = null;   // not owned — do not dispose

        base.Dispose();
    }
}