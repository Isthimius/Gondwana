using SkiaSharp;
using System.Drawing;

public static class SkiaExtensions
{
    public static SKColor ToSKColor(this Color color)
        => new(color.R, color.G, color.B, color.A);

    public static SKRect ToSKRect(this Rectangle rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    public static SKPoint[] ToSKPoints(this Point[] points)
        => points.Select(p => new SKPoint(p.X, p.Y)).ToArray();
}