namespace Gondwana.Assets;

/// <summary>A requested physical change; it never changes a logical definition identity.</summary>
public sealed class ReferenceRelocation
{
    /// <summary>Gets the original physical endpoint.</summary>
    public AssetLocation Source { get; }

    /// <summary>Gets the replacement physical endpoint.</summary>
    public AssetLocation Destination { get; }

    /// <summary>Creates a same-type relocation between loose or packed endpoints.</summary>
    /// <param name="source">The original physical location.</param>
    /// <param name="destination">The new physical location of the same resource or document.</param>
    public ReferenceRelocation(AssetLocation source, AssetLocation destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (source.AssetType != destination.AssetType)
            throw new ArgumentException($"Incompatible relocation from {source.AssetType} to {destination.AssetType}.", nameof(destination));
        Source = source;
        Destination = destination;
    }
}
