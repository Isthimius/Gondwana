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

            if (overlap.Width < overlap.Height)
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
        }

        if (totalDx != 0 || totalDy != 0)
        {
            movableOwner.TranslateWorldPx(totalDx, totalDy);
        }

        // Cancel velocity along any axis that had a solid collision, so the entity
        // slides along the unblocked axis instead of being stopped entirely.
        if (hitX || hitY)
        {
            movableOwner.CancelVelocityComponent(hitX, hitY);
        }
    }
}