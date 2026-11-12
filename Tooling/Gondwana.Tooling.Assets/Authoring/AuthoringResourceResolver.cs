using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Tilesheets.Sources;
using SkiaSharp;

namespace Gondwana.Tooling.Assets;

/// <summary>Physical authoring resource identity; a packed entry always includes its expected type.</summary>
/// <param name="Path">Loose file path or containing archive path; construction does not normalize it.</param>
/// <param name="AssetType">Expected asset type used when opening the resource.</param>
/// <param name="EntryName">Exact packed entry name, or null for a loose file.</param>
public sealed record AuthoringResource(string Path, AssetTypes AssetType, string? EntryName = null)
{
    /// <summary>Gets the absolute directory containing the loose file or archive.</summary>
    public string BaseDirectory => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
    /// <summary>Gets the stored archive path for a packed entry, or null for a loose file.</summary>
    public string? ArchivePath => EntryName is null ? null : Path;
}

/// <summary>Loads authoring data without engine initialization, runtime registration, or reference rewriting.</summary>
/// <param name="packages">Borrowed package cache used for exact typed packed reads; the resolver does not dispose it.</param>
/// <param name="beforeRead">Optional callback invoked with each physical file or archive path immediately before reading it.</param>
public sealed class AuthoringResourceResolver(AssetPackageCatalog packages, Action<string>? beforeRead)
{
    /// <summary>Creates a resolver using a borrowed package cache.</summary>
    public AuthoringResourceResolver(AssetPackageCatalog packages) : this(packages, null) { }

    /// <summary>Normalizes a resource path relative to an explicit base directory without checking existence.</summary>
    /// <param name="path">Nonempty resource path; absolute paths retain their root.</param>
    /// <param name="baseDirectory">Fully qualified directory used for relative paths.</param>
    /// <returns>The absolute normalized resource path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">The resource path is empty or invalid, or the base directory is not fully qualified.</exception>
    public static string ResolvePath(string path, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return System.IO.Path.GetFullPath(path, baseDirectory);
    }

    /// <summary>Converts an animation's explicit loose or packed tilesheet source into a physical resource identity.</summary>
    /// <param name="source">Serialized tilesheet source to resolve; logical names alone are not searched.</param>
    /// <param name="baseDirectory">Absolute directory containing the referring document or archive.</param>
    /// <returns>The normalized loose GTS path or exact typed archive entry.</returns>
    /// <exception cref="InvalidDataException">The source kind is unsupported or a packed source has no entry name.</exception>
    public static AuthoringResource TilesheetSource(AnimationTilesheetSourceDefinition source, string baseDirectory) =>
        source.Kind switch
        {
            AnimationTilesheetSourceKind.LooseDefinitionFile =>
                new(ResolvePath(source.GtsPath!, baseDirectory), AssetTypes.TilesheetDefinition),
            AnimationTilesheetSourceKind.PackedDefinitionFile =>
                new(ResolvePath(source.AssetsFilePath!, baseDirectory), AssetTypes.TilesheetDefinition,
                    RequireEntry(source.AssetEntryName)),
            _ => throw new InvalidDataException($"Unsupported tilesheet source kind: {source.Kind}.")
        };

    /// <summary>Converts a GSPR scene registration to its exact physical and logical address.</summary>
    public static ResourceAddress SpriteSceneSource(SpriteSceneSourceDefinition source, string baseDirectory)
    {
        var resource = source.Kind switch
        {
            SpriteSceneSourceKind.LooseDefinitionFile => new AuthoringResource(
                ResolvePath(source.GscnPath!, baseDirectory), AssetTypes.SceneDefinition),
            SpriteSceneSourceKind.PackedDefinitionFile => new AuthoringResource(
                ResolvePath(source.AssetsFilePath!, baseDirectory), AssetTypes.SceneDefinition, RequireEntry(source.AssetEntryName)),
            _ => throw new InvalidDataException($"'{source.SceneId}' has an incomplete scene source reference.")
        };
        return new(resource, source.SceneId);
    }

    private static string RequireEntry(string? entry) =>
        !string.IsNullOrWhiteSpace(entry) ? entry : throw new InvalidDataException("Packed source has no exact entry name.");

    private Stream Open(AuthoringResource resource, AssetTypes expectedType)
    {
        if (resource.AssetType != expectedType)
            throw new InvalidDataException($"Expected {expectedType}, received {resource.AssetType} for '{resource.Path}'.");
        beforeRead?.Invoke(resource.Path);
        return resource.EntryName is null ? File.OpenRead(resource.Path)
            : packages.OpenAsset(resource.Path, expectedType, RequireEntry(resource.EntryName));
    }

    /// <summary>Loads one logical tilesheet definition from an explicit loose or packed resource.</summary>
    /// <param name="resource">Physical location with the expected TilesheetDefinition asset type.</param>
    /// <param name="identity">Logical member identity, or null for a document containing exactly one member.</param>
    /// <returns>The deserialized definition; the temporary input stream is closed before returning.</returns>
    /// <exception cref="InvalidDataException">The resource type is wrong, the packed entry is unavailable, or the document or selector is invalid.</exception>
    public TilesheetDefinition LoadTilesheet(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.TilesheetDefinition);
        return TilesheetDefinitionSerializer.Load(stream, identity);
    }

    /// <summary>Resolves a dragged frame against the exact persisted GTS member and its image.</summary>
    /// <exception cref="InvalidDataException">The member, region, coordinates or image are invalid or unavailable.</exception>
    public TilesheetRegionDefinition ResolveTilesheetFrame(TilesheetFrameDrag frame) =>
        ResolveTilesheetFrame(frame, out _);

    /// <summary>Resolves a dragged frame and returns its validated GTS member for a preview.</summary>
    public TilesheetRegionDefinition ResolveTilesheetFrame(TilesheetFrameDrag frame, out TilesheetDefinition definition)
    {
        definition = LoadExplicitTilesheet(frame.Address);
        var region = ResolveTilesheetFrame(definition, frame.RegionName, frame.X, frame.Y);
        ValidateTilesheetImage(frame.Address.Resource, definition);
        return region;
    }

    /// <summary>Loads an explicitly selected GTS member and validates its layout against its saved image.</summary>
    public TilesheetDefinition LoadValidatedTilesheet(ResourceAddress address)
    {
        var definition = LoadExplicitTilesheet(address);
        ValidateTilesheetImage(address.Resource, definition);
        return definition;
    }

    private TilesheetDefinition LoadExplicitTilesheet(ResourceAddress address)
    {
        if (string.IsNullOrWhiteSpace(address.Member))
            throw new InvalidDataException("Select a specific tilesheet definition before dragging a frame.");

        var resource = address.Resource;
        if (resource.EntryName is null && !string.Equals(Path.GetExtension(resource.Path), ".gts", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A loose tilesheet source must be a .gts file.");

        return LoadTilesheet(resource, address.Member);
    }

    private void ValidateTilesheetImage(AuthoringResource resource, TilesheetDefinition definition)
    {
        using var image = DecodeImage(definition, resource.BaseDirectory, resource.ArchivePath);
        var errors = TilesheetDefinitionValidator.Validate(definition, image.Width, image.Height);
        if (errors.Count > 0)
            throw new InvalidDataException($"Tilesheet '{definition.Name}': {errors[0]}");
    }

    /// <summary>Resolves a region and coordinates against an already validated tilesheet.</summary>
    public static TilesheetRegionDefinition ResolveTilesheetFrame(TilesheetDefinition definition, string regionName, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(regionName))
            throw new InvalidDataException("Select a specific region before dragging a frame.");

        var region = definition.Regions.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, regionName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Region '{regionName}' was not found in '{definition.Name}'.");
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        if (x < 0 || y < 0 || x >= columns || y >= rows)
            throw new InvalidDataException($"Frame ({x}, {y}) is outside region '{region.Name}'.");

        return region;
    }

    /// <summary>Loads one logical animation definition from an explicit loose or packed resource.</summary>
    /// <param name="resource">Physical location with the expected AnimationDefinition asset type.</param>
    /// <param name="identity">Logical member identity, or null for a document containing exactly one member.</param>
    /// <returns>The deserialized definition; the temporary input stream is closed before returning.</returns>
    /// <exception cref="InvalidDataException">The resource type is wrong, the packed entry is unavailable, or the document or selector is invalid.</exception>
    public AnimationDefinition LoadAnimation(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.AnimationDefinition);
        return AnimationDefinitionSerializer.Load(stream, identity);
    }

    /// <summary>Resolves a saved animation and validates the tilesheet coordinates and image of each frame.</summary>
    public AnimationDefinition ResolveAnimation(AnimationDrag drag, Func<string, ResourceAddress?> declaredTilesheet,
        Action<ResourceAddress, TilesheetDefinition>? onValidatedTilesheet = null)
    {
        var resource = drag.Address.Resource;
        RequireSavedMember(drag.Address, ".gani");
        var animation = LoadAnimation(resource, drag.Address.Member);
        var errors = AnimationDefinitionValidator.Validate(animation);
        if (errors.Count != 0)
            throw new InvalidDataException($"Animation '{animation.Key}': {errors[0]}");

        foreach (var frames in animation.Frames.GroupBy(frame => frame.Tilesheet, StringComparer.Ordinal))
        {
            var source = animation.TilesheetSources.FirstOrDefault(item => item.Tilesheet == frames.Key);
            var sheet = source is null
                ? declaredTilesheet(frames.Key) ?? throw new InvalidDataException($"Animation '{animation.Key}' needs a saved GTS source for '{frames.Key}'.")
                : new ResourceAddress(TilesheetSource(source, resource.BaseDirectory), frames.Key);
            var definition = LoadValidatedTilesheet(sheet);
            foreach (var frame in frames)
                ResolveTilesheetFrame(definition, frame.RegionName, frame.XTile, frame.YTile);
            onValidatedTilesheet?.Invoke(sheet, definition);
        }

        return animation;
    }

    /// <summary>Resolves a saved scene and verifies that the exact layer belongs to it.</summary>
    public SceneDefinition ResolveSceneLayer(SceneLayerDrag drag)
    {
        RequireSavedMember(drag.Address, ".gscn");
        var scene = LoadScene(drag.Address.Resource, drag.Address.Member);
        if (string.IsNullOrWhiteSpace(drag.LayerId) || scene.Layers.Count(layer => layer.ID == drag.LayerId) != 1)
            throw new InvalidDataException($"Layer '{drag.LayerId}' is missing or ambiguous in scene '{scene.ID}'.");
        var errors = SceneDefinitionValidator.Validate(scene);
        if (errors.Count != 0)
            throw new InvalidDataException($"Scene '{scene.ID}': {errors[0]}");
        return scene;
    }

    private static void RequireSavedMember(ResourceAddress address, string extension)
    {
        if (string.IsNullOrWhiteSpace(address.Member))
            throw new InvalidDataException("Choose a specific saved definition.");
        if (address.Location.AssetEntryName is null &&
            !string.Equals(Path.GetExtension(address.Location.Path), extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"A loose source must be a {extension} file.");
    }

    /// <summary>Loads all logical sprite definitions from one exact loose or packed source.</summary>
    public IReadOnlyList<SpriteDefinition> LoadSprites(AuthoringResource resource)
    {
        using var stream = Open(resource, AssetTypes.SpriteDefinition);
        using var reader = new StreamReader(stream);
        return SpriteDefinitionSerializer.FromDocumentJson(reader.ReadToEnd(), resource.EntryName ?? resource.Path).Definitions;
    }

    /// <summary>Lists saved logical members from an exact loose or packed definition address.</summary>
    public IReadOnlyList<string> GetMembers(AuthoringResource resource)
    {
        using var stream = Open(resource, resource.AssetType);
        return resource.AssetType switch
        {
            AssetTypes.TilesheetDefinition => TilesheetDefinitionSerializer.LoadAll(stream).Select(item => item.Name).ToArray(),
            AssetTypes.AnimationDefinition => AnimationDefinitionSerializer.LoadAll(stream).Select(item => item.Key).ToArray(),
            AssetTypes.SceneDefinition => SceneDefinitionSerializer.LoadAll(stream).Select(item => item.ID).ToArray(),
            _ => throw new InvalidDataException($"Unsupported resource type: {resource.AssetType}.")
        };
    }

    /// <summary>Loads one logical scene definition from an explicit loose or packed resource.</summary>
    /// <param name="resource">Physical location with the expected SceneDefinition asset type.</param>
    /// <param name="identity">Logical member identity, or null for a document containing exactly one member.</param>
    /// <returns>The deserialized definition; the temporary input stream is closed before returning.</returns>
    /// <exception cref="InvalidDataException">The resource type is wrong, the packed entry is unavailable, or the document or selector is invalid.</exception>
    public SceneDefinition LoadScene(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.SceneDefinition);
        return SceneDefinitionSerializer.Load(stream, identity);
    }

    /// <summary>Decodes the selected image source without applying the tilesheet's alpha mask.</summary>
    /// <param name="definition">Tilesheet whose image reference is used; a packed entry takes precedence over a loose path.</param>
    /// <param name="baseDirectory">Absolute directory for resolving explicit relative image and archive paths.</param>
    /// <param name="containingArchive">Fallback archive path for an entry reference without an archive path, or null if none.</param>
    /// <returns>A decoded bitmap owned by the caller, which must dispose it.</returns>
    /// <exception cref="InvalidDataException">The image reference is incomplete, the packed entry is unavailable, or decoding fails.</exception>
    public SKBitmap DecodeImage(TilesheetDefinition definition, string baseDirectory, string? containingArchive = null)
    {
        var image = definition.Image ?? throw new InvalidDataException("GTS does not define an image source.");
        if (!string.IsNullOrWhiteSpace(image.AssetEntryName))
        {
            var archive = string.IsNullOrWhiteSpace(image.AssetsFilePath) ? containingArchive
                : ResolvePath(image.AssetsFilePath, baseDirectory);
            if (archive is null) throw new InvalidDataException("Packed image has no archive source.");
            using var stream = Open(new AuthoringResource(archive, AssetTypes.Image, image.AssetEntryName), AssetTypes.Image);
            return SKBitmap.Decode(stream) ?? throw new InvalidDataException($"Packed image '{image.AssetEntryName}' in '{archive}' is unsupported or corrupt.");
        }
        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath))
            throw new InvalidDataException("Packed image has no exact entry name.");
        var path = ResolvePath(image.FilePath!, baseDirectory);
        beforeRead?.Invoke(path);
        return SKBitmap.Decode(path) ?? throw new InvalidDataException($"Image '{path}' is missing, unsupported or corrupt.");
    }
}
