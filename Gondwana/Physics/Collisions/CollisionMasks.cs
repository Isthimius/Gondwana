namespace Gondwana.Physics.Collisions;

/// <summary>
/// Provides predefined collision mask constants for common collision filtering scenarios.
/// </summary>
public static class CollisionMasks
{
    /// <summary>
    /// Represents a collision mask with no groups enabled (all bits clear).
    /// Used to indicate that no collisions should be detected.
    /// </summary>
    public const int None = 0;
    
    /// <summary>
    /// Represents a collision mask with all groups enabled (all bits set).
    /// Used to indicate that collisions with all groups should be detected.
    /// </summary>
    public const int All = ~0;
}
