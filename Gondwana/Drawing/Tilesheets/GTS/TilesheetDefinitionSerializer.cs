using Gondwana.Assets;
using Gondwana.Physics.Collisions;
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
    /// Loads a tilesheet definition from a loose definition file.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <returns>The resulting tilesheet definition.</returns>
    public static TilesheetDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GTS file not found: {fullPath}", fullPath);

        try
        {
            var definition = FromJson(File.ReadAllText(fullPath), fullPath);
            ApplyDefaultSource(
                definition,
                TilesheetDefinitionSource.LooseDefinitionFile(fullPath));
            definition.LoadStamp = new(definition, filePath: fullPath);
            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GTS file: {fullPath}", ex);
        }
    }

    /// <summary>
    /// Loads a tilesheet definition from a readable stream.
    /// </summary>
    /// <param name="stream">The stream supplying the serialized content. The stream remains open.</param>
    /// <returns>The resulting tilesheet definition.</returns>
    public static TilesheetDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Saves the supplied definition or runtime content to a definition file.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <param name="definition">The persisted definition to process.</param>
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
        File.WriteAllText(fullPath, ToJson(definitionToSave));
    }

    /// <summary>
    /// Deserializes a definition from JSON.
    /// </summary>
    /// <param name="json">The JSON text to deserialize.</param>
    /// <returns>The deserialized definition.</returns>
    public static TilesheetDefinition FromJson(string json) =>
        FromJson(json, sourceDescription: null);

    /// <summary>
    /// Serializes the supplied content as JSON.
    /// </summary>
    /// <param name="definition">The persisted definition to process.</param>
    /// <returns>The serialized JSON text.</returns>
    public static string ToJson(TilesheetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonConvert.SerializeObject(definition, Settings);
    }

    /// <summary>
    /// Captures a tilesheet and its regions as a persistable definition.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="baseDirectory">The base directory used to resolve or rebase relative paths.</param>
    /// <param name="makePathsRelative">Whether to store resource paths relative to the base directory.</param>
    /// <returns>The resulting tilesheet definition.</returns>
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
            PremultiplyAlpha = tilesheet.Premultiplied && tilesheet.MaskColor is null,
            Source = TilesheetDefinitionSource.Generated()
        };
    }

    /// <summary>
    /// Saves a runtime tilesheet as a GTS definition file.
    /// </summary>
    /// <param name="filePath">The destination GTS file path.</param>
    /// <param name="tilesheet">The runtime tilesheet to serialize.</param>
    /// <param name="makePathsRelative">
    /// Whether persistent source paths should be relative to the GTS file.
    /// </param>
    /// <remarks>
    /// When <paramref name="tilesheet"/> has no file or asset source, its source
    /// bitmap is automatically persisted as a PNG beside the GTS file using the
    /// same base file name.
    /// </remarks>
    public static void Save(
        string filePath,
        Tilesheet tilesheet,
        bool makePathsRelative = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(tilesheet);

        var fullPath = Path.GetFullPath(filePath);
        if (string.IsNullOrWhiteSpace(tilesheet.ImageFilePath) &&
            string.IsNullOrWhiteSpace(tilesheet.AssetIdentifier?.AssetsFile.SourcePath))
        {
            tilesheet.PersistImageToFile(
                Path.ChangeExtension(fullPath, ".png"));
        }

        var definition = FromTilesheet(
            tilesheet,
            Path.GetDirectoryName(fullPath),
            makePathsRelative);

        Save(fullPath, definition);
    }

    /// <summary>
    /// Serializes the supplied content as JSON.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="baseDirectory">The base directory used to resolve or rebase relative paths.</param>
    /// <param name="makePathsRelative">Whether to store resource paths relative to the base directory.</param>
    /// <returns>The serialized JSON text.</returns>
    public static string ToJson(
        Tilesheet tilesheet,
        string? baseDirectory = null,
        bool makePathsRelative = false)
    {
        return ToJson(
            FromTilesheet(
                tilesheet,
                baseDirectory,
                makePathsRelative));
    }

    private static TilesheetDefinition FromJson(
        string json,
        string? sourceDescription)
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

                throw new InvalidDataException(
                    $"Failed to deserialize {source}. Result was null.");
            }

            // Maintain compatibility with files written before frame-level metadata existed,
            // and tolerate explicit null collection values from hand-authored JSON.
            definition.Regions ??= [];
            foreach (var region in definition.Regions)
            {
                if (region is null)
                    throw new InvalidDataException("GTS Regions cannot contain null entries.");
                region.Frames ??= [];
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
                string.IsNullOrWhiteSpace(assetIdentifier.AssetsFile.SourcePath))
            {
                throw new InvalidOperationException(
                    $"Tilesheet '{tilesheet.Name}' has an asset identifier, but the assets file path is missing.");
            }

            return new TilesheetImageDefinition
            {
                AssetsFilePath = NormalizePath(
                    assetIdentifier.AssetsFile.SourcePath,
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
        var definition = new TilesheetRegionDefinition
        {
            Name = region.Name,
            Area = region.Area,
            TileSize = region.TileSize,
            TilePadding = region.TilePadding,
            RegionMargin = region.RegionMargin,
            Overhang = region.Overhang,
            CollisionAdjust = region.CollisionAdjust,
            CollisionType = region.CollisionType
        };

        // Persist every frame coordinate while retaining the distinction between an
        // inherited region default (null) and an explicit frame-level override.
        for (int y = 0; y < region.Rows; y++)
        {
            for (int x = 0; x < region.Columns; x++)
            {
                CollisionAdjust? collisionAdjust =
                    region.TryGetFrameCollisionAdjustOverride(
                        x,
                        y,
                        out var frameCollisionAdjust)
                    ? frameCollisionAdjust
                    : null;

                TileCollisionType? collisionType =
                    region.TryGetFrameCollisionTypeOverride(
                        x,
                        y,
                        out var frameCollisionType)
                    ? frameCollisionType
                    : null;

                definition.Frames.Add(new TilesheetFrameDefinition
                {
                    XTile = x,
                    YTile = y,
                    CollisionAdjust = collisionAdjust,
                    CollisionType = collisionType
                });
            }
        }

        return definition;
    }

    private static TilesheetMaskDefinition? CreateMaskDefinition(Tilesheet tilesheet)
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

        if (!Path.IsPathRooted(path))
            return path.Replace('\\', '/');

        var fullPath = Path.GetFullPath(path);
        var fullBaseDirectory = Path.GetFullPath(baseDirectory);

        return Path.GetRelativePath(fullBaseDirectory, fullPath).Replace('\\', '/');
    }
}
