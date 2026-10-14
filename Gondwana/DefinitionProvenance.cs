using Newtonsoft.Json.Linq;

namespace Gondwana;

// Owned by the materialized object (or collection manager), never by a global name map.
// Compare the serializer's complete runtime projection, including nested mutable data.
internal sealed class DefinitionProvenance
{
    internal string? FilePath { get; }
    internal string? AssetsFilePath { get; }
    internal string? AssetEntryName { get; }
    /// <summary>Gets the logical member identity within the physical source.</summary>
    internal string? DefinitionIdentity { get; }
    private readonly JToken _baseline;

    private DefinitionProvenance(string? filePath, string? assetsFilePath, string? entryName, object definition, string? definitionIdentity)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        AssetsFilePath = string.IsNullOrWhiteSpace(assetsFilePath) ? null : Path.GetFullPath(assetsFilePath);
        AssetEntryName = entryName;
        DefinitionIdentity = definitionIdentity;
        _baseline = JToken.FromObject(definition);
    }

    /// <summary>Captures one runtime member's baseline and physical source.</summary>
    /// <param name="filePath">The loose document path, if any.</param>
    /// <param name="assetsFilePath">The archive path, if any.</param>
    /// <param name="entryName">The typed document entry name.</param>
    /// <param name="capture">Produces the member's runtime projection.</param>
    /// <param name="definitionIdentity">The logical member identity.</param>
    /// <returns>Owned provenance, or null for an anonymous source.</returns>
    internal static DefinitionProvenance? Capture(string? filePath, string? assetsFilePath,
        string? entryName, Func<object> capture, string? definitionIdentity = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) &&
            (string.IsNullOrWhiteSpace(assetsFilePath) || string.IsNullOrWhiteSpace(entryName)))
            return null;
        return new(filePath, assetsFilePath, entryName, capture(), definitionIdentity);
    }

    internal bool Matches(object definition) => JToken.DeepEquals(_baseline, JToken.FromObject(definition));
}

// Captures the actual input location separately from user-editable authoring provenance.
// Editing a loaded definition before materializing it invalidates source preservation.
internal sealed class DefinitionLoadStamp
{
    private readonly JToken _definition;
    private readonly string? _filePath;
    private readonly string? _assetsFilePath;
    private readonly string? _entryName;
    private readonly string? _definitionIdentity;

    /// <summary>Gets the actual physical source directory independently of editable authoring metadata.</summary>
    internal string? ReferenceBaseDirectory => _filePath is not null ? Path.GetDirectoryName(_filePath) :
        _assetsFilePath is not null ? Path.GetDirectoryName(_assetsFilePath) : null;

    /// <summary>Records the parsed member and its actual input location.</summary>
    /// <param name="definition">The parsed member baseline.</param>
    /// <param name="filePath">The loose document path.</param>
    /// <param name="assetsFilePath">The archive path.</param>
    /// <param name="entryName">The typed document entry name.</param>
    /// <param name="definitionIdentity">The logical member identity.</param>
    internal DefinitionLoadStamp(object definition, string? filePath = null, string? assetsFilePath = null, string? entryName = null, string? definitionIdentity = null)
    {
        _definition = JToken.FromObject(definition);
        _filePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        _assetsFilePath = string.IsNullOrWhiteSpace(assetsFilePath) ? null : Path.GetFullPath(assetsFilePath);
        _entryName = entryName;
        _definitionIdentity = definitionIdentity;
    }

    internal DefinitionProvenance? Materialized(object definition, Func<object> capture) =>
        JToken.DeepEquals(_definition, JToken.FromObject(definition))
            ? DefinitionProvenance.Capture(_filePath, _assetsFilePath, _entryName, capture, _definitionIdentity)
            : null;
}
