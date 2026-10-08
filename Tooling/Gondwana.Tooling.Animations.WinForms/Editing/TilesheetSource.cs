using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>
/// A loose GTS definition loaded as an authoring source for GANI frame references.
/// </summary>
internal sealed class TilesheetSource : IDisposable
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; }
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public TilesheetDefinition Definition { get; }
    /// <summary>
    /// Gets the image.
    /// </summary>
    public Bitmap? Image { get; private set; }
    /// <summary>
    /// Gets the preview warning.
    /// </summary>
    public string? PreviewWarning { get; private set; }

    private TilesheetSource(
        string filePath,
        TilesheetDefinition definition)
    {
        FilePath = filePath;
        BaseDirectory = Path.GetDirectoryName(filePath)!;
        Definition = definition;
        LoadPreviewImage();
    }

    /// <summary>
    /// Loads tilesheet source from the supplied source.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting tilesheet source.</returns>
    public static TilesheetSource Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(path));

        path = Path.GetFullPath(path);
        return new TilesheetSource(
            path,
            TilesheetDefinitionSerializer.Load(path));
    }

    /// <summary>
    /// Creates a frame definition for the selected tilesheet region and grid coordinates.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The resulting animation frame definition.</returns>
    public AnimationFrameDefinition CreateFrame(
        TilesheetRegionDefinition region,
        int x,
        int y) =>
        new()
        {
            Tilesheet = Definition.Name,
            RegionName = region.Name,
            XTile = x,
            YTile = y
        };

    /// <summary>
    /// Attempts to resolve a frame to its tilesheet region and image-pixel bounds.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="region">When this method returns, contains the region.</param>
    /// <param name="bounds">When this method returns, contains the bounds.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryResolve(
        AnimationFrameDefinition frame,
        out TilesheetRegionDefinition? region,
        out Rectangle bounds)
    {
        region = null;
        bounds = Rectangle.Empty;

        if (!string.Equals(
                frame.Tilesheet,
                Definition.Name,
                StringComparison.Ordinal))
        {
            return false;
        }

        region = Definition.Regions.FirstOrDefault(
            candidate => string.Equals(
                candidate.Name,
                frame.RegionName,
                StringComparison.OrdinalIgnoreCase));

        if (region is null)
            return false;

        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        if (frame.XTile < 0 ||
            frame.YTile < 0 ||
            frame.XTile >= columns ||
            frame.YTile >= rows)
        {
            return false;
        }

        bounds = FrameBounds(region, frame.XTile, frame.YTile);
        return true;
    }

    /// <summary>
    /// Computes a frame's pixel bounds within the tilesheet image.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The frame rectangle in tilesheet image pixels.</returns>
    public static Rectangle FrameBounds(
        TilesheetRegionDefinition region,
        int x,
        int y) =>
        new(
            checked((int)(
                (long)region.Area.X +
                region.RegionMargin.Left +
                region.TilePadding.Left +
                x * ((long)region.TileSize.Width +
                     region.TilePadding.Left +
                     region.TilePadding.Right))),
            checked((int)(
                (long)region.Area.Y +
                region.RegionMargin.Top +
                region.TilePadding.Top +
                y * ((long)region.TileSize.Height +
                     region.TilePadding.Top +
                     region.TilePadding.Bottom))),
            region.TileSize.Width,
            region.TileSize.Height);

    private void LoadPreviewImage()
    {
        var image = Definition.Image;
        if (image is null)
        {
            PreviewWarning = "GTS does not define an image source.";
            return;
        }

        if (string.IsNullOrWhiteSpace(image.FilePath))
        {
            PreviewWarning = "Packed GTS image sources can be referenced but are not previewed by this editor yet.";
            return;
        }

        try
        {
            var path = Path.IsPathRooted(image.FilePath)
                ? image.FilePath
                : Path.GetFullPath(image.FilePath, BaseDirectory);

            using var decoded = SKBitmap.Decode(path)
                ?? throw new InvalidDataException("Unsupported or corrupt image.");

            using var rgba = new SKBitmap(
                new SKImageInfo(
                    decoded.Width,
                    decoded.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Unpremul));

            using var pixels = decoded.PeekPixels();
            if (!pixels.ReadPixels(
                    rgba.Info,
                    rgba.GetPixels(),
                    rgba.RowBytes,
                    0,
                    0))
            {
                throw new InvalidDataException("Could not decode image pixels.");
            }

            if (Definition.Mask is { } mask)
            {
                rgba.ApplyAlphaMask(
                    new SKColor(
                        mask.Red,
                        mask.Green,
                        mask.Blue,
                        mask.Alpha),
                    mask.Tolerance);
            }

            using var encoded = rgba.Encode(
                SKEncodedImageFormat.Png,
                100);
            using var stream = encoded.AsStream();
            using var bitmap = new Bitmap(stream);
            Image = new Bitmap(bitmap);
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            UnauthorizedAccessException or
            System.Runtime.InteropServices.ExternalException)
        {
            PreviewWarning = "Image cannot be previewed: " + ex.Message;
        }
    }

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        Image?.Dispose();
        Image = null;
    }
}

internal readonly record struct FramePreview(
    Bitmap Image,
    Rectangle SourceBounds);
