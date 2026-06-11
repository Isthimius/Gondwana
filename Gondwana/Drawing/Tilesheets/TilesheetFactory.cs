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

    internal static Tilesheet FromDefinitionAsset(
        AssetsFile assetsFile,
        string gtsEntryName)
    {
        if (assetsFile is null)
            throw new ArgumentNullException(nameof(assetsFile));

        if (string.IsNullOrWhiteSpace(gtsEntryName))
            throw new ArgumentException("GTS asset entry name must be a non-empty string.", nameof(gtsEntryName));

        using var stream = assetsFile.Get(
            AssetTypes.TilesheetDefinition,
            gtsEntryName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Tilesheet definition asset '{gtsEntryName}' could not be found in AssetsFile '{assetsFile.FilePath}'.");
        }

        var definition = TilesheetDefinitionSerializer.Load(stream);

        var baseDirectory = string.IsNullOrWhiteSpace(assetsFile.FilePath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(assetsFile.FilePath));

        return FromDefinition(
            definition,
            baseDirectory,
            defaultAssetsFile: assetsFile);
    }

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

        if (!string.IsNullOrWhiteSpace(image.FilePath))
        {
            var imagePath = ResolvePath(image.FilePath, baseDirectory);

            return new Tilesheet(
                definition.Name,
                imagePath,
                addDefaultRegion);
        }

        if (!string.IsNullOrWhiteSpace(image.AssetEntryName))
        {
            AssetsFile assetsFile;

            if (!string.IsNullOrWhiteSpace(image.AssetsFilePath))
            {
                var assetsPath = ResolvePath(image.AssetsFilePath, baseDirectory);
                assetsFile = LoadExistingAssetsFile(assetsPath);
            }
            else if (defaultAssetsFile is not null)
            {
                assetsFile = defaultAssetsFile;
            }
            else
            {
                throw new InvalidOperationException(
                    "TilesheetDefinition image specifies an AssetEntryName but does not specify an AssetsFilePath, and no default AssetsFile was provided.");
            }

            return new Tilesheet(
                assetsFile,
                image.AssetEntryName,
                addDefaultRegion);
        }

        throw new InvalidOperationException(
            "TilesheetDefinition must specify either Image.FilePath or Image.AssetEntryName.");
    }

    private static AssetsFile LoadExistingAssetsFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(path));

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                $"Assets file not found: {fullPath}",
                fullPath);

        return AssetsFile.LoadOrCreate(fullPath);
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
