using System.Runtime.InteropServices;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>Shared file-type artwork for directory nodes and document tabs.</summary>
internal static class StudioFileTypeIcons
{
    // Keep labels and placeholder icons together so artwork can be replaced easily.
    internal static readonly (string Key, string Label, string Glyph, Color Color)[] Categories =
    [
        ("gaf", "Assets (.gaf)", "A", Color.Goldenrod),
        ("gts", "Tilesheets (.gts)", "T", Color.DodgerBlue),
        ("gani", "Animations (.gani)", "▶", Color.MediumOrchid),
        ("gscn", "Scenes (.gscn)", "S", Color.SeaGreen),
        ("gspr", "Sprites (.gspr)", "◆", Color.Coral),
        ("gsnd", "Sound (.gsnd)", "♪", Color.Teal),
        ("misc", "Misc", "?", Color.SlateGray)
    ];

    /// <summary>Draws one of the file-type glyphs at the requested pixel size.</summary>
    internal static Bitmap CreateBitmap(int pixels, string glyph, Color color)
    {
        float scale = pixels / 16f;
        var bitmap = new Bitmap(pixels, pixels);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            using var background = new SolidBrush(color);
            // Pixel units keep the glyph tied to the target bitmap, independent of monitor DPI.
            using var font = new Font("Segoe UI", 8 * 96f / 72f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            using var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.FillRectangle(background, scale, scale, pixels - 2 * scale, pixels - 2 * scale);
            graphics.DrawString(glyph, font, Brushes.White, new RectangleF(0, 0, pixels, pixels), alignment);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Maps Studio formats to shared assembly-embedded artwork.</summary>
    internal static string? ResourceFileFor(string key) => key.ToLowerInvariant() switch
    {
        "gaf" => "icons8-unpack-luggage-96.png",
        "gts" => "icons8-small-icons-96.png",
        "gani" => "icons8-video-96.png",
        "gscn" => "icons8-nature-96.png",
        "gspr" => "icons8-frodo-96.png",
        "gsnd" => "icons8-sound-96.png",
        "misc" => "icons8-deployment-96.png",
        _ => null
    };

    /// <summary>Uses embedded artwork for known formats, retaining glyphs as a fallback.</summary>
    internal static Bitmap CreateFileTypeBitmap(int pixels, string key, string glyph, Color color)
    {
        string? fileName = ResourceFileFor(key);
        return fileName is null
            ? CreateBitmap(pixels, glyph, color)
            : ToolingIconResources.CreateBitmap(fileName, pixels);
    }

    /// <summary>Creates an independent 16-pixel icon for a Studio document.</summary>
    internal static Icon CreateDocumentIcon(string extension)
    {
        // Both .gaf and .zip documents use the assets icon.
        string key = extension.Equals("zip", StringComparison.OrdinalIgnoreCase) ? "gaf" : extension;
        foreach (var category in Categories)
        {
            if (!category.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            using var bitmap = CreateFileTypeBitmap(16, category.Key, category.Glyph, category.Color);
            IntPtr handle = bitmap.GetHicon();
            try
            {
                // Clone so the returned icon owns its handle; release the temporary HICON.
                using var temporary = Icon.FromHandle(handle);
                return (Icon)temporary.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(extension), extension, "Unsupported Studio document format.");
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
