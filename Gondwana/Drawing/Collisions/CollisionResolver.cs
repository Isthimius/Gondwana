using System.Drawing;
using Gondwana.Collision;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Collisions;

/// <summary>
/// Simple collision resolution for Sprites against collidable Tiles.
/// Phase 1: axis-aligned AABB push-out, Sprite vs. Tiles on same SceneLayer.
/// </summary>
internal static class CollisionResolver
{
    private static readonly List<ICollider> _queryResults = new();

    /// <summary>
    /// Resolves collisions for all Sprites that have collision detection enabled.
    /// </summary>
    internal static void ResolveSpriteTileCollisions(Scene scene)
    {
        var world = scene.CollisionWorld;
        if (world == null)
            return;

        foreach (var sprite in SpriteManager.AllSprites)
        {
            // only handle sprites in this scene
            if (sprite.SceneLayer.Scene != scene)
                continue;

            ResolveForSprite(sprite, world);
        }
    }

    private static void ResolveForSprite(Sprite sprite, CollisionWorld world)
    {
        var layer = sprite.SceneLayer;
        if (layer == null)
            return;

        // Start from the sprite’s current collision rect in pixel space.
        Rectangle rect = sprite.CollisionArea;

        // Build AABB for broad-phase query.
        var aabb = Aabb.FromRectangle(rect);

        // For now, treat the sprite’s “layerMask” as 1 and it collides with everything (~0).
        // Later you can pass real masks from the sprite’s collider.
        const int spriteLayerMask = 1;
        const int spriteCollidesWithMask = ~0;

        world.QueryAabb(aabb, spriteLayerMask, spriteCollidesWithMask, _queryResults);

        foreach (var collider in _queryResults)
        {
            // Don’t collide with yourself
            if (ReferenceEquals(collider.Owner, sprite))
                continue;

            // Only care about Tiles on the same SceneLayer
            if (collider.Owner is not Tile tile)
                continue;

            if (tile.SceneLayer != layer)
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
