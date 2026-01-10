using System.Drawing;
using SkiaSharp;

namespace Gondwana.SkiaSharp;

public static class SkiaExtensions
{
    public static Rectangle ToRectangle(this SKRect rect)
        => Rectangle.FromLTRB((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);

    public static SKColor ToSKColor(this Color color)
        => new(color.R, color.G, color.B, color.A);

    public static SKRect ToSKRect(this Rectangle rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    public static SKRect ToSKRect(this RectangleF rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    public static SKRectI ToSKRectI(this Rectangle rect)
        => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static SKPoint[] ToSKPoints(this Point[] points, bool enclose = false)
    {
        if (!enclose)
            return points.Select(p => new SKPoint(p.X, p.Y)).ToArray(); ;

        var len = points.Length + 1;
        var result = new SKPoint[len];

        for (int i = 0; i < points.Length; i++)
            result[i] = new SKPoint(points[i].X, points[i].Y);

        if (enclose)
            result[^1] = result[0]; // append first point to close polygon

        return result;
    }
}