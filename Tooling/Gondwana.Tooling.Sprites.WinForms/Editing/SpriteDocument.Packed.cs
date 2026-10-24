using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;

namespace Gondwana.Tooling.Sprites.Editing;

public sealed partial class SpriteDocument
{
    /// <summary>Physical packed origin, or null for a loose/unsaved document.</summary>
    public AssetLocation? PackedLocation { get; private set; }

    /// <summary>Opens all members of an exact typed GSPR entry without extracting a loose file.</summary>
    /// <param name="archive">Borrowed archive with a physical source path; it is not retained or disposed by the document.</param>
    /// <param name="entryName">Entry name matched without case in the SpriteDefinition asset type.</param>
    /// <returns>A clean editing session with the first member selected and the packed origin recorded.</returns>
    /// <exception cref="FileNotFoundException">The typed entry does not exist.</exception>
    /// <exception cref="ArgumentException">The archive has no physical source path.</exception>
    /// <exception cref="InvalidDataException">The entry contains no definitions.</exception>
    public static SpriteDocument Open(AssetsFile archive, string entryName)
    {
        if (!archive.GetAllEntries().Any(entry => entry.AssetType == AssetTypes.SpriteDefinition &&
            string.Equals(entry.AssetName, entryName, StringComparison.OrdinalIgnoreCase)))
            throw new FileNotFoundException($"SpriteDefinition entry '{entryName}' not found in '{archive.SourcePath}'.");
        var path = Path.GetFullPath(archive.SourcePath ?? throw new ArgumentException("Archive requires a physical source path."));
        var directory = Path.GetDirectoryName(path)!;
        var definitions = SpriteDefinitionSerializer.LoadAll(archive, entryName).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        return new SpriteDocument(definitions[0], directory, null, false)
        {
            _definitions = definitions,
            PackedLocation = new(AssetTypes.SpriteDefinition, path, entryName),
            IsDirty = false
        };
    }

    /// <summary>Serializes all members without rebasing or changing document state. The host validates before persistence.</summary>
    /// <returns>A new UTF-8 JSON byte array for the complete definition document.</returns>
    public byte[] SerializePacked() => Encoding.UTF8.GetBytes(
        SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = _definitions }));

    /// <summary>Clears the dirty flag and raises <see cref="Changed"/> after the host persists the original packed entry.</summary>
    /// <remarks>Does not write the archive; call only after successful persistence.</remarks>
    /// <exception cref="InvalidOperationException">The document has no packed location.</exception>
    public void AcceptPackedSave()
    {
        if (PackedLocation is null) throw new InvalidOperationException("This document is not packed.");
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
