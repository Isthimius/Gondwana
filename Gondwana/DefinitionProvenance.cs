using Newtonsoft.Json.Linq;

namespace Gondwana;

// Owned by the materialized object (or collection manager), never by a global name map.
// Compare the serializer's complete runtime projection, including nested mutable data.
internal sealed class DefinitionProvenance
{
    internal string? FilePath { get; }
    internal string? AssetsFilePath { get; }
    internal string? AssetEntryName { get; }
    private readonly JToken _baseline;

    private DefinitionProvenance(string? filePath, string? assetsFilePath, string? entryName, object definition)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        AssetsFilePath = string.IsNullOrWhiteSpace(assetsFilePath) ? null : Path.GetFullPath(assetsFilePath);
        AssetEntryName = entryName;
        _baseline = JToken.FromObject(definition);
    }

    internal static DefinitionProvenance? Capture(string? filePath, string? assetsFilePath,
        string? entryName, Func<object> capture)
    {
        if (string.IsNullOrWhiteSpace(filePath) &&
            (string.IsNullOrWhiteSpace(assetsFilePath) || string.IsNullOrWhiteSpace(entryName)))
            return null;
        return new(filePath, assetsFilePath, entryName, capture());
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

    internal DefinitionLoadStamp(object definition, string? filePath = null, string? assetsFilePath = null, string? entryName = null)
    {
        _definition = JToken.FromObject(definition);
        _filePath = filePath;
        _assetsFilePath = assetsFilePath;
        _entryName = entryName;
    }

    internal DefinitionProvenance? Materialized(object definition, Func<object> capture) =>
        JToken.DeepEquals(_definition, JToken.FromObject(definition))
            ? DefinitionProvenance.Capture(_filePath, _assetsFilePath, _entryName, capture)
            : null;
}
