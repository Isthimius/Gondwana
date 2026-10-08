using Newtonsoft.Json;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Captures provenance details for a GSCN scene definition.
/// </summary>
public sealed class SceneDefinitionSource
{
    [JsonConstructor]
    private SceneDefinitionSource() { }

    /// <summary>
    /// Gets the high-level source kind.
    /// </summary>
    [JsonProperty]
    public SceneDefinitionSourceKind Kind { get; private init; }

    /// <summary>
    /// Gets the .gscn path when loaded from a loose definition file.
    /// </summary>
    [JsonProperty]
    public string? GscnFilePath { get; private init; }

    /// <summary>
    /// Gets the .gaf path when loaded from a packed definition file.
    /// </summary>
    [JsonProperty]
    public string? AssetsFilePath { get; private init; }

    /// <summary>
    /// Gets the packed entry name when loaded from a packed definition file.
    /// </summary>
    [JsonProperty]
    public string? AssetEntryName { get; private init; }

    /// <summary>
    /// Creates a source marker for a loose .gscn file.
    /// </summary>
    /// <param name="gscnFilePath">The gscn file path.</param>
    /// <returns>A source descriptor containing the loose definition file path.</returns>
    public static SceneDefinitionSource LooseDefinitionFile(string gscnFilePath)
    {
        if (string.IsNullOrWhiteSpace(gscnFilePath))
            throw new ArgumentException("GSCN file path must be a non-empty string.", nameof(gscnFilePath));

        return new SceneDefinitionSource
        {
            Kind = SceneDefinitionSourceKind.LooseDefinitionFile,
            GscnFilePath = gscnFilePath
        };
    }

    /// <summary>
    /// Creates a source marker for a packed .gscn entry inside an assets file.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A source descriptor containing the assets file path and entry name.</returns>
    public static SceneDefinitionSource PackedDefinitionFile(string assetsFilePath, string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));

        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        return new SceneDefinitionSource
        {
            Kind = SceneDefinitionSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }

    /// <summary>
    /// Creates a source marker for a definition generated from runtime state or tooling.
    /// </summary>
    /// <returns>A source descriptor identifying runtime-generated content.</returns>
    public static SceneDefinitionSource Generated() =>
        new()
        {
            Kind = SceneDefinitionSourceKind.Generated
        };

    /// <summary>
    /// Creates an empty source marker when no provenance is known.
    /// </summary>
    /// <returns>A descriptor with no persisted source.</returns>
    public static SceneDefinitionSource None() =>
        new()
        {
            Kind = SceneDefinitionSourceKind.None
        };
}
