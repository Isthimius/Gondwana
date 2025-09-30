using Newtonsoft.Json;

namespace Gondwana.Assets;

/// <summary>
/// Represents an identifier for a specific asset within a file, including its type and name.
/// </summary>
public sealed class AssetsFileIdentifier
{
    [JsonProperty]
    public AssetsFile AssetsFile { get; private set; } = null!;

    [JsonProperty]
    public AssetTypes AssetType { get; private set; }

    [JsonProperty]
    public string AssetName { get; private set; } = null!;

    [JsonIgnore]
    public bool IsValid => Data is not null;

    [JsonIgnore]
    public Stream? Data => AssetsFile?[AssetType, AssetName];

    public AssetsFileIdentifier()
    { }

    public AssetsFileIdentifier(AssetsFile resFile, AssetTypes resType, string entry)
    {
        AssetsFile = resFile;
        AssetType = resType;
        AssetName = entry;
    }

    public override string ToString() =>
        $"Asset File {AssetsFile?.FilePath} / {AssetType} / {AssetName}";
}