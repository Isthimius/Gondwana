using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Assets;

internal sealed partial class SemanticPackPlan
{
    private void Analyze(Entry owner)
    {
        if (owner.Document is null) return;
        string PackedPath(AssetLocation location) => Path.GetRelativePath(Path.GetDirectoryName(owner.Location.Path)!, location.Path);
        foreach (var (identity, member) in owner.Document.Members())
        {
            _referenceMember = identity;
            _cancellation.ThrowIfCancellationRequested();
            var label = $"{owner.Location} :: member '{identity}'";
            switch (member)
            {
                case TilesheetDefinition sheet:
                    {
                        var image = sheet.Image ?? throw new InvalidDataException($"{label}: missing Image.");
                        var source = Physical(owner, AssetTypes.Image, image.FilePath, image.AssetsFilePath, image.AssetEntryName, null, label + " :: Image");
                        Map(owner, source, Resolve(source, null, label + " :: Image").Destination);
                        break;
                    }
                case AudioDefinition audio:
                    for (int resourceIndex = 0; resourceIndex < audio.Resources.Count; resourceIndex++)
                    {
                        var resource = audio.Resources[resourceIndex];
                        if (resource.SourceKind == AudioResourceSourceKind.Uri)
                        {
                            _importPreview?.Add($"External URI (unchanged): {resource.SourceUri}");
                            continue;
                        }
                        var resourceLabel = label + $" :: Resources[{resourceIndex}] audio '{resource.Key}'";
                        var source = Physical(owner, AssetTypes.Audio, resource.FilePath, resource.AssetsFilePath, resource.AssetEntryName, (int)resource.SourceKind, resourceLabel);
                        Map(owner, source, Resolve(source, null, resourceLabel).Destination);
                    }
                    break;
                case AnimationDefinition animation:
                    Sources(owner, label, AssetTypes.TilesheetDefinition, animation.Frames.Select(frame => frame.Tilesheet),
                        animation.TilesheetSources, source => source.Tilesheet,
                        source => Physical(owner, AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Tilesheet '" + source.Tilesheet + "'"),
                        (key, location) => location.AssetEntryName is null ? AnimationTilesheetSourceDefinition.Loose(key, location.Path) :
                            AnimationTilesheetSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                    if (_referenceResolver is null && !string.IsNullOrWhiteSpace(animation.NextCycleKey) &&
                        !StringComparer.OrdinalIgnoreCase.Equals(animation.NextCycleKey, animation.Key))
                        Logical(AssetTypes.AnimationDefinition, animation.NextCycleKey, label + " :: NextCycleKey");
                    break;
                case SceneDefinition scene:
                    {
                        var tiles = scene.Layers.SelectMany(layer => layer.Tiles).ToArray();
                        Sources(owner, label, AssetTypes.TilesheetDefinition, tiles.Where(tile => tile.Frame is not null).Select(tile => tile.Frame!.Tilesheet),
                            scene.TilesheetSources, source => source.Tilesheet,
                            source => Physical(owner, AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Tilesheet '" + source.Tilesheet + "'"),
                            (key, location) => location.AssetEntryName is null ? SceneTilesheetSourceDefinition.Loose(key, location.Path) :
                                SceneTilesheetSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                        Sources(owner, label, AssetTypes.AnimationDefinition, tiles.Select(tile => tile.AnimationKey),
                            scene.AnimationSources, source => source.AnimationKey,
                            source => Physical(owner, AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Animation '" + source.AnimationKey + "'"),
                            (key, location) => location.AssetEntryName is null ? SceneAnimationSourceDefinition.Loose(key, location.Path) :
                                SceneAnimationSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                        break;
                    }
                case SpriteDefinition sprites:
                    {
                        Sources(owner, label, AssetTypes.TilesheetDefinition, sprites.Sprites.Where(sprite => sprite.Frame is not null).Select(sprite => sprite.Frame!.Tilesheet),
                            sprites.TilesheetSources, source => source.Tilesheet,
                            source => Physical(owner, AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Tilesheet '" + source.Tilesheet + "'"),
                            (key, location) => location.AssetEntryName is null ? SpriteTilesheetSourceDefinition.Loose(key, location.Path) :
                                SpriteTilesheetSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                        Sources(owner, label, AssetTypes.AnimationDefinition, sprites.Sprites.Select(sprite => sprite.AnimationKey),
                            sprites.AnimationSources, source => source.AnimationKey,
                            source => Physical(owner, AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Animation '" + source.AnimationKey + "'"),
                            (key, location) => location.AssetEntryName is null ? SpriteAnimationSourceDefinition.Loose(key, location.Path) :
                                SpriteAnimationSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                        var scenes = Sources(owner, label, AssetTypes.SceneDefinition, sprites.Sprites.Select(sprite => sprite.SceneId),
                            sprites.SceneSources, source => source.SceneId,
                            source => Physical(owner, AssetTypes.SceneDefinition, source.GscnPath, source.AssetsFilePath, source.AssetEntryName, (int)source.Kind, label + " :: Scene '" + source.SceneId + "'"),
                            (key, location) => location.AssetEntryName is null ? SpriteSceneSourceDefinition.Loose(key, location.Path) :
                                SpriteSceneSourceDefinition.Packed(key, PackedPath(location), location.AssetEntryName));
                        foreach (var sprite in sprites.Sprites)
                        {
                            if (string.IsNullOrWhiteSpace(sprite.SceneId)) continue;
                            var sceneDefinition = (SceneDefinition)scenes[sprite.SceneId].Document!.Members().Single(item => item.Identity == sprite.SceneId).Member;
                            if (!sceneDefinition.Layers.Any(layer => layer.ID == sprite.SceneLayerId))
                                throw new InvalidDataException($"{label}: sprite '{sprite.Nickname ?? sprite.Id.ToString()}' references missing layer '{sprite.SceneLayerId}' in scene '{sprite.SceneId}'.");
                        }
                        break;
                    }
            }
        }
    }

    private Dictionary<string, Entry> Sources<T>(Entry owner, string label, AssetTypes type, IEnumerable<string?> required,
        List<T> sources, Func<T, string> identity, Func<T, AssetLocation> physical, Func<string, AssetLocation, T> create)
    {
        var comparer = Identities(type);
        var resolved = new Dictionary<string, Entry>(comparer);
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            var source = sources[sourceIndex];
            var key = identity(source);
            if (resolved.ContainsKey(key)) throw new InvalidDataException($"{label}: duplicate/conflicting {type} source '{key}'.");
            var location = physical(source);
            var collection = type == AssetTypes.TilesheetDefinition ? "TilesheetSources" : type == AssetTypes.AnimationDefinition ? "AnimationSources" : "SceneSources";
            var target = Resolve(location, key, label + $" :: {collection}[{sourceIndex}] {type} '{key}'");
            resolved.Add(key, target);
            Map(owner, location, target.Destination);
        }
        foreach (var key in required.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key!).Distinct(comparer))
        {
            if (resolved.ContainsKey(key)) continue;
            var target = Logical(type, key, label);
            sources.Add(create(key, target.Location));
            owner.NeedsRewrite = true;
            resolved.Add(key, target);
            Map(owner, target.Location, target.Destination);
        }
        return resolved;
    }
}
