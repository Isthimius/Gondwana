using Gondwana.Drawing;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class BitmapBackbuffer : BackbufferBase
{
    private SKBitmap _buffer;
    private SKSurface _surface;

    public BitmapBackbuffer(int width, int height) : base(width, height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _buffer = new SKBitmap(info);
        _surface = SKSurface.Create(info, _buffer.GetPixels(), _buffer.Info.RowBytes);
    }

    public override SKCanvas Canvas => _surface.Canvas;

    public override void BeginFrame()
    {
        var c = Canvas;
        c.RestoreToCount(1);
        c.Save();
        c.ResetMatrix();
        c.ClipRect(new SKRect(0, 0, Width, Height));
    }

    public override void EndFrame() => _surface.Flush();

    public void ClearOpaque(SKColor color) =>
        Canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 255));

    /// <summary>
    /// Runs as part of DoBackgroundTasks
    /// </summary>
    public override void DrawTileFrame(Tile tile)
    {
        var bmp = tile.CurrentFrame.SkBitmap;
        var dst = tile.DrawLocation.ToSKRect();

        if (bmp is null)
        {
            //Engine.Logger.LogTrace("drawing blank at " + dst.ToString());
            Canvas.DrawRect(dst, _fillPaint);
        }
        else
        {
            //Engine.Logger.LogTrace("drawing image at " + dst.ToString());
            Canvas.DrawBitmap(bmp, dst);
        }

        AddToDirtyRectangle(tile.DrawLocation);
    }

    public override SKImage Snapshot() => SKImage.FromBitmap(_buffer);

    public override void Dispose()
    {
        base.Dispose();
        EndFrame();
        _surface.Dispose();
        _buffer.Dispose();
    }
}
