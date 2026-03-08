using SkiaSharp;

namespace Gondwana.SkiaSharp;

/// <summary>
/// Provides utility methods for working with SkiaSharp bitmaps, including saving, encoding, and alpha channel operations.
/// </summary>
public static class SkiaHelper
{
    /// <summary>
    /// Saves the given SKBitmap to the specified file path in PNG format.
    /// </summary>
    /// <param name="bitmap">The SKBitmap to save.</param>
    /// <param name="filePath">The full file path to save to.</param>
    /// <param name="format">Optional image format (default is PNG).</param>
    /// <param name="quality">Optional quality for formats like JPEG (0-100).</param>
    public static void SaveBitmapToFile(SKBitmap bitmap, string filePath,
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
    public static byte[] EncodeBitmapToBytes(SKBitmap bitmap, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
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
    public static void ApplyAlphaMask(SKBitmap bitmap, SKColor targetColor, byte tolerance = 5)
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
    public static SKBitmap PremultiplyAlpha(SKBitmap bitmap)
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