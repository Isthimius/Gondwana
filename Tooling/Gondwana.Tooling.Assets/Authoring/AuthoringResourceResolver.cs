using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Tilesheets.Sources;
using SkiaSharp;

namespace Gondwana.Tooling.Assets;

/// <summary>Physical authoring resource identity; a packed entry always includes its expected type.</summary>
public sealed record AuthoringResource(string Path, AssetTypes AssetType, string? EntryName = null)
{
    public string BaseDirectory => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
    public string? ArchivePath => EntryName is null ? null : Path;
}

/// <summary>Loads authoring data without engine initialization, runtime registration, or reference rewriting.</summary>
public sealed class AuthoringResourceResolver(AssetPackageCatalog packages)
{
    public static string ResolvePath(string path, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return System.IO.Path.GetFullPath(path, baseDirectory);
    }

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

    private static string RequireEntry(string? entry) =>
        !string.IsNullOrWhiteSpace(entry) ? entry : throw new InvalidDataException("Packed source has no exact entry name.");

    private Stream Open(AuthoringResource resource, AssetTypes expectedType)
    {
        if (resource.AssetType != expectedType)
            throw new InvalidDataException($"Expected {expectedType}, received {resource.AssetType} for '{resource.Path}'.");
        return resource.EntryName is null ? File.OpenRead(resource.Path)
            : packages.OpenAsset(resource.Path, expectedType, RequireEntry(resource.EntryName));
    }

    public TilesheetDefinition LoadTilesheet(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.TilesheetDefinition);
        return TilesheetDefinitionSerializer.Load(stream, identity);
    }

    public AnimationDefinition LoadAnimation(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.AnimationDefinition);
        return AnimationDefinitionSerializer.Load(stream, identity);
    }

    public SceneDefinition LoadScene(AuthoringResource resource, string? identity)
    {
        using var stream = Open(resource, AssetTypes.SceneDefinition);
        return SceneDefinitionSerializer.Load(stream, identity);
    }

    /// <summary>Decodes an image once for a source load; caller owns the returned bitmap.</summary>
    public SKBitmap DecodeImage(TilesheetDefinition definition, string baseDirectory, string? containingArchive = null)
    {
        var image = definition.Image ?? throw new InvalidDataException("GTS does not define an image source.");
        if (!string.IsNullOrWhiteSpace(image.AssetEntryName))
        {
            var archive = string.IsNullOrWhiteSpace(image.AssetsFilePath) ? containingArchive
                : ResolvePath(image.AssetsFilePath, baseDirectory);
            if (archive is null) throw new InvalidDataException("Packed image has no archive source.");
            using var stream = packages.OpenAsset(archive, AssetTypes.Image, image.AssetEntryName);
            return SKBitmap.Decode(stream) ?? throw new InvalidDataException($"Packed image '{image.AssetEntryName}' in '{archive}' is unsupported or corrupt.");
        }
        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath))
            throw new InvalidDataException("Packed image has no exact entry name.");
        var path = ResolvePath(image.FilePath!, baseDirectory);
        return SKBitmap.Decode(path) ?? throw new InvalidDataException($"Image '{path}' is missing, unsupported or corrupt.");
    }
}
