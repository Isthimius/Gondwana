using Gondwana.Assets;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Wraps a single AssetFile entry for display in the DataGrid.
/// </summary>
public sealed class AssetRecordViewModel : ViewModelBase
{
    public AssetTypes AssetType { get; init; }
    public string AssetName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

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
