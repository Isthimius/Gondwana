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

    /// <summary>
    /// Cancels velocity along the specified axes after a solid collision push-out.
    /// Zeroing only the blocked axis preserves motion along the unblocked axis,
    /// enabling wall-sliding and platformer-style collision response.
    /// </summary>
    /// <param name="cancelX">When <see langword="true"/>, zeroes the horizontal velocity component.</param>
    /// <param name="cancelY">When <see langword="true"/>, zeroes the vertical velocity component.</param>
    void CancelVelocityComponent(bool cancelX, bool cancelY);

    /// <summary>
    /// Marks collision-blocked axes for suppression during the next integrated movement step.
    /// </summary>
    /// <param name="blockX">When <see langword="true"/>, suppresses horizontal re-acceleration on the next integration step.</param>
    /// <param name="blockY">When <see langword="true"/>, suppresses vertical re-acceleration on the next integration step.</param>
    void SetBlockedAxesForNextIntegratedStep(bool blockX, bool blockY);
}
