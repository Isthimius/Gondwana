using System.Drawing.Drawing2D;

namespace Gondwana.Tooling.Studio.Shared.WinForms.Theming;

/// <summary>Loads shared PNG artwork as independently owned, DPI-sized bitmaps.</summary>
public static class ToolingIconResources
{
    private const string ResourcePrefix = "Gondwana.Tooling.Studio.Shared.WinForms.Theming.assets.";
    private static readonly int[] OxygenSizes = [8, 16, 22, 24, 32, 48, 64, 128, 256];

    /// <summary>Returns an owned bitmap sized for the requesting control's effective DPI.</summary>
    public static Bitmap CreateBitmap(string fileName, int pixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (pixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixels));

        using var stream = typeof(ToolingIconResources).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
                           ?? throw new InvalidOperationException($"Missing embedded tooling icon: {fileName}");

        using var source = Image.FromStream(stream);
        return Resize(source, pixels);
    }

    /// <summary>Loads the closest suitable native Oxygen PNG from the published artwork directory.</summary>
    internal static Bitmap? TryCreateOxygenBitmap(string fileName, int pixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (pixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixels));

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new ArgumentException("Expected an icon filename, not a path.", nameof(fileName));

        var path = FindOxygenIcon(fileName, pixels);

        if (path is null)
            return null;

        using var stream = File.OpenRead(path);
        using var source = Image.FromStream(stream);
        return Resize(source, pixels);
    }

    internal static string? FindOxygenIcon(string fileName, int pixels)
    {
        // Prefer exact size, then the next larger native artwork, then the largest smaller one.
        var candidates = OxygenSizes.Where(size => size >= pixels)
            .Concat(OxygenSizes.Where(size => size < pixels).Reverse());

        foreach (int size in candidates)
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "icons",
                "oxygen",
                $"{size}x{size}",
                fileName);

            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static Bitmap Resize(Image source, int pixels)
    {
        var bitmap = new Bitmap(pixels, pixels);

        try
        {
            using var graphics = Graphics.FromImage(bitmap);

            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            graphics.DrawImage(
                source,
                new Rectangle(
                    0,
                    0,
                    pixels,
                    pixels),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
