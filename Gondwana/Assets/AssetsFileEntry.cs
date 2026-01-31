using Microsoft.Extensions.Logging;

namespace Gondwana.Assets;

/// <summary>
/// Represents an entry for an engine asset file, including its type and name.
/// </summary>
/// <remarks>This class provides functionality to parse a string representation of an engine asset file entry
/// and to compare entries for equality. The string representation is expected in the format
/// 'AssetType_AssetName'.</remarks>
public sealed class AssetsFileEntry : IEquatable<AssetsFileEntry>
{
    /// <summary>
    /// Gets or sets the type of asset associated with the engine.
    /// </summary>
    public AssetTypes AssetType { get; set; }

    /// <summary>
    /// Gets or sets the name of the asset.
    /// </summary>
    public string AssetName { get; set; } = string.Empty;

    /// <summary>
    /// Returns a string representation of the asset, combining the asset type and name.
    /// </summary>
    /// <returns>A string in the format "AssetType_assetname", where "AssetType" is the type of the asset and
    /// "assetname" is the lowercase version of the asset name.</returns>
    public override string ToString() => $"{AssetType}_{AssetName.ToLower()}";

    /// <summary>
    /// Parses a string representation of an engine asset file entry into an <see cref="AssetsFileEntry"/>
    /// object.
    /// </summary>
    /// <remarks>The method logs a warning if the input string is null, empty, or not in the expected format.
    /// It also logs a warning if the asset type is invalid.</remarks>
    /// <param name="entry">The string to parse, expected in the format 'AssetType_AssetName'.</param>
    /// <returns>An <see cref="AssetsFileEntry"/> object if the string is successfully parsed; otherwise, <see
    /// langword="null"/>.</returns>
    internal static AssetsFileEntry? FromString(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            Engine.Logger.LogWarning("Attempted to parse an empty or null entry string.");
            return null;
        }

        var parts = entry.Split('_', 2);
        if (parts.Length != 2)
        {
            Engine.Logger.LogWarning("Invalid entry format: {Entry}. Expected format is 'AssetType_AssetName'.", entry);
            return null;
        }

        if (!Enum.TryParse<AssetTypes>(parts[0], ignoreCase: true, out var assetType))
        {
            Engine.Logger.LogWarning("Invalid asset type: {AssetType}. Valid types are: {ValidTypes}.",
                parts[0], string.Join(", ", Enum.GetNames(typeof(AssetTypes))));
            return null;
        }

        return new AssetsFileEntry
        {
            AssetType = assetType,
            AssetName = parts[1]
        };
    }

    /// <summary>
    /// Determines whether the current <see cref="AssetsFileEntry"/> is equal to another <see cref="AssetsFileEntry"/>
    /// </summary>
    /// <remarks>Two entries are considered equal if they have the same asset type and asset name (case-insensitive comparison).</remarks>
    /// <param name="other">The <see cref="AssetsFileEntry"/> to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified entry is equal to the current entry; otherwise, <see langword="false"/>.</returns>
    public bool Equals(AssetsFileEntry? other)
    {
        if (other is null) return false;
        return AssetType == other.AssetType &&
               string.Equals(AssetName, other.AssetName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="AssetsFileEntry"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified object is an <see cref="AssetsFileEntry"/> and is equal to the current entry; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => Equals(obj as AssetsFileEntry);

    /// <summary>
    /// Returns the hash code for the current <see cref="AssetsFileEntry"/>.
    /// </summary>
    /// <remarks>The hash code is computed based on the asset type and the lowercase version of the asset name to ensure case-insensitive equality.</remarks>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(AssetType, AssetName.ToLowerInvariant());
}
