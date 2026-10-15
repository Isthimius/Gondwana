using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana;

/// <summary>Transforms complete native documents without file, archive, or registry mutations.</summary>
/// <remarks>
/// Returned documents are independent authoring copies without load stamps: only loading committed
/// content establishes runtime PreserveSource provenance. Logical selectors and NextCycleKey are unchanged.
/// </remarks>
public static class DefinitionReferenceRelocator
{
    /// <summary>Relocates references in every member of a tilesheet document.</summary>
    /// <param name="document">The complete input document, never modified.</param>
    /// <param name="sourceDocument">Its actual current physical location, used to resolve relative dependencies.</param>
    /// <param name="destinationDocument">Its planned physical location, used to store portable dependencies.</param>
    /// <param name="relocations">The batch of same-type physical mappings.</param>
    /// <param name="requireAllMappings">Whether unused mappings fail. Disable when applying a project batch to individual documents.</param>
    /// <returns>An independent document. Throws InvalidDataException for malformed references, ambiguous or missing mappings.</returns>
    public static TilesheetDefinitionDocument Relocate(TilesheetDefinitionDocument document,
        AssetLocation sourceDocument, AssetLocation destinationDocument,
        IEnumerable<ReferenceRelocation> relocations, bool requireAllMappings = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new RelocationContext(AssetTypes.TilesheetDefinition, sourceDocument, destinationDocument, relocations);
        var result = TilesheetDefinitionSerializer.FromDocumentJson(TilesheetDefinitionSerializer.ToDocumentJson(document));
        foreach (var member in result.Definitions)
        {
            var image = member.Image ?? throw new InvalidDataException($"Tilesheet '{member.Name}': Image must not be null.");
            context.Rewrite(AssetTypes.Image, image.FilePath, image.AssetsFilePath, image.AssetEntryName, null,
                $"Tilesheet '{member.Name}' :: Image", (file, archive, entry, packed) =>
                {
                    image.FilePath = file;
                    image.AssetsFilePath = archive;
                    image.AssetEntryName = entry;
                });
            member.Source = destinationDocument.AssetEntryName is null
                ? TilesheetDefinitionSource.LooseDefinitionFile(destinationDocument.Path)
                : TilesheetDefinitionSource.PackedDefinitionFile(destinationDocument.Path, destinationDocument.AssetEntryName);
        }
        context.Complete(requireAllMappings);
        return result;
    }

    /// <summary>Relocates references in every member of an animation document.</summary>
    /// <param name="document">The complete input document, never modified.</param>
    /// <param name="sourceDocument">Its actual current physical location, used to resolve relative dependencies.</param>
    /// <param name="destinationDocument">Its planned physical location, used to store portable dependencies.</param>
    /// <param name="relocations">The batch of same-type physical mappings.</param>
    /// <param name="requireAllMappings">Whether unused mappings fail. Disable when applying a project batch to individual documents.</param>
    /// <returns>An independent document. Throws InvalidDataException for malformed references, ambiguous or missing mappings.</returns>
    public static AnimationDefinitionDocument Relocate(AnimationDefinitionDocument document,
        AssetLocation sourceDocument, AssetLocation destinationDocument,
        IEnumerable<ReferenceRelocation> relocations, bool requireAllMappings = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new RelocationContext(AssetTypes.AnimationDefinition, sourceDocument, destinationDocument, relocations);
        var result = AnimationDefinitionSerializer.FromDocumentJson(AnimationDefinitionSerializer.ToDocumentJson(document));
        foreach (var member in result.Definitions)
        {
            for (int i = 0; i < member.TilesheetSources.Count; i++)
            {
                var source = member.TilesheetSources[i] ?? throw new InvalidDataException($"Member contains null TilesheetSources[{i}].");
                context.Rewrite(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Animation '{member.Key}' :: TilesheetSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GtsPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? AnimationTilesheetSourceKind.PackedDefinitionFile : AnimationTilesheetSourceKind.LooseDefinitionFile;
                    });
            }
            member.Source = destinationDocument.AssetEntryName is null
                ? AnimationDefinitionSource.LooseDefinitionFile(destinationDocument.Path)
                : AnimationDefinitionSource.PackedDefinitionFile(destinationDocument.Path, destinationDocument.AssetEntryName);
        }
        context.Complete(requireAllMappings);
        return result;
    }

    /// <summary>Relocates references in every member of a scene document.</summary>
    /// <param name="document">The complete input document, never modified.</param>
    /// <param name="sourceDocument">Its actual current physical location, used to resolve relative dependencies.</param>
    /// <param name="destinationDocument">Its planned physical location, used to store portable dependencies.</param>
    /// <param name="relocations">The batch of same-type physical mappings.</param>
    /// <param name="requireAllMappings">Whether unused mappings fail. Disable when applying a project batch to individual documents.</param>
    /// <returns>An independent document. Throws InvalidDataException for malformed references, ambiguous or missing mappings.</returns>
    public static SceneDefinitionDocument Relocate(SceneDefinitionDocument document,
        AssetLocation sourceDocument, AssetLocation destinationDocument,
        IEnumerable<ReferenceRelocation> relocations, bool requireAllMappings = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new RelocationContext(AssetTypes.SceneDefinition, sourceDocument, destinationDocument, relocations);
        var result = SceneDefinitionSerializer.FromDocumentJson(SceneDefinitionSerializer.ToDocumentJson(document));
        foreach (var member in result.Definitions)
        {
            for (int i = 0; i < member.TilesheetSources.Count; i++)
            {
                var source = member.TilesheetSources[i] ?? throw new InvalidDataException($"Member contains null TilesheetSources[{i}].");
                context.Rewrite(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Scene '{member.ID}' :: TilesheetSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GtsPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? SceneTilesheetSourceKind.PackedDefinitionFile : SceneTilesheetSourceKind.LooseDefinitionFile;
                    });
            }
            for (int i = 0; i < member.AnimationSources.Count; i++)
            {
                var source = member.AnimationSources[i] ?? throw new InvalidDataException($"Member contains null AnimationSources[{i}].");
                context.Rewrite(AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Scene '{member.ID}' :: AnimationSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GaniPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? SceneAnimationSourceKind.PackedDefinitionFile : SceneAnimationSourceKind.LooseDefinitionFile;
                    });
            }
            member.Source = destinationDocument.AssetEntryName is null
                ? SceneDefinitionSource.LooseDefinitionFile(destinationDocument.Path)
                : SceneDefinitionSource.PackedDefinitionFile(destinationDocument.Path, destinationDocument.AssetEntryName);
        }
        context.Complete(requireAllMappings);
        return result;
    }

    /// <summary>Relocates references in every member of a sprite document.</summary>
    /// <param name="document">The complete input document, never modified.</param>
    /// <param name="sourceDocument">Its actual current physical location, used to resolve relative dependencies.</param>
    /// <param name="destinationDocument">Its planned physical location, used to store portable dependencies.</param>
    /// <param name="relocations">The batch of same-type physical mappings.</param>
    /// <param name="requireAllMappings">Whether unused mappings fail. Disable when applying a project batch to individual documents.</param>
    /// <returns>An independent document. Throws InvalidDataException for malformed references, ambiguous or missing mappings.</returns>
    public static SpriteDefinitionDocument Relocate(SpriteDefinitionDocument document,
        AssetLocation sourceDocument, AssetLocation destinationDocument,
        IEnumerable<ReferenceRelocation> relocations, bool requireAllMappings = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new RelocationContext(AssetTypes.SpriteDefinition, sourceDocument, destinationDocument, relocations);
        var result = SpriteDefinitionSerializer.FromDocumentJson(SpriteDefinitionSerializer.ToDocumentJson(document));
        foreach (var member in result.Definitions)
        {
            for (int i = 0; i < member.TilesheetSources.Count; i++)
            {
                var source = member.TilesheetSources[i] ?? throw new InvalidDataException($"Member contains null TilesheetSources[{i}].");
                context.Rewrite(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Sprite '{member.Key}' :: TilesheetSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GtsPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? SpriteTilesheetSourceKind.PackedDefinitionFile : SpriteTilesheetSourceKind.LooseDefinitionFile;
                    });
            }
            for (int i = 0; i < member.SceneSources.Count; i++)
            {
                var source = member.SceneSources[i] ?? throw new InvalidDataException($"Member contains null SceneSources[{i}].");
                context.Rewrite(AssetTypes.SceneDefinition, source.GscnPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Sprite '{member.Key}' :: SceneSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GscnPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? SpriteSceneSourceKind.PackedDefinitionFile : SpriteSceneSourceKind.LooseDefinitionFile;
                    });
            }
            for (int i = 0; i < member.AnimationSources.Count; i++)
            {
                var source = member.AnimationSources[i] ?? throw new InvalidDataException($"Member contains null AnimationSources[{i}].");
                context.Rewrite(AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName,
                    (int)source.Kind, $"Sprite '{member.Key}' :: AnimationSources[{i}]", (file, archive, entry, packed) =>
                    {
                        source.GaniPath = file;
                        source.AssetsFilePath = archive;
                        source.AssetEntryName = entry;
                        source.Kind = packed ? SpriteAnimationSourceKind.PackedDefinitionFile : SpriteAnimationSourceKind.LooseDefinitionFile;
                    });
            }
            member.Source = destinationDocument.AssetEntryName is null
                ? SpriteDefinitionSource.LooseDefinitionFile(destinationDocument.Path)
                : SpriteDefinitionSource.PackedDefinitionFile(destinationDocument.Path, destinationDocument.AssetEntryName);
        }
        context.Complete(requireAllMappings);
        return result;
    }

    /// <summary>Relocates references in every member of an audio document.</summary>
    /// <param name="document">The complete input document, never modified.</param>
    /// <param name="sourceDocument">Its actual current physical location, used to resolve relative dependencies.</param>
    /// <param name="destinationDocument">Its planned physical location, used to store portable dependencies.</param>
    /// <param name="relocations">The batch of same-type physical mappings.</param>
    /// <param name="requireAllMappings">Whether unused mappings fail. Disable when applying a project batch to individual documents.</param>
    /// <returns>An independent document. Throws InvalidDataException for malformed references, ambiguous or missing mappings.</returns>
    public static AudioDefinitionDocument Relocate(AudioDefinitionDocument document,
        AssetLocation sourceDocument, AssetLocation destinationDocument,
        IEnumerable<ReferenceRelocation> relocations, bool requireAllMappings = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new RelocationContext(AssetTypes.AudioDefinition, sourceDocument, destinationDocument, relocations);
        var result = AudioDefinitionSerializer.FromDocumentJson(AudioDefinitionSerializer.ToDocumentJson(document));
        foreach (var member in result.Definitions)
        {
            foreach (var resource in member.Resources)
            {
                if (resource is null) throw new InvalidDataException($"Audio '{member.Key}': null resource.");
                if (resource.SourceKind == AudioResourceSourceKind.Uri) continue;
                context.Rewrite(AssetTypes.Audio, resource.FilePath, resource.AssetsFilePath, resource.AssetEntryName,
                    (int)resource.SourceKind, $"Audio '{member.Key}' :: Resource '{resource.Key}'", (file, archive, entry, packed) =>
                    {
                        if (packed && string.IsNullOrWhiteSpace(resource.SourceExtension))
                        {
                            resource.SourceExtension = Path.GetExtension(resource.FilePath ?? resource.AssetEntryName);
                            if (string.IsNullOrWhiteSpace(resource.SourceExtension) && string.IsNullOrWhiteSpace(Path.GetExtension(entry)))
                                throw new InvalidDataException($"Audio '{member.Key}' :: Resource '{resource.Key}': cannot infer packed audio format; specify SourceExtension.");
                        }
                        resource.FilePath = file;
                        resource.AssetsFilePath = archive;
                        resource.AssetEntryName = entry;
                        resource.SourceKind = packed ? AudioResourceSourceKind.PackedAsset : AudioResourceSourceKind.LooseFile;
                    });
            }
            member.Source = destinationDocument.AssetEntryName is null
                ? AudioDefinitionSource.LooseDefinitionFile(destinationDocument.Path)
                : AudioDefinitionSource.PackedDefinitionFile(destinationDocument.Path, destinationDocument.AssetEntryName);
        }
        context.Complete(requireAllMappings);
        return result;
    }

    private sealed class RelocationContext
    {
        private readonly AssetLocation _source;
        private readonly AssetLocation _destination;
        private readonly ReferenceRelocation[] _mappings;
        private readonly bool[] _used;

        /// <summary>Validates a batch before examining or modifying any cloned references.</summary>
        /// <param name="type">The containing document type.</param>
        /// <param name="source">Its original location.</param>
        /// <param name="destination">Its planned location.</param>
        /// <param name="mappings">The requested physical mappings.</param>
        internal RelocationContext(AssetTypes type, AssetLocation source, AssetLocation destination, IEnumerable<ReferenceRelocation> mappings)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(mappings);
            if (source.AssetType != type || destination.AssetType != type)
                throw new ArgumentException($"Containing locations must both have type {type}.");
            _source = source;
            _destination = destination;
            _mappings = mappings.ToArray();
            _used = new bool[_mappings.Length];
            if (_mappings.Any(mapping => mapping is null)) throw new ArgumentException("Relocations must not contain null.", nameof(mappings));
            for (int i = 0; i < _mappings.Length; i++)
                for (int j = 0; j < _mappings.Length; j++)
                {
                    if (i == j) continue;
                    if (_mappings[i].Source.Matches(_mappings[j].Source))
                        throw new InvalidDataException($"Ambiguous duplicate relocation source: {_mappings[i].Source}.");
                    if (_mappings[i].Destination.Matches(_mappings[j].Source) &&
                        !_mappings[j].Source.Matches(_mappings[j].Destination))
                        throw new InvalidDataException($"Relocation chains/cycles are ambiguous on repeated application: {_mappings[j].Source}.");
                }
        }

        /// <summary>Rewrites one existing format-specific reference, preserving its logical fields.</summary>
        /// <param name="type">The dependency's type.</param>
        /// <param name="file">The stored loose path.</param>
        /// <param name="archive">The stored GAF path.</param>
        /// <param name="entry">The typed entry name.</param>
        /// <param name="kind">Zero for loose, one for packed, or null for the GTS inferred form.</param>
        /// <param name="label">Member-qualified diagnostic context.</param>
        /// <param name="apply">Updates only the existing model's physical reference fields.</param>
        internal void Rewrite(AssetTypes type, string? file, string? archive, string? entry, int? kind,
            string label, Action<string?, string?, string?, bool> apply)
        {
            // GTS may implicitly reference an image in its containing GAF.
            if (kind is null && string.IsNullOrWhiteSpace(file) && string.IsNullOrWhiteSpace(archive) &&
                !string.IsNullOrWhiteSpace(entry) && _source.AssetEntryName is not null)
                archive = _source.Path;
            bool loose = !string.IsNullOrWhiteSpace(file);
            bool packed = !string.IsNullOrWhiteSpace(archive) && !string.IsNullOrWhiteSpace(entry);
            if (loose == packed || (loose && (!string.IsNullOrWhiteSpace(archive) || !string.IsNullOrWhiteSpace(entry))) ||
                (kind is not null && kind != (packed ? 1 : 0)))
                throw new InvalidDataException($"{label}: reference must identify exactly one loose file or typed GAF entry consistent with its source kind.");
            var directory = Path.GetDirectoryName(_source.Path)!;
            AssetLocation location;
            try { location = new AssetLocation(type, Path.GetFullPath(loose ? file! : archive!, directory), packed ? entry : null); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidDataException($"{label}: invalid physical reference path.", ex);
            }
            var target = location;
            for (int i = 0; i < _mappings.Length; i++)
            {
                if (location.Matches(_mappings[i].Destination)) _used[i] = true;
                if (!location.Matches(_mappings[i].Source)) continue;
                _used[i] = true;
                target = _mappings[i].Destination;
            }
            if (target.Matches(location) && _source.Matches(_destination)) return;
            var relative = Path.GetRelativePath(Path.GetDirectoryName(_destination.Path)!, target.Path);
            apply(target.AssetEntryName is null ? relative : null,
                target.AssetEntryName is null ? null : relative, target.AssetEntryName, target.AssetEntryName is not null);
        }

        /// <summary>Rejects unused mappings unless the caller explicitly permits a wider batch.</summary>
        /// <param name="requireAll">Whether every mapping must match an original or already-relocated reference.</param>
        internal void Complete(bool requireAll)
        {
            if (!requireAll) return;
            var missing = _mappings.Where((mapping, index) => !_used[index]).Select(mapping => mapping.Source.ToString()).ToArray();
            if (missing.Length != 0)
                throw new InvalidDataException("Relocation sources not found in document references: " + string.Join("; ", missing));
        }
    }
}
