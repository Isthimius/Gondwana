using Newtonsoft.Json;

namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Captures provenance details for an animation definition.
/// </summary>
public sealed class AnimationDefinitionSource
{
    [JsonConstructor]
    private AnimationDefinitionSource() { }

    /// <summary>
    /// Gets the high-level source kind.
    /// </summary>
    [JsonProperty]
    public AnimationDefinitionSourceKind Kind { get; private init; }

    /// <summary>
    /// Gets the .gani path when loaded from a loose definition file.
    /// </summary>
    [JsonProperty]
    public string? GaniFilePath { get; private init; }

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
    /// Creates provenance for a definition stored in a loose file.
    /// </summary>
    /// <param name="ganiFilePath">The path to the loose GANI animation definition.</param>
    /// <returns>A source descriptor containing the loose definition file path.</returns>
    public static AnimationDefinitionSource LooseDefinitionFile(string ganiFilePath)
    {
        if (string.IsNullOrWhiteSpace(ganiFilePath))
            throw new ArgumentException("GANI file path must be a non-empty string.", nameof(ganiFilePath));

        return new AnimationDefinitionSource
        {
            Kind = AnimationDefinitionSourceKind.LooseDefinitionFile,
            GaniFilePath = ganiFilePath
        };
    }

    /// <summary>
    /// Creates provenance for a definition stored inside an assets file.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A source descriptor containing the assets file path and entry name.</returns>
    public static AnimationDefinitionSource PackedDefinitionFile(
        string assetsFilePath,
        string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));

        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        return new AnimationDefinitionSource
        {
            Kind = AnimationDefinitionSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }

    /// <summary>
    /// Creates provenance for a definition generated from runtime state.
    /// </summary>
    /// <returns>A source descriptor identifying runtime-generated content.</returns>
    public static AnimationDefinitionSource Generated() =>
        new() { Kind = AnimationDefinitionSourceKind.Generated };

    /// <summary>
    /// Creates an empty definition-source descriptor.
    /// </summary>
    /// <returns>A descriptor with no persisted source.</returns>
    public static AnimationDefinitionSource None() =>
        new() { Kind = AnimationDefinitionSourceKind.None };
}
