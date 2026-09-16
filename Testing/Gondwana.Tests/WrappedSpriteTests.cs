using System.Drawing;
using System.Numerics;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests;

[Collection("SpriteManager")]
public sealed class WrappedSpriteTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LayerTopology_DoesNotChangeMovementOptIn(bool layerWrap, bool movementWrap)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = layer.WrapVertically = layerWrap;
        var sprite = SpriteManager.Instance.CreateSprite(layer, default);
        try
        {
            sprite.SetPosition(new(3.9f, 1));
            sprite.Movement.WrapX = movementWrap;
            sprite.Movement.SetVelocity(new(1, 0));
            sprite.Movement.AdvanceMovement(0.2f);
            Assert.InRange(sprite.GetPosition().X, movementWrap ? 0.09f : 4.09f, movementWrap ? 0.11f : 4.11f);
        }
        finally { SpriteManager.Instance._spriteList.Remove(sprite); sprite.DisposeImmediate(); }
    }

    [Fact]
    public void FollowedLayer_TakesPrecedence_AndNormalizedSpriteCollidesImmediately()
    {
        using var scene = new Scene();
        scene.AddLayer(20, 20); // Non-periodic background does not own a sprite-follow camera.
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = true;
        var sprite = SpriteManager.Instance.CreateSprite(layer, default);
        try
        {
            sprite.Visible = true;
            sprite.RenderSize = new(16, 16);
            sprite.Collider!.CollisionGroup = sprite.Collider.CollidesWith = 1;
            sprite.CollisionType = TileCollisionType.Trigger;
            sprite.SetPosition(new(3.9f, 1));
            var camera = new Camera(scene) { WorldBoundsPx = new(0, 0, 128, 128), GetVisibleWorldSizePx = () => new(32, 32) };
            camera.FollowCentered(sprite, hard: true);
            camera.SnapTo(new(120, 0));
            camera.Update(1);
            float before = camera.PositionPx.X;
            sprite.Movement.WrapX = true;
            sprite.Movement.SetVelocity(new(1, 0));
            sprite.Movement.AdvanceMovement(0.2f);
            camera.Update(1);
            Assert.InRange(camera.PositionPx.X - before, 5, 8);
            var tile = layer[0, 1]!;
            tile.CollisionsEnabled = true;
            tile.Collider!.CollisionGroup = tile.Collider.CollidesWith = 1;
            bool hit = false;
            layer.CollisionResolver.TriggerOverlap += (a, b, _) => hit |= ReferenceEquals(a, sprite.Collider) && ReferenceEquals(b, tile.Collider);
            layer.CollisionResolver.Resolve();
            Assert.True(hit);
            var instances = layer.GetDrawablesInWorldRect(new(125, 30, 30, 30)).Cast<WrappedDrawable>();
            Assert.Contains(instances, d => ReferenceEquals(d.Owner, sprite) && d.Offset.X == 128);
        }
        finally { SpriteManager.Instance._spriteList.Remove(sprite); sprite.DisposeImmediate(); }
    }
}
