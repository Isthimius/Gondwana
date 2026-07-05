using SkiaSharp;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Provides factory methods for creating <see cref="Tilesheet"/> instances from various sources.
/// </summary>
internal static class TilesheetFactory
{
    /// <summary>
    /// Creates a tilesheet from an existing SkiaSharp bitmap.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="bitmap">The SkiaSharp bitmap containing the tilesheet image.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance.</returns>
    internal static Tilesheet FromBitmap(string name, SKBitmap bitmap) => new Tilesheet(name, bitmap);

    /// <summary>
    /// Creates a tilesheet by loading an image from a stream.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="stream">The stream containing the image data.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance.</returns>
    internal static Tilesheet FromStream(string name, Stream stream) => new Tilesheet(name, stream);

    /// <summary>
    /// Creates a tilesheet by loading an image from a file.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="imageFilePath">The path to the image file.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance.</returns>
    internal static Tilesheet FromImageFile(string name, string imageFilePath) => new Tilesheet(name, imageFilePath);

    /// <summary>
    /// Creates a tilesheet by loading an image from an assets file.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the tilesheet image.</param>
    /// <param name="entryName">The name of the asset entry within the assets file.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance.</returns>
    internal static Tilesheet FromAssetsFile(AssetsFile assetsFile, string entryName) => new Tilesheet(assetsFile, entryName);

    /// <summary>
    /// Creates a tilesheet by loading and parsing a GTS (Gondwana Tilesheet) definition file.
    /// </summary>
    /// <param name="gtsPath">The path to the GTS definition file.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance configured according to the definition file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="gtsPath"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the GTS file does not exist.</exception>
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

    /// <summary>
    /// Creates a tilesheet from a tilesheet definition object.
    /// </summary>
    /// <param name="definition">The tilesheet definition containing image source, regions, and masking settings.</param>
    /// <param name="baseDirectory">The base directory for resolving relative paths. If null, paths must be absolute.</param>
    /// <param name="defaultAssetsFile">The default assets file to use when the definition specifies an asset entry without an assets file path.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance configured according to the definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
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

    /// <summary>
    /// Creates a tilesheet by loading a GTS definition from an assets file.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the GTS definition.</param>
    /// <param name="gtsEntryName">The name of the GTS definition entry within the assets file.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance configured according to the definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assetsFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="gtsEntryName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the GTS definition asset cannot be found in the assets file.</exception>
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
        definition.Source = TilesheetDefinitionSource.PackedDefinitionFile(
            assetsFile.FilePath,
            gtsEntryName);

        var baseDirectory = string.IsNullOrWhiteSpace(assetsFile.FilePath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(assetsFile.FilePath));

        return FromDefinition(
            definition,
            baseDirectory,
            defaultAssetsFile: assetsFile);
    }

    #region private methods

    /// <summary>
    /// Creates a tilesheet from a definition, resolving the image source from either a file path or assets file.
    /// </summary>
    /// <param name="definition">The tilesheet definition.</param>
    /// <param name="addDefaultRegion">Whether to add a default region covering the entire tilesheet.</param>
    /// <param name="baseDirectory">The base directory for resolving relative paths.</param>
    /// <param name="defaultAssetsFile">The default assets file to use when the definition specifies an asset entry without an assets file path.</param>
    /// <returns>A new <see cref="Tilesheet"/> instance with the image loaded but no regions defined yet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the definition does not specify a valid image source.</exception>
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

    /// <summary>
    /// Loads an existing assets file from the specified path.
    /// </summary>
    /// <param name="path">The path to the assets file.</param>
    /// <returns>The loaded <see cref="AssetsFile"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the assets file does not exist.</exception>
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

    /// <summary>
    /// Resolves a path by combining it with a base directory if it is relative.
    /// </summary>
    /// <param name="path">The path to resolve, which may be relative or absolute.</param>
    /// <param name="baseDirectory">The base directory to use for resolving relative paths. If null or empty, the path is returned as-is.</param>
    /// <returns>The resolved absolute path if the path is relative and a base directory is provided; otherwise, the original path.</returns>
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
