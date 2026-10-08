using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Sprites;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for collision profile.
/// </summary>
[Collection("SpriteManager")]
public sealed class CollisionProfileTests
{
    /// <summary>
    /// Verifies get mask combines registered groups and empty means none.
    /// </summary>
    [Fact]
    public void GetMask_CombinesRegisteredGroupsAndEmptyMeansNone()
    {
        var groups = new CollisionGroupRegistry();

        Assert.Equal(CollisionMasks.None, groups.GetMask([]));
        Assert.Equal(
            groups.Actors | groups.Projectiles,
            groups.GetMask(["Actors", "Projectiles"]));
    }

    /// <summary>
    /// Verifies scene layer default profile applies to every fixed tile.
    /// </summary>
    [Fact]
    public void SceneLayer_DefaultProfileAppliesToEveryFixedTile()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 1, 16, 16);

        foreach (SceneLayerTile tile in layer)
        {
            Assert.Equal(CollisionProfileNames.World, tile.CollisionProfileName);
            Assert.Equal(scene.CollisionGroups.WorldStatic, tile.Collider!.CollisionGroup);
            Assert.Equal(
                scene.CollisionGroups.Actors | scene.CollisionGroups.Projectiles,
                tile.Collider.CollidesWith);
        }

        layer.DefaultTileCollisionProfile = CollisionProfileNames.Sensor;

        foreach (SceneLayerTile tile in layer)
        {
            Assert.Equal(CollisionProfileNames.Sensor, tile.CollisionProfileName);
            Assert.Equal(scene.CollisionGroups.Triggers, tile.Collider!.CollisionGroup);
            Assert.Equal(scene.CollisionGroups.Actors, tile.Collider.CollidesWith);
        }
    }

    /// <summary>
    /// Verifies custom profile resolves names through scene collision groups.
    /// </summary>
    [Fact]
    public void CustomProfile_ResolvesNamesThroughSceneCollisionGroups()
    {
        using var scene = new Scene();
        int enemies = scene.CollisionGroups.Define("Enemies");
        var profile = scene.CollisionProfiles.Define(
            "Enemy",
            "Enemies",
            ["Actors", "Projectiles"]);

        Assert.Equal(enemies, profile.ResolveCollisionGroup(scene.CollisionGroups));
        Assert.Equal(
            scene.CollisionGroups.Actors | scene.CollisionGroups.Projectiles,
            profile.ResolveCollidesWith(scene.CollisionGroups));
    }

    /// <summary>
    /// Verifies attach collider applies state configured before collider exists.
    /// </summary>
    [Fact]
    public void AttachCollider_AppliesStateConfiguredBeforeColliderExists()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        using var tile = new DeferredColliderTile(layer);

        tile.SetCollisionProfile(CollisionProfileNames.Actor);
        tile.CollisionType = TileCollisionType.Trigger;

        Assert.Null(tile.Collider);

        tile.AttachTestCollider();
        var collider = Assert.IsAssignableFrom<ICollider>(tile.Collider);

        Assert.Equal(scene.CollisionGroups.Actors, collider.CollisionGroup);
        Assert.Equal(CollisionResponseType.Trigger, collider.ResponseType);
        Assert.True(tile.CollisionsEnabled);
        Assert.Contains(collider, layer.ColliderRegistry.DynamicColliders);

        tile.CollisionType = TileCollisionType.None;
        Assert.DoesNotContain(collider, layer.ColliderRegistry.DynamicColliders);
    }

    /// <summary>
    /// Verifies sprite profile resolves when existing layer is attached to scene.
    /// </summary>
    [Fact]
    public void SpriteProfile_ResolvesWhenExistingLayerIsAttachedToScene()
    {
        var layer = new SceneLayer(1, 1, 16, 16);
        var bitmap = new global::SkiaSharp.SKBitmap(16, 16);
        using var tilesheet = Gondwana.Drawing.Tilesheets.TilesheetFactory.FromBitmap(
            "DeferredProfile",
            bitmap);
        var sprite = SpriteManager.Instance.CreateSprite(
            layer,
            tilesheet.GetFrame(0, 0),
            collisionProfileName: CollisionProfileNames.Actor);

        try
        {
            Assert.Equal(CollisionMasks.None, sprite.Collider!.CollisionGroup);

            using var scene = new Scene();
            scene.AddLayer(layer);

            Assert.Equal(scene.CollisionGroups.Actors, sprite.Collider.CollisionGroup);
            Assert.Equal(
                scene.CollisionGroups.WorldStatic |
                scene.CollisionGroups.Actors |
                scene.CollisionGroups.Projectiles |
                scene.CollisionGroups.Triggers,
                sprite.Collider.CollidesWith);
        }
        finally
        {
            if (SpriteManager.Instance._spriteList.Remove(sprite))
                sprite.DisposeImmediate();
        }
    }

    /// <summary>
    /// Verifies scene add layer ignores unexpected null sprite entries.
    /// </summary>
    [Fact]
    public void SceneAddLayer_IgnoresUnexpectedNullSpriteEntries()
    {
        SpriteManager.Instance._spriteList.Add(null!);

        try
        {
            using var scene = new Scene();
            var layer = new SceneLayer(1, 1, 16, 16);
            scene.AddLayer(layer);
        }
        finally
        {
            SpriteManager.Instance._spriteList.RemoveAll(sprite => sprite is null);
        }
    }

    private sealed class DeferredColliderTile : Tile
    {
        private readonly SceneLayer _sceneLayer;

        internal DeferredColliderTile(SceneLayer sceneLayer)
        {
            _sceneLayer = sceneLayer;
        }

        /// <inheritdoc/>
        public override bool IsPositionFixed => false;
        /// <inheritdoc/>
        public override Rectangle DrawLocationWorld => new(0, 0, 16, 16);
        /// <inheritdoc/>
        public override PointF SceneLayerCoordinates => PointF.Empty;
        /// <inheritdoc/>
        public override SceneLayer SceneLayer => _sceneLayer;

        internal void AttachTestCollider()
        {
            AttachCollider(new TileCollider(
                this,
                CollisionMasks.None,
                CollisionMasks.None));
        }
    }
}
