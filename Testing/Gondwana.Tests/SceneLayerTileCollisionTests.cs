using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;

namespace Gondwana.Tests;

public sealed class SceneLayerTileCollisionTests
{
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
