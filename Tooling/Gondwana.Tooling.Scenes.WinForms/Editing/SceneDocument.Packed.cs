using System.Text;
using Gondwana.Assets;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.Editing;

public sealed partial class SceneDocument
{
    /// <summary>Physical packed origin, or null for a loose/unsaved document.</summary>
    public AssetLocation? PackedLocation { get; private set; }

    /// <summary>Opens all members of an exact typed entry without extracting a loose file.</summary>
    public static SceneDocument Open(AssetsFile archive, string entryName)
    {
        if (!archive.GetAllEntries().Any(entry => entry.AssetType == AssetTypes.SceneDefinition &&
            string.Equals(entry.AssetName, entryName, StringComparison.OrdinalIgnoreCase)))
            throw new FileNotFoundException($"SceneDefinition entry '{entryName}' not found in '{archive.SourcePath}'.");
        var path = Path.GetFullPath(archive.SourcePath ?? throw new ArgumentException("Archive requires a physical source path."));
        var directory = Path.GetDirectoryName(path)!;
        var definitions = SceneDefinitionSerializer.LoadAll(archive, entryName).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        return new SceneDocument(definitions[0], null, directory, false)
        {
            _definitions = definitions,
            PackedLocation = new(AssetTypes.SceneDefinition, path, entryName),
            IsDirty = false
        };
    }

    /// <summary>Serializes all members without rebasing or changing document state. The host validates before persistence.</summary>
    public byte[] SerializePacked() => Encoding.UTF8.GetBytes(
        SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = _definitions }));

    /// <summary>Called only after the host successfully persists the original packed entry.</summary>
    public void AcceptPackedSave()
    {
        if (PackedLocation is null) throw new InvalidOperationException("This document is not packed.");
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
