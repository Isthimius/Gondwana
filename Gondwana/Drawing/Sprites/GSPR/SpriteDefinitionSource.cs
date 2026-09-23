using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>
/// Captures provenance details for a GSPR scene definition.
/// </summary>
public sealed class SpriteDefinitionSource
{
    [JsonConstructor]
    private SpriteDefinitionSource() { }

    /// <summary>
    /// Gets the high-level source kind.
    /// </summary>
    [JsonProperty]
    public SpriteDefinitionSourceKind Kind { get; private init; }

    /// <summary>
    /// Gets the .gspr path when loaded from a loose definition file.
    /// </summary>
    [JsonProperty]
    public string? GsprFilePath { get; private init; }

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
    /// Creates a source marker for a loose .gspr file.
    /// </summary>
    public static SpriteDefinitionSource LooseDefinitionFile(string gsprFilePath)
    {
        if (string.IsNullOrWhiteSpace(gsprFilePath))
            throw new ArgumentException("GSPR file path must be a non-empty string.", nameof(gsprFilePath));

        return new SpriteDefinitionSource
        {
            Kind = SpriteDefinitionSourceKind.LooseDefinitionFile,
            GsprFilePath = gsprFilePath
        };
    }

    /// <summary>
    /// Creates a source marker for a packed .gspr entry inside an assets file.
    /// </summary>
    public static SpriteDefinitionSource PackedDefinitionFile(string assetsFilePath, string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));

        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        return new SpriteDefinitionSource
        {
            Kind = SpriteDefinitionSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }

    /// <summary>
    /// Creates a source marker for a definition generated from runtime state or tooling.
    /// </summary>
    public static SpriteDefinitionSource Generated() =>
        new()
        {
            Kind = SpriteDefinitionSourceKind.Generated
        };

    /// <summary>
    /// Creates an empty source marker when no provenance is known.
    /// </summary>
    public static SpriteDefinitionSource None() =>
        new()
        {
            Kind = SpriteDefinitionSourceKind.None
        };
}
