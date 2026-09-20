using Newtonsoft.Json;

namespace Gondwana.Audio.GAUD;

/// <summary>
/// Captures provenance details for a Gondwana audio (.gaud) definition.
/// </summary>
public sealed class AudioDefinitionSource
{
    [JsonConstructor]
    private AudioDefinitionSource() { }

    [JsonProperty]
    public AudioDefinitionSourceKind Kind { get; private init; }

    [JsonProperty]
    public string? GaudFilePath { get; private init; }

    [JsonProperty]
    public string? AssetsFilePath { get; private init; }

    [JsonProperty]
    public string? AssetEntryName { get; private init; }

    public static AudioDefinitionSource LooseDefinitionFile(string gaudFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gaudFilePath);
        return new AudioDefinitionSource
        {
            Kind = AudioDefinitionSourceKind.LooseDefinitionFile,
            GaudFilePath = gaudFilePath
        };
    }

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

    public static AudioDefinitionSource Generated() =>
        new() { Kind = AudioDefinitionSourceKind.Generated };

    public static AudioDefinitionSource None() =>
        new() { Kind = AudioDefinitionSourceKind.None };
}
