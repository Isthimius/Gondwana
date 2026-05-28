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
    /// Converts a SkiaSharp SKColor to a System.Drawing Color.
    /// </summary>
    /// <param name="skcolor">The SKColor to convert.</param>
    /// <returns>A Color with the same RGBA values.</returns>
    public static Color ToColor(this SKColor skcolor)
        => Color.FromArgb(skcolor.Alpha, skcolor.Red, skcolor.Green, skcolor.Blue);

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

    /// <summary>
    /// Copies a region from a source SKBitmap to a new SKBitmap.
    /// </summary>
    /// <param name="source">The source SKBitmap.</param>
    /// <param name="sourceRect">The rectangle region to copy.</param>
    /// <returns>A new SKBitmap containing the copied region.</returns>
    public static SKBitmap CopyRegion(this SKBitmap source, Rectangle sourceRect)
    {
        if (source == null || source.IsEmpty)
            throw new ArgumentException("Invalid source bitmap.", nameof(source));

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            throw new ArgumentException("Source rectangle must have positive size.", nameof(sourceRect));

        var skSourceRect = sourceRect.ToSKRectI();

        if (!source.Info.Rect.Contains(skSourceRect))
            throw new ArgumentOutOfRangeException(nameof(sourceRect), "Source rectangle is outside the bitmap bounds.");

        var copy = new SKBitmap(new SKImageInfo(
            sourceRect.Width,
            sourceRect.Height,
            source.Info.ColorType,
            source.Info.AlphaType));

        using var canvas = new SKCanvas(copy);

        canvas.Clear(SKColors.Transparent);

        canvas.DrawBitmap(
            source,
            skSourceRect,
            new SKRect(0, 0, sourceRect.Width, sourceRect.Height));

        return copy;
    }

    /// <summary>
    /// Copies a region from a source SKImage to a new SKImage.
    /// </summary>
    /// <param name="source">The source SKImage.</param>
    /// <param name="sourceRect">The rectangle region to copy.</param>
    /// <returns>A new SKImage containing the copied region.</returns>
    public static SKImage CopyRegion(this SKImage source, Rectangle sourceRect)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            throw new ArgumentException("Source rectangle must have positive size.", nameof(sourceRect));

        var skSourceRect = new SKRect(
            sourceRect.Left,
            sourceRect.Top,
            sourceRect.Right,
            sourceRect.Bottom);

        var skDestRect = new SKRect(
            0,
            0,
            sourceRect.Width,
            sourceRect.Height);

        var imageInfo = new SKImageInfo(
            sourceRect.Width,
            sourceRect.Height,
            source.ColorType,
            source.AlphaType,
            source.ColorSpace);

        using var surface = SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("Could not create SKSurface.");

        surface.Canvas.Clear(SKColors.Transparent);

        surface.Canvas.DrawImage(
            source,
            skSourceRect,
            skDestRect);

        return surface.Snapshot();
    }
}