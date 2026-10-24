using Gondwana.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

internal sealed class DocumentLocationComparer : IEqualityComparer<AssetLocation>
{
    internal static readonly DocumentLocationComparer Instance = new();

    /// <summary>Compares paths and entry names without case, including asset type only for packed entries.</summary>
    /// <param name="left">The first location, which may be <see langword="null"/>.</param>
    /// <param name="right">The second location, which may be <see langword="null"/>.</param>
    /// <returns>True for matching physical identities or two null references; otherwise false.</returns>
    public bool Equals(AssetLocation? left, AssetLocation? right) =>
        ReferenceEquals(left, right)
        || left is not null
        && right is not null
        && StringComparer.OrdinalIgnoreCase.Equals(left.Path, right.Path)
        && StringComparer.OrdinalIgnoreCase.Equals(left.AssetEntryName, right.AssetEntryName)
        && (left.AssetEntryName is null || left.AssetType == right.AssetType);

    /// <summary>Hashes the same case-insensitive identity components used by <see cref="Equals"/>.</summary>
    /// <param name="location">The non-null location to hash; loose-file asset types are ignored.</param>
    /// <returns>A hash of the path and, for packed entries, entry name and asset type.</returns>
    public int GetHashCode(AssetLocation location) => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(location.Path),
        location.AssetEntryName is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(location.AssetEntryName),
        location.AssetEntryName is null ? 0 : (int)location.AssetType);
}
