using System.Drawing;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Provides a GPU-presented backbuffer implementation using SkiaSharp and OpenGL.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture:</strong> Rendering (tile drawing, canvas operations) happens on a raster
/// <see cref="SKSurface"/> that is safe to access from any thread — including the engine's background
/// render thread.  At present time the rendered raster image is handed to
/// <c>WinFormGpuRenderSurfaceAdapter</c>, which draws it to the OpenGL surface inside
/// <c>SKGLControl.PaintSurface</c> on the GL thread using GPU-accelerated blitting.
/// This avoids any requirement for the GL context to be current on the render thread.
/// </para>
/// <para>
/// <see cref="Initialize"/> is called from the GL thread (via the adapter's
/// <c>GrContextFirstAvailable</c> / <c>ResizeRequested</c> events) to (re)create the raster surface
/// with the correct pixel dimensions.  Until the first <see cref="Initialize"/> call the constructor
/// allocates a surface from the dimensions supplied by <see cref="RenderSurfaceHost{TBackbuffer}"/>,
/// so rendering is always safe from the first frame.
/// </para>
/// </remarks>
public class GpuBackbuffer : BackbufferBase
{
    private readonly object _gate = new();   // guards _bitmap/_surface/_disposed
    private SKBitmap? _bitmap;
    private SKSurface? _surface;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuBackbuffer"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The initial width of the backbuffer in pixels.</param>
    /// <param name="height">The initial height of the backbuffer in pixels.</param>
    public GpuBackbuffer(int width, int height)
        : base(width, height)
    {
        CreateSurface(width, height);
    }

    /// <summary>
    /// Recreates the raster surface with the supplied dimensions.
    /// </summary>
    /// <remarks>
    /// This method is called from the GL thread via the adapter's <c>GrContextFirstAvailable</c> and
    /// <c>ResizeRequested</c> events, but the raster surface itself may be accessed safely from any
    /// thread.  The <paramref name="grContext"/> parameter is accepted for API symmetry with the adapter
    /// events but is not used here; GPU acceleration occurs at presentation time inside the adapter.
    /// </remarks>
    /// <param name="grContext">Ignored — provided for API symmetry with the adapter events.</param>
    /// <param name="width">The new surface width in pixels.</param>
    /// <param name="height">The new surface height in pixels.</param>
    public void Initialize(GRContext grContext, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        lock (_gate)
        {
            if (_disposed) return;
            DisposeSurface_NoLock();
            CreateSurface(width, height);
            UpdateSize(width, height);
        }
    }

    /// <summary>
    /// No-op for <see cref="GpuBackbuffer"/>: resize is driven by <see cref="Initialize"/> which is
    /// called from the GL thread via the adapter's <c>ResizeRequested</c> event.
    /// </summary>
    protected internal override void RequestResize(int width, int height) { }

    /// <summary>
    /// Gets the SkiaSharp canvas for drawing operations.
    /// </summary>
    public override SKCanvas Canvas
    {
        get { lock (_gate) return _surface!.Canvas; }
    }

    /// <summary>
    /// Prepares the backbuffer for a new rendering frame.
    /// </summary>
    protected internal override void BeginFrame()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null) return;
            var c = _surface.Canvas;
            c.RestoreToCount(1);
            c.Save();
            c.ResetMatrix();
            c.ClipRect(new SKRect(0, 0, Width, Height));
        }
    }

    /// <summary>
    /// Completes the current frame and flushes all pending drawing operations to the backbuffer.
    /// </summary>
    protected internal override void EndFrame()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null) return;
            _surface.Flush();
        }
    }

    /// <summary>
    /// Draws a single tile frame to the backbuffer at the specified screen location.
    /// </summary>
    /// <param name="tile">The tile to render.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates.</param>
    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image is null) return;
        lock (_gate)
        {
            if (_surface is null) return;
            _surface.Canvas.DrawImage(image, destRectScreen.ToSKRect());
        }
    }

    /// <summary>
    /// Creates an immutable snapshot of the current backbuffer contents.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the backbuffer has been disposed.</exception>
    protected internal override SKImage Snapshot()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null)
                throw new ObjectDisposedException(nameof(GpuBackbuffer));

            return _surface.Snapshot();
        }
    }

    private void CreateSurface(int width, int height)
    {
        // Bgra8888 matches the native Windows GDI pixel format (same as BitmapBackbuffer),
        // avoiding any channel-swapping overhead when uploading the raster image to the GPU
        // adapter during presentation.
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _bitmap = new SKBitmap(info);
        _surface = SKSurface.Create(info, _bitmap.GetPixels(), _bitmap.Info.RowBytes);
    }

    private void DisposeSurface_NoLock()
    {
        _surface?.Dispose();
        _surface = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    /// <summary>
    /// Releases all resources used by this <see cref="GpuBackbuffer"/>.
    /// </summary>
    public override void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            base.Dispose();
            DisposeSurface_NoLock();
        }
    }
}
