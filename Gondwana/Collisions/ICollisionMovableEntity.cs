namespace Gondwana.Collisions;

/// <summary>
/// Represents a collision entity that can be moved or repositioned in response to collision resolution.
/// </summary>
public interface ICollisionMovableEntity : ICollisionEntity
{
    /// <summary>
    /// Applies a translation in world pixel space.
    /// </summary>
    /// <param name="dx">The horizontal displacement in pixels.</param>
    /// <param name="dy">The vertical displacement in pixels.</param>
    void TranslateWorldPx(int dx, int dy);
}
