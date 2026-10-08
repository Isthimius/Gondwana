using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for scene layer tile collision.
/// </summary>
public sealed class SceneLayerTileCollisionTests
{
    /// <summary>
    /// Verifies layer tile can configure and register its public collider.
    /// </summary>
    [Fact]
    public void LayerTile_CanConfigureAndRegisterItsPublicCollider()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 32,
            height: 32,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        var tile = layer[0, 0]!;
        var collider = Assert.IsAssignableFrom<ICollider>(tile.Collider);

        collider.CollisionGroup = scene.CollisionGroups.WorldStatic;
        collider.CollidesWith = scene.CollisionGroups.Actors;
        tile.CollisionsEnabled = true;

        Assert.Contains(collider, layer.ColliderRegistry.StaticColliders);

        tile.CollisionsEnabled = false;

        Assert.DoesNotContain(collider, layer.ColliderRegistry.StaticColliders);
    }
}
