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

    /// <summary>
    /// Saves the given SKBitmap to the specified file path in PNG format.
    /// </summary>
    /// <param name="bitmap">The SKBitmap to save.</param>
    /// <param name="filePath">The full file path to save to.</param>
    /// <param name="format">Optional image format (default is PNG).</param>
    /// <param name="quality">Optional quality for formats like JPEG (0-100).</param>
    public static void SaveBitmapToFile(this SKBitmap bitmap, string filePath,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Encodes an SKBitmap to PNG or JPEG and returns the raw bytes.
    /// </summary>
    /// <param name="bitmap">The bitmap to export.</param>
    /// <param name="format">The image format (PNG by default).</param>
    /// <param name="quality">Image quality (used only for lossy formats like JPEG).</param>
    /// <returns>Byte array representing the encoded image.</returns>
    public static byte[] EncodeBitmapToBytes(this SKBitmap bitmap, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>
    /// Applies an alpha mask to a bitmap by making pixels that match the target color transparent.
    /// Pixels within the tolerance range of the target color will be set to fully transparent.
    /// </summary>
    /// <param name="bitmap">The bitmap to modify.</param>
    /// <param name="targetColor">The color to make transparent.</param>
    /// <param name="tolerance">The tolerance for color matching (0-255). Higher values match more similar colors.</param>
    public static void ApplyAlphaMask(this SKBitmap bitmap, SKColor targetColor, byte tolerance = 5)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        int width = bitmap.Width;
        int height = bitmap.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                if (IsColorClose(color, targetColor, tolerance))
                {
                    // Fully transparent should also have RGB = 0 for premul correctness.
                    bitmap.SetPixel(x, y, new SKColor(0, 0, 0, 0));
                }
            }
        }

        bitmap.NotifyPixelsChanged();
    }

    /// <summary>
    /// Converts a bitmap to use premultiplied alpha format.
    /// If the bitmap already uses premultiplied alpha, it is returned unchanged.
    /// Otherwise, a new bitmap is created with premultiplied alpha values.
    /// </summary>
    /// <param name="bitmap">The bitmap to convert.</param>
    /// <returns>A bitmap with premultiplied alpha. May be the original bitmap if it already uses premultiplied alpha, or a new bitmap with converted values.</returns>
    public static SKBitmap PremultiplyAlpha(this SKBitmap bitmap)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        if (bitmap.AlphaType == SKAlphaType.Premul)
            return bitmap;

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, bitmap.ColorType, SKAlphaType.Premul);
        var premulBitmap = new SKBitmap(info);

        int width = bitmap.Width;
        int height = bitmap.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                byte a = c.Alpha;

                byte r = (byte)((c.Red * a + 127) / 255);
                byte g = (byte)((c.Green * a + 127) / 255);
                byte b = (byte)((c.Blue * a + 127) / 255);

                premulBitmap.SetPixel(x, y, new SKColor(r, g, b, a));
            }
        }

        premulBitmap.NotifyPixelsChanged();
        return premulBitmap;
    }

    private static bool IsColorClose(SKColor a, SKColor b, byte tolerance)
    {
        return
            Math.Abs(a.Red - b.Red) <= tolerance &&
            Math.Abs(a.Green - b.Green) <= tolerance &&
            Math.Abs(a.Blue - b.Blue) <= tolerance;
    }
}