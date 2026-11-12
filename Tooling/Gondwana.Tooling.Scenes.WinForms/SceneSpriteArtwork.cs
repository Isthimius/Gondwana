using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed record SceneSpriteArtwork(Bitmap Image, Rectangle Bounds);

internal sealed class SceneSpriteArtworkCache : IDisposable
{
    private readonly Dictionary<ResourceAddress, SceneTilesheetSource> _sheets = [];
    private readonly Dictionary<ResourceAddress, AnimationDefinition> _animations = [];

    internal SceneSpriteArtwork? Find(AssetLocation location, SpriteDefinition member, SpriteInstanceDefinition sprite,
        AssetPackageCatalog packages, Func<string, SceneTilesheetSource?> sceneSheet, Func<string, SceneAnimationSource?> sceneAnimation,
        Func<AssetTypes, string, ResourceAddress?> neighbor)
    {
        var root = Path.GetDirectoryName(location.Path)!;
        if (sprite.AnimationKey is { Length: > 0 } key && (sprite.StartAnimation || sprite.Frame is null))
        {
            var source = member.AnimationSources.Where(item => string.Equals(item.AnimationKey, key, StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
            if (sceneAnimation(key) is { } authored && SceneAnimationSource.FirstPreviewFrame(authored.Definition) is { } sceneFrame)
                return Artwork(authored.FindTilesheet(sceneFrame.Tilesheet) ?? sceneSheet(sceneFrame.Tilesheet), sceneFrame)
                    ?? ResolveSpriteFrame(sceneFrame);

            var animation = source is not null
                ? Address(AssetTypes.AnimationDefinition, key, root, source.Kind == SpriteAnimationSourceKind.PackedDefinitionFile,
                    source.GaniPath, source.AssetsFilePath, source.AssetEntryName)
                : neighbor(AssetTypes.AnimationDefinition, key);
            if (animation is not null)
            {
                if (!_animations.TryGetValue(animation, out var definition))
                {
                    definition = new AuthoringResourceResolver(packages).LoadAnimation(animation.Resource, key);
                    _animations.Add(animation, definition);
                }
                if (SceneAnimationSource.FirstPreviewFrame(definition) is { } frame)
                {
                    var sheet = definition.TilesheetSources.Where(item => item.Tilesheet == frame.Tilesheet).SingleOrDefault();
                    if (sheet is not null)
                    {
                        var resource = AuthoringResourceResolver.TilesheetSource(sheet, animation.Resource.BaseDirectory);
                        return Resolve(new ResourceAddress(resource, frame.Tilesheet), frame, packages);
                    }
                    return ResolveSpriteFrame(frame);
                }
            }
        }
        return sprite.Frame is { } still
            ? ResolveSpriteFrame(Frame(still.Tilesheet, still.RegionName, still.XTile, still.YTile)) : null;

        SceneSpriteArtwork? ResolveSpriteFrame(SceneFrameDefinition frame)
        {
            var source = member.TilesheetSources.Where(item => item.Tilesheet == frame.Tilesheet).SingleOrDefault();
            if (source is not null)
                return Resolve(Address(AssetTypes.TilesheetDefinition, frame.Tilesheet, root,
                    source.Kind == SpriteTilesheetSourceKind.PackedDefinitionFile, source.GtsPath, source.AssetsFilePath, source.AssetEntryName),
                    frame, packages);
            return Artwork(sceneSheet(frame.Tilesheet), frame) ??
                (neighbor(AssetTypes.TilesheetDefinition, frame.Tilesheet) is { } discovered ? Resolve(discovered, frame, packages) : null);
        }
    }

    private SceneSpriteArtwork? Resolve(ResourceAddress address, SceneFrameDefinition frame, AssetPackageCatalog packages)
    {
        if (!_sheets.TryGetValue(address, out var source))
        {
            source = SceneTilesheetSource.LoadResource(address.Resource, address.Member, packages);
            _sheets.Add(address, source);
        }
        return Artwork(source, frame);
    }

    private static SceneSpriteArtwork? Artwork(SceneTilesheetSource? source, SceneFrameDefinition frame) =>
        source?.Image is { } image && source.TryResolve(frame, out _, out var bounds) &&
        new Rectangle(Point.Empty, image.Size).Contains(bounds) ? new(image, bounds) : null;

    private static SceneFrameDefinition Frame(string sheet, string region, int x, int y) =>
        new() { Tilesheet = sheet, RegionName = region, XTile = x, YTile = y };

    private static ResourceAddress Address(AssetTypes type, string member, string root, bool packed,
        string? loose, string? archive, string? entry)
    {
        string? path = packed ? archive : loose;
        if (string.IsNullOrWhiteSpace(path) || packed && string.IsNullOrWhiteSpace(entry))
            throw new InvalidDataException($"'{member}' has an incomplete source.");
        return new(new AssetLocation(type, AuthoringResourceResolver.ResolvePath(path, root), packed ? entry : null), member);
    }

    public void Dispose()
    {
        foreach (var sheet in _sheets.Values) sheet.Dispose();
        _sheets.Clear();
        _animations.Clear();
    }
}
