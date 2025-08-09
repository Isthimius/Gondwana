using Gondwana.Drawing;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class BitmapBackbuffer : BackbufferBase
{
    private readonly object _swapLock = new();

    private SKBitmap _front;
    private SKBitmap _back;
    private SKSurface _backSurface;

    // lets the host know whether there’s anything new to publish
    private bool _frameDirty;

    public BitmapBackbuffer(int width, int height) : base(width, height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _front = new SKBitmap(info);
        _back = new SKBitmap(info);
        _backSurface = SKSurface.Create(info, _back.GetPixels(), _back.Info.RowBytes);
    }

    public override SKCanvas Canvas => _backSurface.Canvas;

    /// <summary>Reset canvas state at the start of a background draw.</summary>
    public void BeginFrame()
    {
        var c = _backSurface.Canvas;
        c.RestoreToCount(1);
        c.Save();
        c.ResetMatrix();
        c.ClipRect(new SKRect(0, 0, Width, Height));
        _frameDirty = false;                                // <—
        DirtyRectangle = System.Drawing.Rectangle.Empty;    // optional
    }

    /// <summary>Optional convenience if you want an opaque clear in the engine.</summary>
    public void ClearOpaque(SKColor color) =>
        Canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 255));

    /// <summary>Called by BackbufferBase.DrawTiles(...) for each visible tile.</summary>
    public override void DrawTileFrame(Tile tile)
    {
        var bmp = tile.CurrentFrame.SkBitmap;
        if (bmp is null) return;

        // draw the tile
        Canvas.DrawBitmap(bmp, tile.DrawLocation.ToSKRect());

        if (bmp != null)
        {
            Canvas.DrawBitmap(bmp, tile.DrawLocation.ToSKRect());
            AddToDirtyRectangle(tile.DrawLocation);  // optional but nice
            _frameDirty = true;                      // <—
        }

        // mark dirty regions and flag frame as updated
        AddToDirtyRectangle(tile.DrawLocation);
        _frameDirty = true;
    }

    /// <summary>
    /// Publish the frame if something drew. Returns true when a swap happened,
    /// with the src rect for the new _front.
    /// </summary>
    public void MarkDirty() => _frameDirty = true;

    public bool TryEndFrame(out SKRectI src)
    {
        lock (_swapLock)
        {
            if (!_frameDirty)
            {
                src = default;
                return false;
            }

            _backSurface.Flush();

            var tmp = _front; _front = _back; _back = tmp;

            _backSurface.Dispose();
            var info = _back.Info;
            _backSurface = SKSurface.Create(info, _back.GetPixels(), info.RowBytes);

            src = new SKRectI(0, 0, _front.Width, _front.Height);
            _frameDirty = false;
            return true;
        }
    }

    /// <summary>
    /// Unconditional swap (rarely needed now). Prefer TryEndFrame.
    /// </summary>
    public (SKBitmap front, SKRectI src) EndFrame()
    {
        _backSurface.Flush();
        lock (_swapLock)
        {
            var tmp = _front; _front = _back; _back = tmp;

            _backSurface.Dispose();
            var info = _back.Info;
            _backSurface = SKSurface.Create(info, _back.GetPixels(), info.RowBytes);

            _frameDirty = false;
            return (_front, new SKRectI(0, 0, _front.Width, _front.Height));
        }
    }

    public void Resize(int width, int height)
    {
        lock (_swapLock)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            _backSurface.Dispose();
            _front.Dispose();
            _back.Dispose();

            _front = new SKBitmap(info);
            _back = new SKBitmap(info);
            _backSurface = SKSurface.Create(info, _back.GetPixels(), info.RowBytes);
        }
    }

    /// <summary>
    /// Zero-copy wrapper over the persistent _front pixels for the adapter.
    /// Disposing the SKImage wrapper does not free the underlying bitmap.
    /// </summary>
    public override SKImage Snapshot() => SKImage.FromBitmap(_front);

    public override void Dispose()
    {
        base.Dispose();
        _backSurface.Dispose();
        _front.Dispose();
        _back.Dispose();
    }
}
