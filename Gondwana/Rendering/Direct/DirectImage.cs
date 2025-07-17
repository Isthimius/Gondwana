using Gondwana.Drawing;
using System.Drawing;
using SkiaSharp;
using Gondwana.Rendering;

namespace Gondwana.Rendering.Direct;

public class DirectImage : DirectDrawing
{
    private readonly Tilesheet _tilesheet;

    public DirectImage(VisibleSurfaceBase surface, Rectangle bounds, Tilesheet bmp)
        : base(surface, bounds)
    {
        _tilesheet = bmp;
    }

    protected internal override void Render()
    {
        var canvas = _surface.Buffer.Canvas;
        var destRect = Bounds.ToSKRect();

        if (_tilesheet == null)
            return;

        var bitmap = _tilesheet.SkBitmap;
        if (bitmap == null)
            return;

        canvas.DrawBitmap(bitmap, destRect);
    }

    public Tilesheet Tilesheet => _tilesheet;
}
