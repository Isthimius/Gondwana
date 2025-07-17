using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public static class SkiaExtensions
{
    public static SKColor ToSKColor(this Color color)
        => new(color.R, color.G, color.B, color.A);

    public static SKRect ToSKRect(this Rectangle rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    public static SKRectI ToSKRectI(this Rectangle rect)
        => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static SKPoint[] ToSKPoints(this Point[] points)
        => points.Select(p => new SKPoint(p.X, p.Y)).ToArray();
}