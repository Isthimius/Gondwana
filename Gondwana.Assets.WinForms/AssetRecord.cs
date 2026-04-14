using Gondwana.Assets;

namespace Gondwana.Assets.WinForms;

internal sealed class AssetRecord
{
    public AssetTypes AssetType { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
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
