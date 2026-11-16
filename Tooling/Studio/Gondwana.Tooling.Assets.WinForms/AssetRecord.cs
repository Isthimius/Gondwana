using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class AssetRecord
{
    /// <summary>Gets or sets the typed archive category of this row.</summary>
    public AssetTypes AssetType { get; set; }
    /// <summary>Gets or sets the exact archive entry name within its asset type.</summary>
    public string AssetName { get; set; } = string.Empty;
    /// <summary>Gets or sets the uncompressed entry length in bytes.</summary>
    public long SizeBytes { get; set; }
    /// <summary>Gets the entry length formatted with up to two decimal places and a base-1024 B, KB, MB, or GB suffix.</summary>
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
