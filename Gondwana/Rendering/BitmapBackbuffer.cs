using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class BitmapBackbuffer : BackbufferBase
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;

    public BitmapBackbuffer(int width, int height)
        : base(width, height)
    {
        _bitmap = new SKBitmap(width, height, true);
        _canvas = new SKCanvas(_bitmap);
    }

    public override SKCanvas Canvas => _canvas;
    public override SKImage Snapshot() => SKImage.FromBitmap(_bitmap);

    public override void Dispose()
    {
        base.Dispose();

        _canvas.Dispose();
        _bitmap.Dispose();
    }
}