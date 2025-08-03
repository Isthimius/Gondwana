using Gondwana.Drawing;
using Gondwana.Grid;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class BitmapBackbuffer : BackbufferBase
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;

    public BitmapBackbuffer(int width, int height, GridPointMatrixes? drawSource = null)
        : base(width, height, drawSource)
    {
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _canvas = new SKCanvas(_bitmap);
    }

    public override SKCanvas Canvas => _canvas;

    public override void DrawTileFrame(Tile tile)
    {
        var bitmap = tile.CurrentFrame.SkBitmap;
        if (bitmap != null)
            Canvas.DrawBitmap(bitmap, tile.DrawLocation.ToSKRect());
    }

    public override SKImage Snapshot() => SKImage.FromBitmap(_bitmap);

    public override void Dispose()
    {
        base.Dispose();

        _canvas?.Dispose();
        _bitmap?.Dispose();
    }
}