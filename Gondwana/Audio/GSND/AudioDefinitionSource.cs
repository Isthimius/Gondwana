using Newtonsoft.Json;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Captures provenance details for a Gondwana audio (.gsnd) definition.
/// </summary>
public sealed class AudioDefinitionSource
{
    [JsonConstructor]
    private AudioDefinitionSource() { }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    [JsonProperty]
    public AudioDefinitionSourceKind Kind { get; private init; }

    /// <summary>
    /// Gets the path to the loose GSND definition file.
    /// </summary>
    [JsonProperty]
    public string? GsndFilePath { get; private init; }

    /// <summary>
    /// Gets the path to the containing assets file.
    /// </summary>
    [JsonProperty]
    public string? AssetsFilePath { get; private init; }

    /// <summary>
    /// Gets the entry name inside the assets file.
    /// </summary>
    [JsonProperty]
    public string? AssetEntryName { get; private init; }

    /// <summary>
    /// Creates provenance for a definition stored in a loose file.
    /// </summary>
    /// <param name="gsndFilePath">The path to the loose GSND audio definition.</param>
    /// <returns>A source descriptor containing the loose definition file path.</returns>
    public static AudioDefinitionSource LooseDefinitionFile(string gsndFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gsndFilePath);
        return new AudioDefinitionSource
        {
            Kind = AudioDefinitionSourceKind.LooseDefinitionFile,
            GsndFilePath = gsndFilePath
        };
    }

    /// <summary>
    /// Creates provenance for a definition stored inside an assets file.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A source descriptor containing the assets file path and entry name.</returns>
    public static AudioDefinitionSource PackedDefinitionFile(string assetsFilePath, string assetEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        return new AudioDefinitionSource
        {
            Kind = AudioDefinitionSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }

    /// <summary>
    /// Creates provenance for a definition generated from runtime state.
    /// </summary>
    /// <returns>A source descriptor identifying runtime-generated content.</returns>
    public static AudioDefinitionSource Generated() =>
        new() { Kind = AudioDefinitionSourceKind.Generated };

    /// <summary>
    /// Creates an empty definition-source descriptor.
    /// </summary>
    /// <returns>A descriptor with no persisted source.</returns>
    public static AudioDefinitionSource None() =>
        new() { Kind = AudioDefinitionSourceKind.None };
}
