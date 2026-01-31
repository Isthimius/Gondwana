using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Provides a bitmap-based backbuffer implementation for rendering operations using SkiaSharp.
/// This backbuffer maintains an in-memory bitmap surface that can be drawn to from a render thread
/// and safely snapshotted for display on the UI thread.
/// </summary>
public sealed class BitmapBackbuffer : BackbufferBase
{
    private readonly object _gate = new();       // guards _buffer/_surface/_disposed
    private SKBitmap? _buffer;
    private SKSurface? _surface;
    private bool _disposed;

    // resize request (written by UI thread, read by render thread)
    private int _reqW, _reqH;           // 0 means "no request"

    private int _resizeFlag;            // 0 = none, 1 = pending

    /// <summary>
    /// Initializes a new instance of the <see cref="BitmapBackbuffer"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The initial width of the backbuffer in pixels.</param>
    /// <param name="height">The initial height of the backbuffer in pixels.</param>
    public BitmapBackbuffer(int width, int height) : base(width, height)
    {
        CreateSurface(width, height);
    }

    /// <summary>
    /// Requests a resize of the backbuffer to the specified dimensions.
    /// This method is thread-safe and can be called from the UI thread.
    /// The actual resize operation will be performed on the render thread during the next frame.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <remarks>
    /// Multiple resize requests are coalesced; only the most recent request will be processed.
    /// </remarks>
    protected internal override void RequestResize(int width, int height)
    {
        Volatile.Write(ref _reqW, width);
        Volatile.Write(ref _reqH, height);
        Interlocked.Exchange(ref _resizeFlag, 1); // coalesce requests
    }

    /// <summary>
    /// Gets the <see cref="SKCanvas"/> for drawing operations on this backbuffer.
    /// </summary>
    /// <value>
    /// The canvas instance that can be used to perform drawing operations.
    /// </value>
    /// <remarks>
    /// Access to the canvas is thread-safe and synchronized using an internal lock.
    /// </remarks>
    public override SKCanvas Canvas
    {
        get { lock (_gate) return _surface!.Canvas; }
    }

    private readonly SKPaint _bitmapPaint = new SKPaint
    {
        FilterQuality = SKFilterQuality.None,
        BlendMode = SKBlendMode.SrcOver
    };

    /// <summary>
    /// Gets or sets the filter quality used when drawing bitmaps to the backbuffer.
    /// </summary>
    /// <value>
    /// The <see cref="SKFilterQuality"/> value that determines the quality of bitmap filtering during rendering.
    /// Default is <see cref="SKFilterQuality.None"/> for pixel-perfect rendering.
    /// </value>
    public SKFilterQuality FilterQuality
    {
        get => _bitmapPaint.FilterQuality;
        set => _bitmapPaint.FilterQuality = value;
    }

    /// <summary>
    /// Prepares the backbuffer for a new frame of rendering.
    /// Processes any pending resize requests and resets the canvas state for drawing.
    /// </summary>
    /// <remarks>
    /// This method should only be called from the render thread. It handles:
    /// <list type="bullet">
    /// <item><description>Processing pending resize requests from the UI thread</description></item>
    /// <item><description>Recreating the surface with new dimensions if needed</description></item>
    /// <item><description>Resetting the canvas transform matrix</description></item>
    /// <item><description>Setting up the clipping region</description></item>
    /// </list>
    /// </remarks>
    protected internal override void BeginFrame()
    {
        // if a resize was requested, do it now (render thread only)...
        if (Interlocked.Exchange(ref _resizeFlag, 0) == 1)
        {
            var w = Volatile.Read(ref _reqW);
            var h = Volatile.Read(ref _reqH);

            if (w > 0 && h > 0)
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    DisposeSurface_NoLock();
                    CreateSurface(w, h);
                    UpdateSize(w, h); // tell base about new logical size
                }
            }
        }

        /// prepare for drawing...
        lock (_gate)
        {
            if (_disposed) return;
            var c = _surface!.Canvas;
            c.RestoreToCount(1);
            c.Save();
            c.ResetMatrix();
            c.ClipRect(new SKRect(0, 0, Width, Height));
        }
    }

    /// <summary>
    /// Completes the current frame and flushes all pending drawing operations to the backbuffer.
    /// </summary>
    /// <remarks>
    /// This method should only be called from the render thread after all drawing operations for the current frame are complete.
    /// </remarks>
    protected internal override void EndFrame()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null) return;
            _surface.Flush();
        }
    }

    /// <summary>
    /// Draws a single tile frame to the backbuffer at the specified destination rectangle.
    /// </summary>
    /// <param name="tile">The tile containing the frame to be drawn.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates where the tile should be drawn.</param>
    /// <remarks>
    /// If the tile's current frame does not have a valid bitmap, the method returns without drawing.
    /// The drawing uses the filter quality specified by the <see cref="FilterQuality"/> property.
    /// </remarks>
    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var bmp = tile.CurrentFrame.SkBitmap;

        if (bmp is null)
            return;

        Canvas.DrawBitmap(bmp, destRectScreen.ToSKRect(), _bitmapPaint);
    }

    /// <summary>
    /// Creates an immutable snapshot of the current backbuffer surface.
    /// </summary>
    /// <returns>
    /// An <see cref="SKImage"/> representing the current state of the backbuffer.
    /// This image is immutable and safe to use on the UI thread.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the backbuffer has been disposed or the surface is no longer available.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and is typically called by the render thread to produce an image
    /// that can be safely consumed by the UI thread for display.
    /// </remarks>
    protected internal override SKImage Snapshot()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null)
                throw new ObjectDisposedException(nameof(BitmapBackbuffer));

            return _surface.Snapshot(); // immutable; safe to use on UI thread
        }
    }

    private void CreateSurface(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _buffer = new SKBitmap(info);
        _surface = SKSurface.Create(info, _buffer.GetPixels(), _buffer.Info.RowBytes);
    }

    private void DisposeSurface_NoLock()
    {
        _surface?.Dispose(); _surface = null;
        _buffer?.Dispose(); _buffer = null;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="BitmapBackbuffer"/>.
    /// </summary>
    /// <remarks>
    /// This method disposes of the underlying SkiaSharp surface and bitmap resources.
    /// The disposal is thread-safe and idempotent.
    /// </remarks>
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