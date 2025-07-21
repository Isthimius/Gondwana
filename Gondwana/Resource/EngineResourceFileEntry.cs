using Microsoft.Extensions.Logging;

namespace Gondwana.Resource;

/// <summary>
/// Represents an entry for an engine resource file, including its type and name.
/// </summary>
/// <remarks>This class provides functionality to parse a string representation of an engine resource file entry
/// and to compare entries for equality. The string representation is expected in the format
/// 'ResourceType_ResourceName'.</remarks>
public sealed class EngineResourceFileEntry : IEquatable<EngineResourceFileEntry>
{
    /// <summary>
    /// Gets or sets the type of resource associated with the engine.
    /// </summary>
    public EngineResourceFileTypes ResourceType { get; set; }

    /// <summary>
    /// Gets or sets the name of the resource.
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// Returns a string representation of the resource, combining the resource type and name.
    /// </summary>
    /// <returns>A string in the format "ResourceType_resourcename", where "ResourceType" is the type of the resource and
    /// "resourcename" is the lowercase version of the resource name.</returns>
    public override string ToString() => $"{ResourceType}_{ResourceName.ToLower()}";

    /// <summary>
    /// Parses a string representation of an engine resource file entry into an <see cref="EngineResourceFileEntry"/>
    /// object.
    /// </summary>
    /// <remarks>The method logs a warning if the input string is null, empty, or not in the expected format.
    /// It also logs a warning if the resource type is invalid.</remarks>
    /// <param name="entry">The string to parse, expected in the format 'ResourceType_ResourceName'.</param>
    /// <returns>An <see cref="EngineResourceFileEntry"/> object if the string is successfully parsed; otherwise, <see
    /// langword="null"/>.</returns>
    internal static EngineResourceFileEntry? FromString(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            Engine.Logger.LogWarning("Attempted to parse an empty or null entry string.");
            return null;
        }

        var parts = entry.Split('_', 2);
        if (parts.Length != 2)
        {
            Engine.Logger.LogWarning("Invalid entry format: {Entry}. Expected format is 'ResourceType_ResourceName'.", entry);
            return null;
        }

        if (!Enum.TryParse<EngineResourceFileTypes>(parts[0], ignoreCase: true, out var resourceType))
        {
            Engine.Logger.LogWarning("Invalid resource type: {ResourceType}. Valid types are: {ValidTypes}.",
                parts[0], string.Join(", ", Enum.GetNames(typeof(EngineResourceFileTypes))));
            return null;
        }

        return new EngineResourceFileEntry
        {
            ResourceType = resourceType,
            ResourceName = parts[1]
        };
    }

    public bool Equals(EngineResourceFileEntry? other)
    {
        if (other is null) return false;
        return ResourceType == other.ResourceType &&
               string.Equals(ResourceName, other.ResourceName, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as EngineResourceFileEntry);

    public override int GetHashCode()
    {
        return HashCode.Combine(ResourceType, ResourceName.ToLowerInvariant());
    }
}
