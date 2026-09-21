using System.Numerics;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>Clean GSPR persistence and explicit runtime materialization.</summary>
public static class SpriteDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public static SpriteDefinition Load(string filePath)
    {
        var path = Path.GetFullPath(filePath);
        var definition = FromJson(File.ReadAllText(path));
        definition.Source = SpriteDefinitionSource.LooseDefinitionFile(path);
        return definition;
    }

    public static SpriteDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromJson(reader.ReadToEnd());
    }

    public static SpriteDefinition Load(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        using var stream = assetsFile.Get(AssetTypes.SpriteDefinition, entryName)
            ?? throw new FileNotFoundException($"GSPR asset entry not found: {entryName}");
        var definition = Load(stream);
        definition.Source = SpriteDefinitionSource.PackedDefinitionFile(assetsFile.FilePath, entryName);
        return definition;
    }

    public static string ToJson(SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonConvert.SerializeObject(definition, Settings);
    }

    public static SpriteDefinition FromJson(string json)
    {
        try
        {
            var definition = JsonConvert.DeserializeObject<SpriteDefinition>(json, Settings)
                ?? throw new InvalidDataException("GSPR definition is null.");
            definition.Sprites ??= [];
            definition.TilesheetSources ??= [];
            definition.SceneSources ??= [];
            if (definition.Sprites.Any(sprite => sprite is null) ||
                definition.TilesheetSources.Any(source => source is null) ||
                definition.SceneSources.Any(source => source is null))
                throw new InvalidDataException("GSPR collections cannot contain null entries.");
            return definition;
        }
        catch (JsonException ex) { throw new InvalidDataException("Failed to deserialize GSPR JSON.", ex); }
    }

    public static void Save(string filePath, SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var path = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(path)!;
        var clone = Rebase(definition, directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToJson(clone));
    }

    /// <summary>Clones and rebases authoring locations without changing the source document.</summary>
    public static SpriteDefinition Rebase(SpriteDefinition definition, string directory)
    {
        var clone = FromJson(ToJson(definition));
        var origin = definition.Source.GsprFilePath ?? definition.Source.AssetsFilePath;
        if (string.IsNullOrWhiteSpace(origin)) return clone;
        var oldDirectory = Path.GetDirectoryName(Path.GetFullPath(origin))!;
        string? RebasePath(string? path) => string.IsNullOrWhiteSpace(path) ? path :
            Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path, oldDirectory));
        foreach (var source in clone.TilesheetSources)
        {
            source.GtsPath = RebasePath(source.GtsPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        foreach (var source in clone.SceneSources)
        {
            source.GscnPath = RebasePath(source.GscnPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        return clone;
    }

    public static void Save(string filePath, IEnumerable<Sprite> sprites) => Save(filePath, FromSprites(sprites));
    public static void Save(string filePath, Sprite sprite) => Save(filePath, FromSprites([sprite]));

    public static SpriteDefinition FromSprites(IEnumerable<Sprite> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        return new() { Sprites = sprites.Select(FromSprite).ToList(), Source = SpriteDefinitionSource.Generated() };
    }

    public static SpriteInstanceDefinition FromSprite(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        var frame = sprite.CurrentFrame;
        return new()
        {
            Id = sprite.Id, Nickname = sprite.Nickname,
            SceneId = sprite.SceneLayer.Scene?.ID ?? string.Empty, SceneLayerId = sprite.SceneLayer.ID,
            Position = sprite.SceneLayerCoordinates,
            Frame = frame.Tilesheet is null ? null : new() { Tilesheet = frame.Tilesheet.Name, RegionName = frame.RegionName, XTile = frame.XTile, YTile = frame.YTile },
            Visible = sprite.Visible, ZOrder = sprite.ZOrder, Rotation = sprite.Rotation,
            HorizAlign = sprite.HorizAlign, VertAlign = sprite.VertAlign,
            NudgeX = sprite.NudgeX, NudgeY = sprite.NudgeY, RenderSize = sprite.RenderSize,
            EnableFog = sprite.EnableFog, AdjustCollisionArea = sprite.AdjustCollisionArea,
            AdjustCollisionAreaByFrame = sprite.AdjustCollisionAreaByFrame,
            CollisionType = sprite.CollisionType, CollisionTypeByFrame = sprite.CollisionTypeByFrame,
            CollisionProfileName = sprite.CollisionProfileName, CollisionsEnabled = sprite.CollisionsEnabled
        };
    }

    public static List<Sprite> LoadSprites(string filePath) => ToSprites(Load(filePath));
    public static List<Sprite> LoadSprites(AssetsFile assetsFile, string entryName) => ToSprites(Load(assetsFile, entryName));
    public static Sprite ToSprite(SpriteInstanceDefinition definition) => ToSprites(new() { Sprites = [definition] })[0];

    /// <summary>Resolves only registered runtime dependencies. Rolls back on any failure.</summary>
    public static List<Sprite> ToSprites(SpriteDefinition definition)
        => ToSprites(definition, allowDuplicateNicknames: false);

    // EngineState retains its historical last-incoming-nickname-wins merge contract.
    internal static List<Sprite> ToSprites(SpriteDefinition definition, bool allowDuplicateNicknames)
    {
        var errors = SpriteDefinitionValidator.Validate(definition, allowDuplicateNicknames);
        if (errors.Count != 0) throw new InvalidDataException("Invalid GSPR definition:\n" + string.Join("\n", errors));
        var created = new List<Sprite>();
        try
        {
            foreach (var entry in definition.Sprites)
            {
                var scenes = Scene._allScenes.Where(scene => scene.ID == entry.SceneId).Take(2).ToArray();
                if (scenes.Length != 1)
                    throw new InvalidDataException($"Sprite '{entry.Nickname}': Scene '{entry.SceneId}' is missing or ambiguous.");
                var scene = scenes[0];
                var layers = scene.SceneLayers.Where(layer => layer.ID == entry.SceneLayerId).Take(2).ToArray();
                if (layers.Length != 1)
                    throw new InvalidDataException($"Sprite '{entry.Nickname}': SceneLayer '{entry.SceneLayerId}' is missing or ambiguous.");
                var layer = layers[0];
                var frame = entry.Frame is null ? default : ResolveFrame(entry.Frame);
                if (!string.IsNullOrWhiteSpace(entry.CollisionProfileName) && !scene.CollisionProfiles.GetProfileNames().Contains(entry.CollisionProfileName))
                    throw new InvalidDataException($"Sprite '{entry.Nickname}': collision profile '{entry.CollisionProfileName}' does not exist.");
                var sprite = SpriteManager.Instance.CreateSprite(layer, frame, entry.Nickname, entry.CollisionProfileName);
                created.Add(sprite);
                if (entry.Id != Guid.Empty) sprite.Id = entry.Id;
                sprite.SetPosition(new Vector2(entry.Position.X, entry.Position.Y));
                sprite.Visible = entry.Visible;
                sprite.ZOrder = entry.ZOrder;
                sprite.Rotation = entry.Rotation;
                sprite.HorizAlign = entry.HorizAlign;
                sprite.VertAlign = entry.VertAlign;
                sprite.NudgeX = entry.NudgeX;
                sprite.NudgeY = entry.NudgeY;
                sprite.RenderSize = entry.RenderSize;
                sprite.EnableFog = entry.EnableFog;
                sprite.AdjustCollisionArea = entry.AdjustCollisionArea;
                sprite.CollisionType = entry.CollisionType;
                sprite.AdjustCollisionAreaByFrame = entry.AdjustCollisionAreaByFrame;
                sprite.CollisionTypeByFrame = entry.CollisionTypeByFrame;
                sprite.CollisionsEnabled = entry.CollisionsEnabled;
            }
            return created;
        }
        catch
        {
            foreach (var sprite in created)
            {
                SpriteManager.Instance._spriteList.Remove(sprite);
                sprite.DisposeImmediate();
            }
            throw;
        }
    }

    private static Frame ResolveFrame(SpriteFrameDefinition definition)
    {
        var sheet = TilesheetRegistry.Instance.GetOrNull(definition.Tilesheet)
            ?? throw new InvalidDataException($"GSPR tilesheet '{definition.Tilesheet}' is not registered.");
        var region = sheet.GetRegion(definition.RegionName)
            ?? throw new InvalidDataException($"GSPR region '{definition.RegionName}' is missing on '{definition.Tilesheet}'.");
        if (definition.XTile >= region.Columns || definition.YTile >= region.Rows)
            throw new InvalidDataException($"GSPR frame ({definition.XTile}, {definition.YTile}) is outside region '{definition.RegionName}'.");
        return sheet.GetFrame(definition.RegionName, definition.XTile, definition.YTile);
    }
}
