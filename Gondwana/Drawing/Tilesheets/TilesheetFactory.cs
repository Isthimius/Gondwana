using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Provides factory methods for creating <see cref="Tilesheet"/> instances from supported sources.
/// </summary>
internal static class TilesheetFactory
{
    #region factory methods

    internal static Tilesheet FromBitmap(string name, SKBitmap bitmap) => new(name, bitmap);

    internal static Tilesheet FromStream(string name, Stream stream) => new(name, stream);

    internal static Tilesheet FromImageFile(string name, string imageFilePath) => new(name, imageFilePath);

    internal static Tilesheet FromAssetsFile(AssetsFile assetsFile, string entryName) => new(assetsFile, entryName);

    internal static Tilesheet FromDefinitionFile(string gtsPath)
    {
        if (string.IsNullOrWhiteSpace(gtsPath))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(gtsPath));

        var fullPath = Path.GetFullPath(gtsPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GTS file not found: {fullPath}", fullPath);

        var definition = TilesheetDefinitionSerializer.Load(fullPath);
        return FromDefinition(definition, Path.GetDirectoryName(fullPath));
    }

    internal static Tilesheet FromDefinition(TilesheetDefinition definition,
                                             string? baseDirectory = null,
                                             AssetsFile? defaultAssetsFile = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tilesheet = CreateTilesheet(
            definition,
            addDefaultRegion: false,
            baseDirectory,
            defaultAssetsFile);

        foreach (var region in definition.Regions)
        {
            var runtimeRegion = tilesheet.AddRegion(
                region.Name,
                region.Area,
                region.TileSize,
                region.TilePadding,
                region.RegionMargin,
                region.Overhang,
                region.CollisionAdjust);

            // A missing frame value inherits the region default. A present value is
            // an explicit override, even when it currently equals that default.
            foreach (var frame in region.Frames ?? [])
            {
                if (frame.CollisionAdjust is { } adjust)
                {
                    runtimeRegion.SetFrameCollisionAdjust(
                        frame.XTile,
                        frame.YTile,
                        adjust);
                }
            }
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

    internal static Tilesheet FromDefinitionAsset(AssetsFile assetsFile, string gtsEntryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);

        if (string.IsNullOrWhiteSpace(gtsEntryName))
        {
            throw new ArgumentException(
                "GTS asset entry name must be a non-empty string.",
                nameof(gtsEntryName));
        }

        using var stream = assetsFile.Get(AssetTypes.TilesheetDefinition, gtsEntryName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Tilesheet definition asset '{gtsEntryName}' could not be found in AssetsFile '{assetsFile.FilePath}'.");
        }

        var definition = TilesheetDefinitionSerializer.Load(stream);

        if (!string.IsNullOrWhiteSpace(assetsFile.FilePath))
        {
            definition.Source = TilesheetDefinitionSource.PackedDefinitionFile(
                assetsFile.FilePath,
                gtsEntryName);
        }

        var baseDirectory = string.IsNullOrWhiteSpace(assetsFile.FilePath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(assetsFile.FilePath));

        return FromDefinition(
            definition,
            baseDirectory,
            defaultAssetsFile: assetsFile);
    }

    #endregion factory methods

    #region private methods

    private static Tilesheet CreateTilesheet(
        TilesheetDefinition definition,
        bool addDefaultRegion,
        string? baseDirectory = null,
        AssetsFile? defaultAssetsFile = null)
    {
        var image = definition.Image
            ?? throw new InvalidOperationException(
                "TilesheetDefinition must specify an image source.");

        ValidateImageDefinition(image, defaultAssetsFile);

        if (!string.IsNullOrWhiteSpace(image.FilePath))
        {
            return new Tilesheet(
                definition.Name,
                ResolvePath(image.FilePath, baseDirectory),
                addDefaultRegion);
        }

        AssetsFile assetsFile;

        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath))
        {
            assetsFile = LoadExistingAssetsFile(
                ResolvePath(image.AssetsFilePath, baseDirectory));
        }
        else
        {
            assetsFile = defaultAssetsFile!;
        }

        var tilesheet = new Tilesheet(
            assetsFile,
            image.AssetEntryName!,
            addDefaultRegion);

        // The asset entry identifies the image; it is not necessarily the logical
        // name assigned to the tilesheet by the GTS definition.
        tilesheet.Name = definition.Name;

        return tilesheet;
    }

    private static void ValidateImageDefinition(TilesheetImageDefinition image, AssetsFile? defaultAssetsFile)
    {
        var hasFilePath = !string.IsNullOrWhiteSpace(image.FilePath);
        var hasAssetsFilePath = !string.IsNullOrWhiteSpace(image.AssetsFilePath);
        var hasAssetEntryName = !string.IsNullOrWhiteSpace(image.AssetEntryName);

        if (hasFilePath && (hasAssetsFilePath || hasAssetEntryName))
        {
            throw new InvalidOperationException(
                "TilesheetDefinition image source is ambiguous. Image.FilePath cannot be combined with Image.AssetsFilePath or Image.AssetEntryName.");
        }

        if (hasAssetsFilePath && !hasAssetEntryName)
        {
            throw new InvalidOperationException(
                "TilesheetDefinition image specifies an AssetsFilePath but does not specify an AssetEntryName.");
        }

        if (hasFilePath)
            return;

        if (!hasAssetEntryName)
        {
            throw new InvalidOperationException(
                "TilesheetDefinition must specify either Image.FilePath or Image.AssetEntryName.");
        }

        if (!hasAssetsFilePath && defaultAssetsFile is null)
        {
            throw new InvalidOperationException(
                "TilesheetDefinition image specifies an AssetEntryName but does not specify an AssetsFilePath, and no default AssetsFile was provided.");
        }
    }

    private static AssetsFile LoadExistingAssetsFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Assets file not found: {fullPath}", fullPath);

        return AssetsFile.LoadOrCreate(fullPath);
    }

    private static string ResolvePath(string path, string? baseDirectory)
    {
        if (Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(baseDirectory))
            return path;

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    #endregion private methods
}
