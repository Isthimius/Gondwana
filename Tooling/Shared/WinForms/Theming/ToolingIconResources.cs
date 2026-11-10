using System.Drawing.Drawing2D;

namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>Loads shared PNG artwork as independently owned, DPI-sized bitmaps.</summary>
public static class ToolingIconResources
{
    private const string ResourcePrefix = "Gondwana.Tooling.WinForms.Theming.assets.";

    /// <summary>Returns an owned bitmap sized for the requesting control's effective DPI.</summary>
    public static Bitmap CreateBitmap(string fileName, int pixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (pixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixels));

        using var stream = typeof(ToolingIconResources).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Missing embedded tooling icon: {fileName}");
        using var source = Image.FromStream(stream);
        var bitmap = new Bitmap(pixels, pixels);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, pixels, pixels),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
