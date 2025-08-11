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

    public void BeginFrame()
    {
        var c = Canvas;
        c.RestoreToCount(1);
        c.Save();
        c.ResetMatrix();
        c.ClipRect(new SKRect(0, 0, Width, Height));
        //DirtyRectangle = System.Drawing.Rectangle.Empty;
    }

    public void EndFrame() => _surface.Flush();

    public void ClearOpaque(SKColor color) =>
        Canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 255));

    public override void DrawTileFrame(Tile tile)
    {
        var bmp = tile.CurrentFrame.SkBitmap;
        if (bmp is null) return;

        // Draw once (removed accidental double draw)
        var dst = tile.DrawLocation.ToSKRect();
        Canvas.DrawBitmap(bmp, dst);
        AddToDirtyRectangle(tile.DrawLocation);
    }

    public override SKImage Snapshot() => SKImage.FromBitmap(_buffer);

    public override void Dispose()
    {
        base.Dispose();
        _surface.Dispose();
        _buffer.Dispose();
    }
}
