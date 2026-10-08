using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class AssetRecord
{
    /// <summary>
    /// Gets or sets the asset type.
    /// </summary>
    public AssetTypes AssetType { get; set; }
    /// <summary>
    /// Gets or sets the asset name.
    /// </summary>
    public string AssetName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the size bytes.
    /// </summary>
    public long SizeBytes { get; set; }
    /// <summary>
    /// Gets the display size.
    /// </summary>
    public string DisplaySize => FormatSize(SizeBytes);

    private static string FormatSize(long size)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        double value = size;
        int index = 0;

        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {suffixes[index]}";
    }
}
