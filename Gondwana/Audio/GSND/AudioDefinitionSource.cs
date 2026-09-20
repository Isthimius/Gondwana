using Newtonsoft.Json;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Captures provenance details for a Gondwana audio (.gsnd) definition.
/// </summary>
public sealed class AudioDefinitionSource
{
    [JsonConstructor]
    private AudioDefinitionSource() { }

    [JsonProperty]
    public AudioDefinitionSourceKind Kind { get; private init; }

    [JsonProperty]
    public string? GsndFilePath { get; private init; }

    [JsonProperty]
    public string? AssetsFilePath { get; private init; }

    [JsonProperty]
    public string? AssetEntryName { get; private init; }

    public static AudioDefinitionSource LooseDefinitionFile(string gsndFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gsndFilePath);
        return new AudioDefinitionSource
        {
            Kind = AudioDefinitionSourceKind.LooseDefinitionFile,
            GsndFilePath = gsndFilePath
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
