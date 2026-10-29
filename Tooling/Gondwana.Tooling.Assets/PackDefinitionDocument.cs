using System.Text;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Assets;

/// <summary>Typed access to native documents; serialization and relocation remain owned by Core.</summary>
internal sealed class PackDefinitionDocument
{
    private object _document;
    private PackDefinitionDocument(object document) => _document = document;

    /// <summary>Recognizes the native document types supported by the shared format handlers.</summary>
    /// <param name="type">Typed physical resource classification.</param>
    /// <returns>Whether the resource contains a native definition document.</returns>
    internal static bool IsNative(AssetTypes type) => type is AssetTypes.TilesheetDefinition or AssetTypes.AnimationDefinition or AssetTypes.SceneDefinition or AssetTypes.SpriteDefinition or AssetTypes.AudioDefinition;

    /// <summary>Reads a native document or returns null for a binary asset.</summary>
    /// <param name="type">The planned asset type.</param>
    /// <param name="bytes">The original document bytes.</param>
    /// <param name="identity">The physical file or entry name for legacy singleton identity inference.</param>
    /// <returns>A complete typed document, or null for binary content.</returns>
    internal static PackDefinitionDocument? Read(AssetTypes type, byte[] bytes, string identity)
    {
        if (!IsNative(type)) return null;
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var json = reader.ReadToEnd();
        object document = type switch
        {
            AssetTypes.TilesheetDefinition => TilesheetDefinitionSerializer.FromDocumentJson(json, identity),
            AssetTypes.AnimationDefinition => AnimationDefinitionSerializer.FromDocumentJson(json, identity),
            AssetTypes.SceneDefinition => SceneDefinitionSerializer.FromDocumentJson(json, identity),
            AssetTypes.SpriteDefinition => SpriteDefinitionSerializer.FromDocumentJson(json, identity),
            AssetTypes.AudioDefinition => AudioDefinitionSerializer.FromDocumentJson(json, identity),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return new(document);
    }

    /// <summary>Enumerates every logical member without conflating its identity with the entry name.</summary>
    /// <returns>Logical identity and typed member pairs.</returns>
    internal IEnumerable<(string Identity, object Member)> Members() => _document switch
    {
        TilesheetDefinitionDocument document => document.Definitions.Select(member => (member.Name, (object)member)),
        AnimationDefinitionDocument document => document.Definitions.Select(member => (member.Key, (object)member)),
        SceneDefinitionDocument document => document.Definitions.Select(member => (member.ID, (object)member)),
        SpriteDefinitionDocument document => document.Definitions.Select(member => (member.Key, (object)member)),
        AudioDefinitionDocument document => document.Definitions.Select(member => (member.Key, (object)member)),
        _ => throw new InvalidOperationException("Unknown native document.")
    };

    /// <summary>Enumerates only this document's explicit physical dependencies, without opening or resolving them.</summary>
    internal IEnumerable<AssetLocation> References(AssetLocation owner)
    {
        AssetLocation Physical(AssetTypes type, string? file, string? archive, string? entry)
        {
            if (string.IsNullOrWhiteSpace(file) && string.IsNullOrWhiteSpace(archive) && !string.IsNullOrWhiteSpace(entry)) archive = owner.Path;
            bool loose = !string.IsNullOrWhiteSpace(file);
            bool packed = !string.IsNullOrWhiteSpace(archive) && !string.IsNullOrWhiteSpace(entry);
            if (loose == packed || loose && (!string.IsNullOrWhiteSpace(archive) || !string.IsNullOrWhiteSpace(entry)))
                throw new InvalidDataException("Missing or conflicting physical dependency source.");
            return new(type, Path.GetFullPath(loose ? file! : archive!, Path.GetDirectoryName(owner.Path)!), packed ? entry : null);
        }
        foreach (var (_, member) in Members())
        {
            switch (member)
            {
                case TilesheetDefinition sheet:
                    yield return Physical(AssetTypes.Image, sheet.Image.FilePath, sheet.Image.AssetsFilePath, sheet.Image.AssetEntryName);
                    break;
                case AnimationDefinition animation:
                    foreach (var source in animation.TilesheetSources)
                        yield return Physical(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName);
                    break;
                case SceneDefinition scene:
                    foreach (var source in scene.TilesheetSources)
                        yield return Physical(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName);
                    foreach (var source in scene.AnimationSources)
                        yield return Physical(AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName);
                    break;
                case SpriteDefinition sprites:
                    foreach (var source in sprites.TilesheetSources)
                        yield return Physical(AssetTypes.TilesheetDefinition, source.GtsPath, source.AssetsFilePath, source.AssetEntryName);
                    foreach (var source in sprites.AnimationSources)
                        yield return Physical(AssetTypes.AnimationDefinition, source.GaniPath, source.AssetsFilePath, source.AssetEntryName);
                    foreach (var source in sprites.SceneSources)
                        yield return Physical(AssetTypes.SceneDefinition, source.GscnPath, source.AssetsFilePath, source.AssetEntryName);
                    break;
                case AudioDefinition audio:
                    foreach (var resource in audio.Resources.Where(r => r.SourceKind != AudioResourceSourceKind.Uri))
                        yield return Physical(AssetTypes.Audio, resource.FilePath, resource.AssetsFilePath, resource.AssetEntryName);
                    break;
            }
        }
    }

    /// <summary>Runs Core document and member validation before any archive write.</summary>
    /// <param name="label">Physical document diagnostic context.</param>
    internal void Validate(string label)
    {
        var errors = _document switch
        {
            TilesheetDefinitionDocument document => TilesheetDefinitionSerializer.ValidateDocument(document),
            AnimationDefinitionDocument document => AnimationDefinitionSerializer.ValidateDocument(document),
            SceneDefinitionDocument document => SceneDefinitionSerializer.ValidateDocument(document),
            SpriteDefinitionDocument document => SpriteDefinitionSerializer.ValidateDocument(document),
            AudioDefinitionDocument document => AudioDefinitionSerializer.ValidateDocument(document),
            _ => throw new InvalidOperationException("Unknown native document.")
        };
        if (errors.Count != 0) throw new InvalidDataException($"{label}: {string.Join("; ", errors)}");
    }

    /// <summary>Uses Phase 1 to transform complete documents and emits canonical bytes once.</summary>
    /// <param name="source">The actual input location.</param>
    /// <param name="destination">The final archive location, never a staging filename.</param>
    /// <param name="mappings">Resolved dependency mappings.</param>
    /// <param name="requireAllMappings">Whether mappings unused by this document are rejected.</param>
    /// <returns>The canonical document bytes.</returns>
    internal byte[] Relocate(AssetLocation source, AssetLocation destination, IEnumerable<ReferenceRelocation> mappings, bool requireAllMappings = true)
    {
        _document = _document switch
        {
            TilesheetDefinitionDocument document => DefinitionReferenceRelocator.Relocate(document, source, destination, mappings, requireAllMappings),
            AnimationDefinitionDocument document => DefinitionReferenceRelocator.Relocate(document, source, destination, mappings, requireAllMappings),
            SceneDefinitionDocument document => DefinitionReferenceRelocator.Relocate(document, source, destination, mappings, requireAllMappings),
            SpriteDefinitionDocument document => DefinitionReferenceRelocator.Relocate(document, source, destination, mappings, requireAllMappings),
            AudioDefinitionDocument document => DefinitionReferenceRelocator.Relocate(document, source, destination, mappings, requireAllMappings),
            _ => throw new InvalidOperationException("Unknown native document.")
        };
        Validate(destination.ToString());
        var json = _document switch
        {
            TilesheetDefinitionDocument document => TilesheetDefinitionSerializer.ToDocumentJson(document),
            AnimationDefinitionDocument document => AnimationDefinitionSerializer.ToDocumentJson(document),
            SceneDefinitionDocument document => SceneDefinitionSerializer.ToDocumentJson(document),
            SpriteDefinitionDocument document => SpriteDefinitionSerializer.ToDocumentJson(document),
            AudioDefinitionDocument document => AudioDefinitionSerializer.ToDocumentJson(document),
            _ => throw new InvalidOperationException("Unknown native document.")
        };
        return Encoding.UTF8.GetBytes(json);
    }
}
