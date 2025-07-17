using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering.Direct;

public class DirectRectangle : DirectDrawing
{
    private readonly SKPaint _paint;
    private readonly bool _isFilled;

    public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, int width)
        : base(surface, bounds)
    {
        _paint = new SKPaint
        {
            Color = color.ToSKColor(),
            IsStroke = true,
            StrokeWidth = width,
            IsAntialias = true
        };
        _isFilled = false;
    }

    public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, bool isFilled)
        : base(surface, bounds)
    {
        _paint = new SKPaint
        {
            Color = color.ToSKColor(),
            IsStroke = !isFilled,
            StrokeWidth = 1,
            IsAntialias = true
        };
        _isFilled = isFilled;
    }

    public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, bool isFilled, int alpha)
        : base(surface, bounds)
    {
        _paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, (byte)alpha),
            IsStroke = !isFilled,
            StrokeWidth = 1,
            IsAntialias = true
        };
        _isFilled = isFilled;
    }

    protected internal override void Render()
    {
        var canvas = _surface.Buffer.Canvas;
        var rect = Bounds.ToSKRect();

        if (_isFilled)
            canvas.DrawRect(rect, _paint);
        else
            canvas.DrawRect(rect, _paint);
    }
}
