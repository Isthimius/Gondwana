using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;

namespace Gondwana.Collisions;

/// <summary>
/// Simple collision resolution for Sprites against collidable Tiles.
/// Phase 1: axis-aligned AABB push-out, Sprite vs. Tiles on same SceneLayer.
/// </summary>
internal sealed class CollisionResolver
{
    private readonly List<ICollider> _queryResults = new();
    private readonly ColliderRegistry _world;

    internal CollisionResolver(ColliderRegistry world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    /// Resolves collisions for all Sprites that have collision detection enabled.
    /// </summary>
    internal void ResolveTileCollisions()
    {
        foreach (var dyn in _world.DynamicColliders)
        {
            if (dyn.Owner is not Sprite sprite)
                continue;

            ResolveForSprite(sprite, dyn);
        }
    }

    private void ResolveForSprite(Sprite sprite, ICollider mover)
    {
        // Start from the sprite’s current collision rect in pixel space.
        Rectangle rect = sprite.CollisionArea;

        // Build AABB for broad-phase query.
        var aabb = Aabb.FromRectangle(rect);

        _world.QueryAabb(aabb, mover.LayerMask, mover.CollidesWithMask, _queryResults);

        foreach (var collider in _queryResults)
        {
            // Don’t collide with yourself
            if (ReferenceEquals(collider, mover))
                continue;

            // Only care about Tiles on the same SceneLayer
            if (collider.Owner is not Tile tile)
                continue;

            var other = collider.BoundsWorldPx.ToRectangle();

            if (!rect.IntersectsWith(other))
                continue;

            // Compute intersection
            Rectangle overlap = Rectangle.Intersect(rect, other);
            if (overlap.IsEmpty)
                continue;

            // Centers for deciding push direction
            float centerX = rect.Left + rect.Width * 0.5f;
            float centerY = rect.Top + rect.Height * 0.5f;

            float otherCenterX = other.Left + other.Width * 0.5f;
            float otherCenterY = other.Top + other.Height * 0.5f;

            // Minimum Translation Vector: push along the smallest axis of overlap.
            int dx = 0;
            int dy = 0;

            if (overlap.Width < overlap.Height)
            {
                // Push horizontally
                dx = (centerX < otherCenterX) ? -overlap.Width : overlap.Width;
            }
            else
            {
                // Push vertically
                dy = (centerY < otherCenterY) ? -overlap.Height : overlap.Height;
            }

            rect.X += dx;
            rect.Y += dy;
        }

        // If rect changed, map back to grid coordinates and update the sprite.
        if (rect != sprite.CollisionArea)
        {
            var sceneCoord = sprite.GetSceneLayerCoordsFromSpriteWorldRect(rect);
            sprite.SetPosition(new System.Numerics.Vector2(sceneCoord.X, sceneCoord.Y));
        }
    }
}
