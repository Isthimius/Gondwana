using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Drawing.Tilesheets;

internal static class TilesheetFactory
{
    internal static Tilesheet FromBitmap(string name, SKBitmap bitmap) => new Tilesheet(name, bitmap);

    internal static Tilesheet FromStream(string name, Stream stream) => new Tilesheet(name, stream);

    internal static Tilesheet FromImageFile(string name, string imageFilePath) => new Tilesheet(name, imageFilePath);

    internal static Tilesheet FromAssetsFile(AssetsFile assetsFile, string entryName) => new Tilesheet(assetsFile, entryName);

    internal static Tilesheet FromDefinitionFile(string gtsPath)
    {
        if (string.IsNullOrWhiteSpace(gtsPath))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(gtsPath));

        var fullPath = Path.GetFullPath(gtsPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GTS file not found: {fullPath}", fullPath);

        var definition = TilesheetDefinitionSerializer.Load(fullPath);

        var baseDirectory = Path.GetDirectoryName(fullPath);

        return FromDefinition(definition, baseDirectory);
    }

    internal static Tilesheet FromDefinition(
        TilesheetDefinition definition,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tilesheet = CreateTilesheet(definition, addDefaultRegion: false, baseDirectory);

        foreach (var region in definition.Regions)
        {
            tilesheet.AddRegion(
                region.Name,
                region.Area,
                region.TileSize,
                region.TilePadding,
                region.RegionMargin,
                region.Overhang);
        }

        if (definition.Mask is not null)
        {
            tilesheet.ApplyMask(
                new SKColor(
                    definition.Mask.Red,
                    definition.Mask.Green,
                    definition.Mask.Blue,
                    definition.Mask.Alpha),
                definition.Mask.Tolerance);
        }
        else if (definition.PremultiplyAlpha)
        {
            tilesheet.ApplyPremultiplyAlpha();
        }

        return tilesheet;
    }

    internal static Tilesheet? FromSavedState(string key, Tilesheet saved)
    {
        Tilesheet rebuilt;

        // 1) Rehydrate bitmap from AssetsFile entry (preferred) or file path (fallback)
        if (saved.AssetIdentifier is not null && saved.AssetIdentifier.IsValid)
        {
            var id = saved.AssetIdentifier;
            rebuilt = new Tilesheet(id.AssetsFile, id.AssetName, false);
        }
        else if (!string.IsNullOrWhiteSpace(saved.ImageFilePath) && File.Exists(saved.ImageFilePath))
        {
            rebuilt = new Tilesheet(saved.Name, saved.ImageFilePath, false);
        }
        else
        {
            Engine.Logger.LogWarning(
                "EngineState: Skipping tilesheet '{Key}' because it has no valid AssetIdentifier and no ImageFilePath.",
                key);
            return null;
        }

        // 2) Restore metadata.
        rebuilt.Name = saved.Name;

        // 3) Restore regions.
        //
        // IMPORTANT: Deserialized TilesheetRegion instances are saved-state specs.
        // They do not own a live Tilesheet reference after JSON deserialization.
        // Recreate live regions on the rebuilt tilesheet so each region is attached
        // to the rebuilt source bitmap and can build its own cache.
        foreach (var savedRegion in saved.Regions)
        {
            rebuilt.AddRegion(
                savedRegion.Name,
                savedRegion.Area,
                savedRegion.TileSize,
                savedRegion.TilePadding,
                savedRegion.RegionMargin,
                savedRegion.Overhang);
        }

        // 4) Restore extensible tilesheet metadata
        rebuilt.ValueBag = saved.ValueBag.Clone();

        // 5) Reapply bitmap transforms recorded in the save.
        //
        // IMPORTANT: SkBitmap is not serialized, so these operations must be replayed here.
        // ApplyMask() also premultiplies alpha internally in the implementation.
        if (saved.MaskColor is not null)
        {
            rebuilt.ApplyMask(saved.MaskColor, saved.MaskTolerance);
        }
        else if (saved.Premultiplied)
        {
            // ApplyMask also premultiplies alpha internally,
            // so only call if Premultiplied and no MaskColor
            rebuilt.ApplyPremultiplyAlpha();
        }

        return rebuilt;
    }

    #region private methods

    private static Tilesheet CreateTilesheet(
        TilesheetDefinition definition,
        bool addDefaultRegion,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var image = definition.Image
            ?? throw new InvalidOperationException(
                "TilesheetDefinition must specify an image source.");

        if (!string.IsNullOrWhiteSpace(image.FilePath))
        {
            var imagePath = ResolvePath(image.FilePath, baseDirectory);

            return new Tilesheet(
                definition.Name,
                imagePath,
                addDefaultRegion);
        }

        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath) &&
            !string.IsNullOrWhiteSpace(image.AssetEntryName))
        {
            var assetsPath = ResolvePath(image.AssetsFilePath, baseDirectory);

            if (!File.Exists(assetsPath))
                throw new FileNotFoundException(
                    $"Assets file not found: {assetsPath}",
                    assetsPath);

            var assetsFile = AssetsFile.LoadOrCreate(assetsPath);

            return new Tilesheet(
                assetsFile,
                image.AssetEntryName,
                addDefaultRegion);
        }

        throw new InvalidOperationException(
            "TilesheetDefinition must specify either Image.FilePath or Image.AssetsFilePath + Image.AssetEntryName.");
    }
    private static string ResolvePath(string path, string? baseDirectory)
    {
        if (Path.IsPathRooted(path))
            return path;

        if (string.IsNullOrWhiteSpace(baseDirectory))
            return path;

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    #endregion
}
