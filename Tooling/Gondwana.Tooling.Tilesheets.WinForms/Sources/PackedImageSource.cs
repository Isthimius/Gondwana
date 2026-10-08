namespace Gondwana.Tooling.Tilesheets.Sources;

/// <summary>
/// Identifies an image stored in a Gondwana asset package without extracting it.
/// </summary>
/// <param name="AssetsFilePath">The path to the assets file.</param>
/// <param name="AssetEntryName">The entry name inside the assets file.</param>
public sealed record PackedImageSource(
    string AssetsFilePath,
    string AssetEntryName)
{
    /// <summary>
    /// Gets the full assets file path.
    /// </summary>
    public string FullAssetsFilePath => Path.GetFullPath(AssetsFilePath);

    /// <inheritdoc/>
    public override string ToString() =>
        $"{Path.GetFileName(AssetsFilePath)} : {AssetEntryName}";
}
