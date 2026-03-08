using System.Drawing;
using SkiaSharp;

namespace Gondwana.SkiaSharp;

/// <summary>
/// Provides extension methods for converting between System.Drawing types and SkiaSharp types.
/// </summary>
public static class SkiaExtensions
{
    /// <summary>
    /// Converts a SkiaSharp SKRect to a System.Drawing Rectangle.
    /// </summary>
    /// <param name="rect">The SKRect to convert.</param>
    /// <returns>A Rectangle with integer coordinates created from the SKRect bounds.</returns>
    public static Rectangle ToRectangle(this SKRect rect)
        => Rectangle.FromLTRB((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);

    /// <summary>
    /// Converts a System.Drawing Color to a SkiaSharp SKColor.
    /// </summary>
    /// <param name="color">The Color to convert.</param>
    /// <returns>An SKColor with the same RGBA values as the original color.</returns>
    public static SKColor ToSKColor(this Color color)
        => new(color.R, color.G, color.B, color.A);

    /// <summary>
    /// Converts a System.Drawing Rectangle to a SkiaSharp SKRect.
    /// </summary>
    /// <param name="rect">The Rectangle to convert.</param>
    /// <returns>An SKRect with floating-point coordinates created from the Rectangle.</returns>
    public static SKRect ToSKRect(this Rectangle rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    /// <summary>
    /// Converts a System.Drawing RectangleF to a SkiaSharp SKRect.
    /// </summary>
    /// <param name="rect">The RectangleF to convert.</param>
    /// <returns>An SKRect with the same floating-point coordinates as the original RectangleF.</returns>
    public static SKRect ToSKRect(this RectangleF rect)
        => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    /// <summary>
    /// Converts a System.Drawing Rectangle to a SkiaSharp SKRectI (integer rectangle).
    /// </summary>
    /// <param name="rect">The Rectangle to convert.</param>
    /// <returns>An SKRectI with integer coordinates created from the Rectangle bounds.</returns>
    public static SKRectI ToSKRectI(this Rectangle rect)
        => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    /// <summary>
    /// Converts an array of System.Drawing Point to an array of SkiaSharp SKPoint.
    /// </summary>
    /// <param name="points">The array of points to convert.</param>
    /// <param name="enclose">If true, appends the first point to the end of the array to create a closed polygon.</param>
    /// <returns>An array of SKPoint values. If enclose is true, the array will have one additional element that duplicates the first point.</returns>
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