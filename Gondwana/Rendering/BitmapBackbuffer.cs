using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class BitmapBackbuffer : BackbufferBase
{
    private readonly object _gate = new();       // guards _buffer/_surface/_disposed
    private SKBitmap? _buffer;
    private SKSurface? _surface;
    private bool _disposed;

    // resize request (written by UI thread, read by render thread)
    private int _reqW, _reqH;           // 0 means "no request"

    private int _resizeFlag;            // 0 = none, 1 = pending

    public BitmapBackbuffer(int width, int height) : base(width, height)
    {
        CreateSurface(width, height);
    }

    protected internal override void RequestResize(int width, int height)
    {
        Volatile.Write(ref _reqW, width);
        Volatile.Write(ref _reqH, height);
        Interlocked.Exchange(ref _resizeFlag, 1); // coalesce requests
    }

    public override SKCanvas Canvas
    {
        get { lock (_gate) return _surface!.Canvas; }
    }

    private readonly SKPaint _bitmapPaint = new SKPaint
    {
        FilterQuality = SKFilterQuality.None,
        BlendMode = SKBlendMode.SrcOver
    };

    public SKFilterQuality FilterQuality
    {
        get => _bitmapPaint.FilterQuality;
        set => _bitmapPaint.FilterQuality = value;
    }

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

    protected internal override void EndFrame()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null) return;
            _surface.Flush();
        }
    }

    protected internal override void DrawTileFrame(Tile tile)
    {
        var bmp = tile.CurrentFrame.SkBitmap;
        var worldRect = tile.DrawLocation.ToSKRect();

        if (bmp is not null)
            Canvas.DrawBitmap(bmp, worldRect, _bitmapPaint);
    }

    // Producer copies out an immutable image for the adapter/UI thread
    protected internal override SKImage Snapshot()
    {
        lock (_gate)
        {
            if (_disposed || _surface is null) throw new ObjectDisposedException(nameof(BitmapBackbuffer));
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