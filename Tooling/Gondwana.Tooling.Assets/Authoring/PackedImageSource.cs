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

    /// <summary>Formats the archive filename and image entry name for display.</summary>
    /// <returns>The archive filename, a colon separator, and the entry name.</returns>
    public override string ToString() =>
        $"{Path.GetFileName(AssetsFilePath)} : {AssetEntryName}";
}
