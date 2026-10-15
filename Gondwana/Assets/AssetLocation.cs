namespace Gondwana.Assets;

/// <summary>
/// A physical endpoint for reference transformation, without opening files or archives.
/// Definition member identities remain in the existing format-specific source models.
/// </summary>
public sealed class AssetLocation
{
    /// <summary>Gets the resource or definition type, including for loose files.</summary>
    public AssetTypes AssetType { get; }

    /// <summary>Gets the absolute loose file path or containing GAF path.</summary>
    public string Path { get; }

    /// <summary>Gets the typed GAF entry name, or null for a loose file.</summary>
    public string? AssetEntryName { get; }

    /// <summary>Creates a physical endpoint; relative and drive-relative paths are rejected.</summary>
    /// <param name="assetType">The resource or native definition type.</param>
    /// <param name="path">An absolute loose file or archive path.</param>
    /// <param name="assetEntryName">The GAF entry name, or null for a loose file.</param>
    public AssetLocation(AssetTypes assetType, string path, string? assetEntryName = null)
    {
        if (!Enum.IsDefined(assetType)) throw new ArgumentOutOfRangeException(nameof(assetType));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!System.IO.Path.IsPathFullyQualified(path))
            throw new ArgumentException("A physical location requires an absolute path.", nameof(path));
        if (assetEntryName is not null) ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        AssetType = assetType;
        Path = System.IO.Path.GetFullPath(path);
        if (string.IsNullOrEmpty(System.IO.Path.GetFileName(Path)))
            throw new ArgumentException("A physical location must name a file, not a filesystem root or directory path.", nameof(path));
        AssetEntryName = assetEntryName;
    }

    /// <summary>Compares physical endpoints using platform path and GAF entry semantics.</summary>
    /// <param name="other">The endpoint to compare.</param>
    /// <returns>Whether both endpoints identify the same typed physical resource.</returns>
    internal bool Matches(AssetLocation other) => AssetType == other.AssetType &&
        string.Equals(Path, other.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
        string.Equals(AssetEntryName, other.AssetEntryName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Formats the typed physical location for diagnostics.</summary>
    /// <returns>The path and optional entry name.</returns>
    public override string ToString() => $"{AssetType}: {Path}" + (AssetEntryName is null ? "" : $" :: {AssetEntryName}");
}
