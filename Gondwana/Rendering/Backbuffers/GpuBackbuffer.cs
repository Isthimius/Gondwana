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
/// <strong>Threading model:</strong> <see cref="Initialize"/> must be called on the GL thread (the
/// same thread on which <see cref="SkiaSharp.Views.Desktop.SKGLControl"/> paints).  All methods that
/// access <see cref="GRContext"/> or <see cref="SKSurface"/> — including <see cref="BeginFrame"/>,
/// <see cref="EndFrame"/>, <see cref="DrawTileFrame"/>, <see cref="Canvas"/>, and
/// <see cref="Snapshot"/> — are also assumed to be called from the GL thread once the surface has
/// been initialized.  The engine's background render thread must therefore drive rendering
/// synchronously inside <c>SKGLControl.PaintSurface</c> (or a shared-context approach must be
/// adopted) to ensure the OpenGL context is current during GPU operations.
/// </para>
/// <para>
/// Until <see cref="Initialize"/> has been called (or while a resize is pending), drawing methods
/// are no-ops, <see cref="Canvas"/> returns a discarded off-screen canvas, and
/// <see cref="Snapshot"/> returns an empty placeholder image so that callers never crash.
/// </para>
/// </remarks>
public class GpuBackbuffer : BackbufferBase
{
    // Tiny raster surface used as a safe no-op target before Initialize() succeeds.
    // All rendering operations against this canvas are silently discarded.
    private readonly SKSurface _nullSurface;

    // GL resources — null until Initialize() is called on the GL thread.
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;

    // Set to true once Initialize() succeeds; reset to false on Dispose().
    // Declared volatile so that reads on any thread always observe the latest value written
    // by Initialize() or Dispose() without requiring an explicit lock.
    private volatile bool _initialized;

    // Set to true once Dispose() has been called; guards against double-disposal.
    private bool _disposed;

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
        // Create a tiny raster surface used as a safe no-op target before Initialize() succeeds.
        // Drawing to this canvas is silently discarded once the GPU surface is ready.
        _nullSurface = SKSurface.Create(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Could not create fallback raster surface.");
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
        // Suppress initialization attempts with non-positive dimensions (e.g. minimized window).
        if (width <= 0 || height <= 0)
            return;

        // Dispose old GL resources (surface and render-target only — GRContext is not owned here).
        _surface?.Dispose();
        _surface = null;
        _renderTarget?.Dispose();
        _renderTarget = null;

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
        // Intentional no-op: for the GPU path, resize is handled entirely on the GL thread.
        // The adapter fires ResizeRequested from OnPaintSurface (with the GL context current),
        // which calls Initialize() with the new dimensions directly.
    }

    /// <summary>
    /// Gets the SkiaSharp canvas for drawing operations.
    /// </summary>
    /// <remarks>
    /// Before <see cref="Initialize"/> has been called (or while a resize is pending), this returns
    /// a temporary off-screen canvas so that callers never crash.  Drawing to it is silently discarded.
    /// Once initialized this must only be called from the GL thread.
    /// </remarks>
    public override SKCanvas Canvas => _surface?.Canvas ?? _nullSurface.Canvas;

    /// <summary>
    /// Prepares the backbuffer for a new rendering frame.
    /// </summary>
    /// <remarks>
    /// Returns immediately (no-op) if the backbuffer has not yet been initialized or if a resize
    /// is pending and the GL thread has not yet called <see cref="Initialize"/> with the new dimensions.
    /// </remarks>
    protected internal override void BeginFrame()
    {
        // Skip if not ready: not yet initialized or surface was disposed/nulled during reinit.
        if (!_initialized || _surface is null) return;

        var c = _surface.Canvas;
        // Restore any open save-layers from the previous frame, then set up a clean
        // coordinate space and clip region for the new frame (same pattern as BitmapBackbuffer).
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
        // No-op before initialization or during a pending resize to avoid accessing an invalid surface.
        if (_surface is null) return;
        var image = tile.CurrentFrame.SkImage;
        if (image != null)
            Canvas.DrawImage(image, destRectScreen.ToSKRect());
    }

    /// <summary>
    /// Creates an immutable snapshot of the current backbuffer contents.
    /// </summary>
    /// <remarks>
    /// Before <see cref="Initialize"/> has been called (or while a resize is pending), returns a
    /// blank 1×1 placeholder image so that callers never crash.  Presentation of this placeholder
    /// is harmless: the adapter clips it to an empty source rectangle.
    /// Once initialized this must only be called from the GL thread.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the backbuffer has been disposed.</exception>
    protected internal override SKImage Snapshot()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuBackbuffer));

        // Return a blank placeholder when the GPU surface is not yet ready.
        return (_surface ?? _nullSurface).Snapshot();
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

        _surface?.Dispose();
        _surface = null;
        _renderTarget?.Dispose();
        _renderTarget = null;
        _grContext = null;   // not owned — do not dispose

        _nullSurface.Dispose();

        base.Dispose();
    }
}