using System.Drawing;

namespace Gondwana.Collisions;

/// <summary>
/// Simple collision resolution for dynamic colliders against colliders in the registry.
/// - Solid vs Solid: axis-aligned AABB push-out (minimum-axis).
/// - Trigger involvement: reported via event, no push-out.
/// </summary>
internal sealed class CollisionResolver
{
    private readonly List<ICollider> _queryResults = new();
    private readonly ColliderRegistry _world;

    /// <summary>
    /// Fired when either collider in an overlap is a Trigger. No push-out is performed.
    /// </summary>
    internal event Action<ICollider, ICollider, Rectangle>? TriggerOverlap;

    /// <summary>
    /// Fired when a Solid vs Solid overlap occurs (resolution will also be applied).
    /// </summary>
    internal event Action<ICollider, ICollider, Rectangle>? SolidOverlap;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionResolver"/> class with the specified collider registry.
    /// </summary>
    /// <param name="world">The collider registry containing static and dynamic colliders to resolve against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="world"/> is <c>null</c>.</exception>
    internal CollisionResolver(ColliderRegistry world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    /// Resolves collisions for all dynamic colliders in this registry.
    /// </summary>
    internal void Resolve()
    {
        foreach (var mover in _world.DynamicColliders)
        {
            if (mover.Owner is not ICollisionMovableEntity movableOwner)
                continue; // Dynamic list should only contain movable owners

            ResolveForMover(mover, movableOwner);
        }
    }

    private void ResolveForMover(ICollider mover, ICollisionMovableEntity movableOwner)
    {
        // Start from mover's current collision rect in world pixel space.
        Rectangle rect = mover.Owner.CollisionArea;

        // Broad-phase query based on current rect.
        var aabb = Aabb.FromRectangle(rect);

        _world.QueryAabb(aabb, mover.CollisionGroup, mover.CollidesWith, _queryResults, ignore: mover);

        int totalDx = 0;
        int totalDy = 0;
        int totalAbsDx = 0;
        int totalAbsDy = 0;
        bool hitX = false;
        bool hitY = false;

        foreach (var otherCollider in _queryResults)
        {
            var otherRect = otherCollider.BoundsWorldPx.ToRectangle();

            if (!rect.IntersectsWith(otherRect))
                continue;

            Rectangle overlap = Rectangle.Intersect(rect, otherRect);
            if (overlap.IsEmpty)
                continue;

            // Trigger collisions: report only, no push-out.
            if (mover.ResponseType == CollisionResponseType.Trigger || otherCollider.ResponseType == CollisionResponseType.Trigger)
            {
                TriggerOverlap?.Invoke(mover, otherCollider, overlap);
                continue;
            }

            SolidOverlap?.Invoke(mover, otherCollider, overlap);

            // Centers for deciding push direction
            float centerX = rect.Left + rect.Width * 0.5f;
            float centerY = rect.Top + rect.Height * 0.5f;

            float otherCenterX = otherRect.Left + otherRect.Width * 0.5f;
            float otherCenterY = otherRect.Top + otherRect.Height * 0.5f;

            int dx = 0;
            int dy = 0;

            if (ShouldResolveAlongXAxis(overlap, centerX, centerY, otherCenterX, otherCenterY))
            {
                dx = (centerX < otherCenterX) ? -overlap.Width : overlap.Width;
                hitX = true;
            }
            else
            {
                dy = (centerY < otherCenterY) ? -overlap.Height : overlap.Height;
                hitY = true;
            }

            rect.X += dx;
            rect.Y += dy;

            totalDx += dx;
            totalDy += dy;
            totalAbsDx += Math.Abs(dx);
            totalAbsDy += Math.Abs(dy);
        }

        if (totalDx != 0 || totalDy != 0)
        {
            movableOwner.TranslateWorldPx(totalDx, totalDy);
        }

        // Cancel velocity along any axis that had a solid collision, so the entity
        // slides along the unblocked axis instead of being stopped entirely.
        if (hitX || hitY)
        {
            var (cancelX, cancelY) = SelectVelocityCancellationAxes(hitX, hitY, totalAbsDx, totalAbsDy);
            movableOwner.CancelVelocityComponent(cancelX, cancelY);
            movableOwner.SetBlockedAxesForNextIntegratedStep(cancelX, cancelY);
        }
    }

    internal static bool ShouldResolveAlongXAxis(Rectangle overlap, float centerX, float centerY, float otherCenterX, float otherCenterY)
    {
        bool overlapPrefersX = overlap.Width < overlap.Height;
        float centerDeltaX = MathF.Abs(otherCenterX - centerX);
        float centerDeltaY = MathF.Abs(otherCenterY - centerY);

        // For skewed projections, AABB overlap size alone can misclassify
        // floor/ceiling contacts as horizontal. The center-delta override corrects
        // this, but must only fire when the center-delta evidence is proportionally
        // stronger than the overlap evidence. Requiring centerDeltaRatio > overlapRatio
        // prevents false overrides when the mover has slid far along a tall wall,
        // which inflates the perpendicular center-delta without changing the actual
        // contact axis.
        if (overlapPrefersX)
        {
            float overlapRatio = (float)overlap.Height / overlap.Width;
            float centerDeltaRatio = centerDeltaX > 0 ? centerDeltaY / centerDeltaX : float.MaxValue;
            if (centerDeltaRatio > overlapRatio)
                return false;
        }
        else
        {
            float overlapRatio = (float)overlap.Width / overlap.Height;
            float centerDeltaRatio = centerDeltaY > 0 ? centerDeltaX / centerDeltaY : float.MaxValue;
            if (centerDeltaRatio > overlapRatio)
                return true;
        }

        return overlapPrefersX;
    }

    internal static (bool CancelX, bool CancelY) SelectVelocityCancellationAxes(bool hitX, bool hitY, int totalAbsDx, int totalAbsDy)
    {
        if (!hitX && !hitY)
            return (false, false);

        if (hitX && !hitY)
            return (true, false);

        if (!hitX && hitY)
            return (false, true);

        // Both axes collided this frame; prefer cancelling the axis with the
        // larger accumulated push-out so wall/floor slides keep the tangent component.
        if (totalAbsDx > totalAbsDy)
            return (true, false);

        if (totalAbsDy > totalAbsDx)
            return (false, true);

        // Equal penetration is ambiguous (e.g., corner impact), so cancel both.
        return (true, true);
    }
}
