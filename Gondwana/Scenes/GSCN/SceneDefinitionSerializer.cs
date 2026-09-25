using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Provides loading, saving, conversion, and materialization helpers for Gondwana scene (.gscn) files.
/// </summary>
public static class SceneDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// Loads a loose GSCN definition file.
    /// </summary>
    public static SceneDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GSCN file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GSCN file not found: {fullPath}", fullPath);

        try
        {
            var definition = FromJson(File.ReadAllText(fullPath), fullPath);
            ApplyDefaultSource(definition, SceneDefinitionSource.LooseDefinitionFile(fullPath));
            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GSCN file: {fullPath}", ex);
        }
    }

    /// <summary>
    /// Loads a GSCN definition from a readable stream.
    /// </summary>
    public static SceneDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Loads a GSCN definition stored in an assets file.
    /// </summary>
    public static SceneDefinition Load(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);

        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("GSCN asset entry name must be a non-empty string.", nameof(entryName));

        using var stream = assetsFile.Get(AssetTypes.SceneDefinition, entryName)
            ?? throw new FileNotFoundException($"GSCN asset entry not found: {entryName}", entryName);

        var definition = Load(stream);
        if (definition.Source.Kind == SceneDefinitionSourceKind.None &&
            !string.IsNullOrWhiteSpace(assetsFile.FilePath))
        {
            definition.Source = SceneDefinitionSource.PackedDefinitionFile(
                assetsFile.FilePath,
                entryName);
        }

        return definition;
    }

    /// <summary>
    /// Saves a GSCN definition to a file.
    /// </summary>
    public static void Save(string filePath, SceneDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GSCN file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(definition);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var definitionToSave = CreateDefinitionForSaving(definition, fullPath);
        File.WriteAllText(fullPath, ToJson(definitionToSave));
    }

    /// <summary>
    /// Converts and saves a runtime scene as a GSCN file.
    /// </summary>
    public static void Save(string filePath, Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Save(filePath, FromScene(scene));
    }

    /// <summary>
    /// Deserializes a GSCN definition from JSON.
    /// </summary>
    public static SceneDefinition FromJson(string json) =>
        FromJson(json, sourceDescription: null);

    /// <summary>
    /// Serializes a GSCN definition to JSON.
    /// </summary>
    public static string ToJson(SceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonConvert.SerializeObject(definition, Settings);
    }

    /// <summary>
    /// Converts a runtime scene to the clean GSCN definition model.
    /// </summary>
    public static SceneDefinition FromScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var definition = new SceneDefinition
        {
            ID = scene.ID,
            CollisionGroups = scene.CollisionGroups.GetGroupNames().ToList(),
            CollisionProfiles = scene.CollisionProfiles.GetProfileNames()
                .Select(name =>
                {
                    var profile = scene.CollisionProfiles.Get(name);
                    return new SceneCollisionProfileDefinition
                    {
                        Name = profile.Name,
                        CollisionGroup = profile.CollisionGroup,
                        CollidesWith = profile.CollidesWith.ToList(),
                        CollidesWithAll = profile.CollidesWithAll
                    };
                })
                .ToList(),
            Source = SceneDefinitionSource.Generated()
        };

        foreach (var layer in scene.SceneLayers)
            definition.Layers.Add(CreateLayerDefinition(layer));

        return definition;
    }

    /// <summary>
    /// Serializes a runtime scene directly to GSCN JSON.
    /// </summary>
    public static string ToJson(Scene scene) => ToJson(FromScene(scene));

    /// <summary>
    /// Materializes a runtime scene from a GSCN definition.
    /// Referenced tilesheets must already be registered in <see cref="TilesheetRegistry"/>,
    /// and referenced animation keys must already exist in the GANI/cycle registry.
    /// </summary>
    public static Scene ToScene(SceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = SceneDefinitionValidator.Validate(definition);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(
                "GSCN definition is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }

        var scene = new Scene();

        try
        {
            if (!string.IsNullOrWhiteSpace(definition.ID))
                scene.ID = definition.ID;

            foreach (var group in definition.CollisionGroups ?? [])
                scene.CollisionGroups.Define(group);

            foreach (var profile in definition.CollisionProfiles ?? [])
            {
                scene.CollisionProfiles.Define(
                    profile.Name,
                    profile.CollisionGroup,
                    profile.CollidesWith,
                    profile.CollidesWithAll);
            }

            foreach (var layerDefinition in definition.Layers ?? [])
                MaterializeLayer(scene, layerDefinition);

            return scene;
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Loads a loose GSCN file and materializes a runtime scene.
    /// </summary>
    public static Scene LoadScene(string filePath) => ToScene(Load(filePath));

    /// <summary>
    /// Loads a packed GSCN definition and materializes a runtime scene.
    /// </summary>
    public static Scene LoadScene(AssetsFile assetsFile, string entryName) =>
        ToScene(Load(assetsFile, entryName));

    private static SceneDefinition FromJson(string json, string? sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("GSCN JSON content must be a non-empty string.", nameof(json));

        try
        {
            var definition = JsonConvert.DeserializeObject<SceneDefinition>(json, Settings);
            if (definition is null)
            {
                var source = string.IsNullOrWhiteSpace(sourceDescription)
                    ? "GSCN JSON content"
                    : sourceDescription;

                throw new InvalidDataException($"Failed to deserialize {source}. Result was null.");
            }

            definition.CollisionGroups ??= [];
            definition.CollisionProfiles ??= [];
            definition.TilesheetSources ??= [];
            definition.AnimationSources ??= [];
            definition.Layers ??= [];

            for (int i = 0; i < definition.TilesheetSources.Count; i++)
            {
                if (definition.TilesheetSources[i] is null)
                    throw new InvalidDataException($"GSCN TilesheetSources cannot contain a null entry at index {i}.");
            }

            for (int i = 0; i < definition.AnimationSources.Count; i++)
            {
                if (definition.AnimationSources[i] is null)
                    throw new InvalidDataException($"GSCN AnimationSources cannot contain a null entry at index {i}.");
            }

            foreach (var profile in definition.CollisionProfiles)
            {
                if (profile is null)
                    throw new InvalidDataException("GSCN CollisionProfiles cannot contain null entries.");
                profile.CollidesWith ??= [];
            }

            foreach (var layer in definition.Layers)
            {
                if (layer is null)
                    throw new InvalidDataException("GSCN Layers cannot contain null entries.");
                layer.Tiles ??= [];
            }

            return definition;
        }
        catch (JsonException ex)
        {
            var source = string.IsNullOrWhiteSpace(sourceDescription)
                ? "GSCN JSON content"
                : sourceDescription;

            throw new InvalidDataException($"Failed to deserialize {source}.", ex);
        }
    }

    private static SceneLayerDefinition CreateLayerDefinition(SceneLayer layer)
    {
        var definition = new SceneLayerDefinition
        {
            ID = layer.ID,
            CoordinateSystemType = layer.CoordinateSystemType,
            Columns = layer.GridColumnCount,
            Rows = layer.GridRowCount,
            TileWidth = layer.TileWidth,
            TileHeight = layer.TileHeight,
            ZOrder = layer.ZOrder,
            Parallax = layer.Parallax,
            Visible = layer.Visible,
            WrapHorizontally = layer.WrapHorizontally,
            WrapVertically = layer.WrapVertically,
            ShowGridLines = layer.ShowGridLines,
            ShowCollisionBoxes = layer.ShowCollisionBoxes,
            OriginPx = layer.OriginPx,
            DefaultTileCollisionProfile = layer.DefaultTileCollisionProfile
        };

        for (int x = 0; x < layer.GridColumnCount; x++)
        {
            for (int y = 0; y < layer.GridRowCount; y++)
            {
                var tile = layer[x, y]!;
                definition.Tiles.Add(CreateTileDefinition(tile, x, y));
            }
        }

        return definition;
    }

    private static SceneLayerTileDefinition CreateTileDefinition(
        SceneLayerTile tile,
        int x,
        int y)
    {
        SceneFrameDefinition? frame = null;
        if (tile.CurrentFrame.Tilesheet is not null)
        {
            frame = new SceneFrameDefinition
            {
                Tilesheet = tile.CurrentFrame.Tilesheet.Name,
                RegionName = tile.CurrentFrame.RegionName,
                XTile = tile.CurrentFrame.XTile,
                YTile = tile.CurrentFrame.YTile
            };
        }

        return new SceneLayerTileDefinition
        {
            X = x,
            Y = y,
            Id = tile.Id,
            Nickname = tile.Nickname,
            Visible = tile.Visible,
            Frame = frame,
            EnableAnimator = tile.EnableAnimator,
            AnimationKey = tile.EnableAnimator
                ? tile.TileAnimator.CurrentCycle?.CycleKey
                : null,
            StartAnimation = tile.EnableAnimator &&
                tile.TileAnimator.IsCycling,
            EnableFog = tile.EnableFog,
            AdjustCollisionAreaByFrame = tile.AdjustCollisionAreaByFrame,
            AdjustCollisionArea = tile.AdjustCollisionArea,
            CollisionType = tile.CollisionType,
            CollisionTypeByFrame = tile.CollisionTypeByFrame,
            CollisionProfileName = tile.CollisionProfileName
        };
    }

    private static void MaterializeLayer(
        Scene scene,
        SceneLayerDefinition definition)
    {
        var layer = scene.AddLayer(
            definition.Columns,
            definition.Rows,
            definition.TileWidth,
            definition.TileHeight,
            definition.ZOrder,
            definition.Parallax,
            definition.CoordinateSystemType);

        if (!string.IsNullOrWhiteSpace(definition.ID))
            layer.ID = definition.ID;

        layer.Visible = definition.Visible;
        layer.WrapHorizontally = definition.WrapHorizontally;
        layer.WrapVertically = definition.WrapVertically;
        layer.ShowGridLines = definition.ShowGridLines;
        layer.ShowCollisionBoxes = definition.ShowCollisionBoxes;
        layer.OriginPx = definition.OriginPx;
        layer.DefaultTileCollisionProfile = definition.DefaultTileCollisionProfile;

        foreach (var tileDefinition in definition.Tiles ?? [])
        {
            var tile = layer[tileDefinition.X, tileDefinition.Y]!;
            MaterializeTile(tile, tileDefinition);
        }
    }

    private static void MaterializeTile(
        SceneLayerTile tile,
        SceneLayerTileDefinition definition)
    {
        if (definition.Id != Guid.Empty)
            tile.Id = definition.Id;

        tile.Nickname = definition.Nickname;

        if (definition.Frame is { } frame)
            tile.CurrentFrame = ResolveFrame(frame);

        tile.Visible = definition.Visible;
        tile.EnableFog = definition.EnableFog;

        // Apply explicit values first. Enabling a *ByFrame flag immediately
        // derives the effective value from CurrentFrame, so those flags must be last.
        tile.AdjustCollisionArea = definition.AdjustCollisionArea;
        tile.CollisionType = definition.CollisionType;

        if (!string.IsNullOrWhiteSpace(definition.CollisionProfileName))
            tile.SetCollisionProfile(definition.CollisionProfileName);

        tile.AdjustCollisionAreaByFrame = definition.AdjustCollisionAreaByFrame;
        tile.CollisionTypeByFrame = definition.CollisionTypeByFrame;

        var animationKey = string.IsNullOrWhiteSpace(definition.AnimationKey)
            ? null
            : definition.AnimationKey.Trim();

        tile.EnableAnimator =
            definition.EnableAnimator ||
            animationKey is not null;

        if (animationKey is not null)
        {
            var cycle = Cycle.GetAnimationCycle(animationKey)
                ?? throw new InvalidDataException(
                    $"GSCN references animation '{animationKey}', but no matching GANI/cycle is registered.");

            tile.TileAnimator.CurrentCycle = cycle;

            if (definition.StartAnimation)
                tile.TileAnimator.StartAnimation();
        }
    }

    private static Frame ResolveFrame(SceneFrameDefinition definition)
    {
        var tilesheet = TilesheetRegistry.Instance.GetOrNull(definition.Tilesheet)
            ?? throw new InvalidDataException(
                $"GSCN references tilesheet '{definition.Tilesheet}', but it is not registered.");

        var region = tilesheet.GetRegion(definition.RegionName)
            ?? throw new InvalidDataException(
                $"GSCN references region '{definition.RegionName}' on tilesheet '{definition.Tilesheet}', but it does not exist.");

        if (definition.XTile < 0 ||
            definition.YTile < 0 ||
            definition.XTile >= region.Columns ||
            definition.YTile >= region.Rows)
        {
            throw new InvalidDataException(
                $"GSCN frame ({definition.XTile}, {definition.YTile}) is outside region '{definition.RegionName}' on tilesheet '{definition.Tilesheet}'.");
        }

        return tilesheet.GetFrame(
            definition.RegionName,
            definition.XTile,
            definition.YTile);
    }

    private static void ApplyDefaultSource(
        SceneDefinition definition,
        SceneDefinitionSource source)
    {
        if (definition.Source.Kind == SceneDefinitionSourceKind.None)
            definition.Source = source;
    }

    private static SceneDefinition CreateDefinitionForSaving(
        SceneDefinition definition,
        string fullPath)
    {
        if (definition.Source.Kind != SceneDefinitionSourceKind.None)
            return definition;

        var clone = FromJson(ToJson(definition));
        clone.Source = SceneDefinitionSource.LooseDefinitionFile(fullPath);
        return clone;
    }
}
