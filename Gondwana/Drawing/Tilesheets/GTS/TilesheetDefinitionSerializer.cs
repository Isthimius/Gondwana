using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Provides loading and saving helpers for Gondwana tilesheet definition (.gts) files.
/// </summary>
public static class TilesheetDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// Loads a <see cref="TilesheetDefinition"/> from a .gts file.
    /// </summary>
    /// <param name="filePath">The path to the .gts file.</param>
    /// <returns>The deserialized tilesheet definition.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file cannot be deserialized.</exception>
    public static TilesheetDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GTS file not found: {fullPath}", fullPath);

        try
        {
            var json = File.ReadAllText(fullPath);
            var definition = FromJson(json, fullPath);
            ApplyDefaultSource(
                definition,
                TilesheetDefinitionSource.LooseDefinitionFile(fullPath));

            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GTS file: {fullPath}", ex);
        }
    }

    /// <summary>
    /// Loads a <see cref="TilesheetDefinition"/> from a readable stream.
    /// </summary>
    /// <param name="stream">The stream containing GTS JSON content.</param>
    /// <returns>The deserialized tilesheet definition.</returns>
    public static TilesheetDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        var json = reader.ReadToEnd();

        return FromJson(json);
    }

    /// <summary>
    /// Saves a <see cref="TilesheetDefinition"/> to a .gts file.
    /// </summary>
    /// <param name="filePath">The path to save the .gts file to.</param>
    /// <param name="definition">The tilesheet definition to save.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    public static void Save(string filePath, TilesheetDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(definition);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var definitionToSave = CreateDefinitionForSaving(definition, fullPath);

        var json = ToJson(definitionToSave);

        File.WriteAllText(fullPath, json);
    }

    /// <summary>
    /// Deserializes a <see cref="TilesheetDefinition"/> from JSON text.
    /// </summary>
    /// <param name="json">The JSON content.</param>
    /// <returns>The deserialized tilesheet definition.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or whitespace.</exception>
    /// <exception cref="InvalidDataException">Thrown when the JSON cannot be deserialized.</exception>
    public static TilesheetDefinition FromJson(string json)
    {
        return FromJson(json, sourceDescription: null);
    }

    /// <summary>
    /// Serializes a <see cref="TilesheetDefinition"/> to formatted JSON text.
    /// </summary>
    /// <param name="definition">The tilesheet definition to serialize.</param>
    /// <returns>The formatted JSON content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    public static string ToJson(TilesheetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return JsonConvert.SerializeObject(definition, Settings);
    }

    /// <summary>
    /// Creates a <see cref="TilesheetDefinition"/> from a runtime <see cref="Tilesheet"/>.
    /// </summary>
    /// <param name="tilesheet">The runtime tilesheet to convert.</param>
    /// <param name="baseDirectory">
    /// Optional base directory used when converting source paths to relative paths.
    /// Usually this should be the directory containing the .gts file.
    /// </param>
    /// <param name="makePathsRelative">
    /// Whether image and assets file paths should be written relative to <paramref name="baseDirectory"/>.
    /// </param>
    /// <returns>A serializable tilesheet definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tilesheet"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tilesheet does not have a persistent image source.
    /// </exception>
    public static TilesheetDefinition FromTilesheet(
        Tilesheet tilesheet,
        string? baseDirectory = null,
        bool makePathsRelative = false)
    {
        ArgumentNullException.ThrowIfNull(tilesheet);

        return new TilesheetDefinition
        {
            Name = tilesheet.Name,

            Image = CreateImageDefinition(
                tilesheet,
                baseDirectory,
                makePathsRelative),

            Regions = tilesheet.Regions
                .Select(CreateRegionDefinition)
                .ToList(),

            Mask = CreateMaskDefinition(tilesheet),

            // ApplyMask already premultiplies, so only set this when premultiply
            // was applied independently of a mask.
            PremultiplyAlpha = tilesheet.Premultiplied && tilesheet.MaskColor is null,

            Source = TilesheetDefinitionSource.Generated()
        };
    }

    /// <summary>
    /// Saves a runtime <see cref="Tilesheet"/> as a .gts file.
    /// </summary>
    /// <param name="filePath">The path to save the .gts file to.</param>
    /// <param name="tilesheet">The runtime tilesheet to save.</param>
    /// <param name="makePathsRelative">
    /// Whether image and assets file paths should be written relative to the .gts file directory.
    /// </param>
    public static void Save(
        string filePath,
        Tilesheet tilesheet,
        bool makePathsRelative = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(tilesheet);

        var fullPath = Path.GetFullPath(filePath);
        var baseDirectory = Path.GetDirectoryName(fullPath);

        var definition = FromTilesheet(
            tilesheet,
            baseDirectory,
            makePathsRelative);

        Save(fullPath, definition);
    }

    /// <summary>
    /// Serializes a runtime <see cref="Tilesheet"/> to formatted GTS JSON text.
    /// </summary>
    /// <param name="tilesheet">The runtime tilesheet to serialize.</param>
    /// <param name="baseDirectory">
    /// Optional base directory used when converting source paths to relative paths.
    /// </param>
    /// <param name="makePathsRelative">
    /// Whether image and assets file paths should be written relative to <paramref name="baseDirectory"/>.
    /// </param>
    /// <returns>The formatted JSON content.</returns>
    public static string ToJson(
        Tilesheet tilesheet,
        string? baseDirectory = null,
        bool makePathsRelative = false)
    {
        var definition = FromTilesheet(
            tilesheet,
            baseDirectory,
            makePathsRelative);

        return ToJson(definition);
    }

    #region private methods

    private static TilesheetDefinition FromJson(string json, string? sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("GTS JSON content must be a non-empty string.", nameof(json));

        try
        {
            var definition = JsonConvert.DeserializeObject<TilesheetDefinition>(json, Settings);

            if (definition is null)
            {
                var source = string.IsNullOrWhiteSpace(sourceDescription)
                    ? "GTS JSON content"
                    : sourceDescription;

                throw new InvalidDataException($"Failed to deserialize {source}. Result was null.");
            }

            return definition;
        }
        catch (JsonException ex)
        {
            var source = string.IsNullOrWhiteSpace(sourceDescription)
                ? "GTS JSON content"
                : sourceDescription;

            throw new InvalidDataException($"Failed to deserialize {source}.", ex);
        }
    }

    private static TilesheetImageDefinition CreateImageDefinition(
        Tilesheet tilesheet,
        string? baseDirectory,
        bool makePathsRelative)
    {
        if (!string.IsNullOrWhiteSpace(tilesheet.ImageFilePath))
        {
            return new TilesheetImageDefinition
            {
                FilePath = NormalizePath(
                    tilesheet.ImageFilePath,
                    baseDirectory,
                    makePathsRelative)
            };
        }

        if (tilesheet.AssetIdentifier is not null)
        {
            var assetIdentifier = tilesheet.AssetIdentifier;

            if (assetIdentifier.AssetType != AssetTypes.Image)
            {
                throw new InvalidOperationException(
                    $"Tilesheet '{tilesheet.Name}' references an asset of type '{assetIdentifier.AssetType}', but expected '{AssetTypes.Image}'.");
            }

            if (assetIdentifier.AssetsFile is null ||
                string.IsNullOrWhiteSpace(assetIdentifier.AssetsFile.FilePath))
            {
                throw new InvalidOperationException(
                    $"Tilesheet '{tilesheet.Name}' has an asset identifier, but the assets file path is missing.");
            }

            return new TilesheetImageDefinition
            {
                AssetsFilePath = NormalizePath(
                    assetIdentifier.AssetsFile.FilePath,
                    baseDirectory,
                    makePathsRelative),

                AssetEntryName = assetIdentifier.AssetName
            };
        }

        throw new InvalidOperationException(
            $"Tilesheet '{tilesheet.Name}' cannot be converted to a TilesheetDefinition because it has no ImageFilePath or AssetIdentifier.");
    }

    private static TilesheetRegionDefinition CreateRegionDefinition(
        TilesheetRegion region)
    {
        return new TilesheetRegionDefinition
        {
            Name = region.Name,
            Area = region.Area,
            TileSize = region.TileSize,
            TilePadding = region.TilePadding,
            RegionMargin = region.RegionMargin,
            Overhang = region.Overhang
        };
    }

    private static TilesheetMaskDefinition? CreateMaskDefinition(
        Tilesheet tilesheet)
    {
        if (tilesheet.MaskColor is null)
            return null;

        var color = tilesheet.MaskColor.Value;

        return new TilesheetMaskDefinition
        {
            Red = color.Red,
            Green = color.Green,
            Blue = color.Blue,
            Alpha = color.Alpha,
            Tolerance = tilesheet.MaskTolerance
        };
    }

    private static void ApplyDefaultSource(
        TilesheetDefinition definition,
        TilesheetDefinitionSource source)
    {
        if (definition.Source.Kind == TilesheetDefinitionSourceKind.None)
            definition.Source = source;
    }

    private static TilesheetDefinition CreateDefinitionForSaving(
        TilesheetDefinition definition,
        string fullPath)
    {
        if (definition.Source.Kind != TilesheetDefinitionSourceKind.None)
            return definition;

        var clone = FromJson(ToJson(definition));
        clone.Source = TilesheetDefinitionSource.LooseDefinitionFile(fullPath);
        return clone;
    }

    private static string NormalizePath(
        string path,
        string? baseDirectory,
        bool makeRelative)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (!makeRelative || string.IsNullOrWhiteSpace(baseDirectory))
            return path.Replace('\\', '/');

        // Preserve already-relative paths as-authored to avoid depending on the current working directory.
        if (!Path.IsPathRooted(path))
            return path.Replace('\\', '/');

        var fullPath = Path.GetFullPath(path);
        var fullBaseDirectory = Path.GetFullPath(baseDirectory);

        return Path.GetRelativePath(fullBaseDirectory, fullPath).Replace('\\', '/');
    }

    #endregion private methods
}