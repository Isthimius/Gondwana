using System.Drawing;
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
    public string FilePath { get; }
    public string BaseDirectory { get; }
    public TilesheetDefinition Definition { get; }
    public Bitmap? Image { get; private set; }
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

    public static TilesheetSource Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(path));

        path = Path.GetFullPath(path);
        return new TilesheetSource(
            path,
            TilesheetDefinitionSerializer.Load(path));
    }

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
                StringComparison.Ordinal));

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

    public void Dispose()
    {
        Image?.Dispose();
        Image = null;
    }
}

internal readonly record struct FramePreview(
    Bitmap Image,
    Rectangle SourceBounds);
