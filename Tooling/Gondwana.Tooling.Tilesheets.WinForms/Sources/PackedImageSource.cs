namespace Gondwana.Tooling.Tilesheets.Sources;

/// <summary>
/// Identifies an image stored in a Gondwana asset package without extracting it.
/// </summary>
public sealed record PackedImageSource(
    string AssetsFilePath,
    string AssetEntryName)
{
    public string FullAssetsFilePath => Path.GetFullPath(AssetsFilePath);

    public override string ToString() =>
        $"{Path.GetFileName(AssetsFilePath)} : {AssetEntryName}";
}
