using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
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
public sealed class AuthoringResourceResolver(AssetPackageCatalog packages)
{
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

    private static string RequireEntry(string? entry) =>
        !string.IsNullOrWhiteSpace(entry) ? entry : throw new InvalidDataException("Packed source has no exact entry name.");

    private Stream Open(AuthoringResource resource, AssetTypes expectedType)
    {
        if (resource.AssetType != expectedType)
            throw new InvalidDataException($"Expected {expectedType}, received {resource.AssetType} for '{resource.Path}'.");
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
            using var stream = packages.OpenAsset(archive, AssetTypes.Image, image.AssetEntryName);
            return SKBitmap.Decode(stream) ?? throw new InvalidDataException($"Packed image '{image.AssetEntryName}' in '{archive}' is unsupported or corrupt.");
        }
        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath))
            throw new InvalidDataException("Packed image has no exact entry name.");
        var path = ResolvePath(image.FilePath!, baseDirectory);
        return SKBitmap.Decode(path) ?? throw new InvalidDataException($"Image '{path}' is missing, unsupported or corrupt.");
    }
}
