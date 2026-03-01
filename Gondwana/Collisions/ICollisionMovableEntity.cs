namespace Gondwana.Collisions;

public interface ICollisionMovableEntity : ICollisionEntity
{
    /// <summary>
    /// Applies a translation in world pixel space.
    /// </summary>
    void TranslateWorldPx(int dx, int dy);
}
