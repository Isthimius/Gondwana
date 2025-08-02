using SkiaSharp;

namespace Gondwana.Skia;

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

    public static void ApplyAlphaMask(SKBitmap bitmap, SKColor targetColor, byte tolerance = 0)
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
                    // Make this pixel fully transparent
                    var newColor = new SKColor(color.Red, color.Green, color.Blue, 0);
                    bitmap.SetPixel(x, y, newColor);
                }
            }
        }

        bitmap.NotifyPixelsChanged();
    }

    public static void PremultiplyAlpha(SKBitmap bitmap)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        if (bitmap.AlphaType == SKAlphaType.Premul)
            return; // Already premultiplied

        int width = bitmap.Width;
        int height = bitmap.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                if (color.Alpha == 255)
                    continue;

                byte a = color.Alpha;
                byte r = (byte)(color.Red * a / 255);
                byte g = (byte)(color.Green * a / 255);
                byte b = (byte)(color.Blue * a / 255);

                bitmap.SetPixel(x, y, new SKColor(r, g, b, a));
            }
        }

        bitmap.NotifyPixelsChanged();
    }

    private static bool IsColorClose(SKColor a, SKColor b, byte tolerance)
    {
        return
            Math.Abs(a.Red - b.Red) <= tolerance &&
            Math.Abs(a.Green - b.Green) <= tolerance &&
            Math.Abs(a.Blue - b.Blue) <= tolerance;
    }
}
