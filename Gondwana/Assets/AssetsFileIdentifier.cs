using Newtonsoft.Json;

namespace Gondwana.Assets;

/// <summary>
/// Represents an identifier for a specific asset within a file, including its type and name.
/// </summary>
public sealed class AssetsFileIdentifier
{
    /// <summary>
    /// Gets the <see cref="AssetsFile"/> that contains the asset.
    /// </summary>
    [JsonProperty]
    public AssetsFile AssetsFile { get; private set; } = null!;

    /// <summary>
    /// Gets the type of the asset.
    /// </summary>
    [JsonProperty]
    public AssetTypes AssetType { get; private set; }

    /// <summary>
    /// Gets the name of the asset.
    /// </summary>
    [JsonProperty]
    public string AssetName { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether this asset identifier is valid and can retrieve data.
    /// </summary>
    /// <remarks>An identifier is considered valid if its <see cref="Data"/> property returns a non-null stream.</remarks>
    [JsonIgnore]
    public bool IsValid => Data is not null;

    /// <summary>
    /// Gets the stream containing the asset data.
    /// </summary>
    /// <remarks>Returns <see langword="null"/> if the asset cannot be found in the associated <see cref="AssetsFile"/>.</remarks>
    [JsonIgnore]
    public Stream? Data => AssetsFile?[AssetType, AssetName];

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsFileIdentifier"/> class.
    /// </summary>
    /// <remarks>This parameterless constructor is primarily used for JSON deserialization.</remarks>
    public AssetsFileIdentifier()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsFileIdentifier"/> class with the specified asset file, type, and name.
    /// </summary>
    /// <param name="resFile">The <see cref="AssetsFile"/> that contains the asset.</param>
    /// <param name="resType">The type of the asset.</param>
    /// <param name="entry">The name of the asset entry.</param>
    public AssetsFileIdentifier(AssetsFile resFile, AssetTypes resType, string entry)
    {
        AssetsFile = resFile;
        AssetType = resType;
        AssetName = entry;
    }

    /// <summary>
    /// Returns a string representation of this asset identifier.
    /// </summary>
    /// <returns>A string containing the asset file path, asset type, and asset name.</returns>
    public override string ToString() =>
        $"Asset File {AssetsFile?.FilePath} / {AssetType} / {AssetName}";
}