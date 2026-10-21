using Gondwana.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

internal sealed class DocumentLocationComparer : IEqualityComparer<AssetLocation>
{
    internal static readonly DocumentLocationComparer Instance = new();
    public bool Equals(AssetLocation? left, AssetLocation? right) =>
        ReferenceEquals(left, right) || left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left.Path, right.Path) &&
        StringComparer.OrdinalIgnoreCase.Equals(left.AssetEntryName, right.AssetEntryName) &&
        (left.AssetEntryName is null || left.AssetType == right.AssetType);
    public int GetHashCode(AssetLocation location) => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(location.Path),
        location.AssetEntryName is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(location.AssetEntryName),
        location.AssetEntryName is null ? 0 : (int)location.AssetType);
}
