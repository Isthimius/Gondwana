using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Movement;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests;

public sealed class WrappedCameraTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Camera_ClampsOnlyNonWrappedAxes(bool horizontal, bool vertical)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = horizontal;
        layer.WrapVertically = vertical;
        var camera = new Camera(scene) { WorldBoundsPx = new(0, 0, 128, 128), GetVisibleWorldSizePx = () => new(32, 32) };
        camera.SnapTo(new(200, -30));
        Assert.Equal(new PointF(horizontal ? 200 : 96, vertical ? -30 : 0), camera.PositionPx);
    }

    [Fact]
    public void Camera_FollowsNearestImage_AcrossCanonicalization()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = true;
        var camera = new Camera(scene) { WorldBoundsPx = new(0, 0, 128, 128), GetVisibleWorldSizePx = () => new(32, 32) };
        camera.SnapTo(new(110, 0));
        var target = new PointF(127, 16);
        camera.Follow(() => target, true);
        camera.Update(1);
        Assert.Equal(111, camera.PositionPx.X);
        target.X = 1;
        camera.Update(1);
        Assert.Equal(113, camera.PositionPx.X);
    }

    [Fact]
    public void Camera_FollowCenteredX_SelectsWrappedImageByTrackedAxis()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4, 32, 32, coordinateSystem: CoordinateSystemTypes.IsometricAxial);
        layer.WrapHorizontally = true;
        layer.WrapVertically = true;
        var camera = new Camera(scene) { GetVisibleWorldSizePx = () => new(32, 32) };
        camera.SnapTo(new(184, 84)); // center = (200, 100)
        var target = new TestMovable(layer, MovementSpace.Pixel, new(130, 0));
        camera.FollowCenteredX(target, hard: true);
        camera.Update(1);
        Assert.Equal(new PointF(178, 84), camera.PositionPx);
    }

    private sealed class TestMovable(SceneLayer sceneLayer, MovementSpace positionSpace, Vector2 position) : IMovableOnSceneLayer
    {
        public MovementSpace PositionSpace { get; } = positionSpace;
        public SceneLayer SceneLayer { get; } = sceneLayer;
        private Vector2 Position { get; set; } = position;
        public Vector2 GetPosition() => Position;
        public void SetPosition(Vector2 pos) => Position = pos;
    }
}
