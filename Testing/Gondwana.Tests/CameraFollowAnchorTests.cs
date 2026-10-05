using System.Drawing;
using System.Numerics;
using Gondwana.Physics.Movement;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests;

public sealed class CameraFollowAnchorTests
{
    [Fact]
    public void Camera_FollowAt_PlacesTargetAtRequestedViewportAnchor()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        var camera = CreateCamera(scene);
        var target = new TestMovable(layer, MovementSpace.Pixel, new Vector2(400, 300));

        camera.FollowAt(target, new PointF(0.2f, 0.75f), hard: true);
        camera.Update(1f);

        Assert.Equal(new PointF(360, 225), camera.PositionPx);
    }

    [Fact]
    public void Camera_FollowCentered_RemainsCentered()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        var camera = CreateCamera(scene);
        var target = new TestMovable(layer, MovementSpace.Pixel, new Vector2(400, 300));

        camera.FollowCentered(target, hard: true);
        camera.Update(1f);

        Assert.Equal(new PointF(300, 250), camera.PositionPx);
    }

    [Fact]
    public void Camera_FollowAtX_PreservesVerticalCameraPosition()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        var camera = CreateCamera(scene);
        camera.SnapTo(new PointF(100, 70));
        var target = new TestMovable(layer, MovementSpace.Pixel, new Vector2(400, 300));

        camera.FollowAtX(target, horizontalAnchor: 0.2f, hard: true);
        camera.Update(1f);

        Assert.Equal(new PointF(360, 70), camera.PositionPx);
    }

    [Fact]
    public void Camera_FollowAtY_PreservesHorizontalCameraPosition()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        var camera = CreateCamera(scene);
        camera.SnapTo(new PointF(80, 40));
        var target = new TestMovable(layer, MovementSpace.Pixel, new Vector2(400, 300));

        camera.FollowAtY(target, verticalAnchor: 0.85f, hard: true);
        camera.Update(1f);

        Assert.Equal(new PointF(80, 215), camera.PositionPx);
    }

    [Theory]
    [InlineData(-0.01f, 0.5f)]
    [InlineData(1.01f, 0.5f)]
    [InlineData(0.5f, -0.01f)]
    [InlineData(0.5f, 1.01f)]
    public void Camera_FollowAt_RejectsAnchorsOutsideViewport(float x, float y)
    {
        using var scene = new Scene();
        scene.AddLayer(4, 4);
        var camera = CreateCamera(scene);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => camera.FollowAt(() => PointF.Empty, new PointF(x, y)));
    }

    [Fact]
    public void Camera_FollowAt_UsesAnchorWhenChoosingWrappedImage()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4, 32, 32);
        layer.WrapHorizontally = true;

        var camera = new Camera(scene)
        {
            GetVisibleWorldSizePx = () => new SizeF(64, 64)
        };

        camera.SnapTo(new PointF(40, 0));
        camera.FollowAt(
            () => new PointF(1, 32),
            new PointF(0f, 0.5f),
            hardFollow: true);

        camera.Update(1f);

        Assert.Equal(new PointF(1, 0), camera.PositionPx);
    }

    private static Camera CreateCamera(Scene scene) =>
        new(scene)
        {
            GetVisibleWorldSizePx = () => new SizeF(200, 100)
        };

    private sealed class TestMovable(
        SceneLayer sceneLayer,
        MovementSpace positionSpace,
        Vector2 position) : IMovableOnSceneLayer
    {
        public MovementSpace PositionSpace { get; } = positionSpace;
        public SceneLayer SceneLayer { get; } = sceneLayer;
        private Vector2 Position { get; set; } = position;

        public Vector2 GetPosition() => Position;

        public void SetPosition(Vector2 pos) => Position = pos;
    }
}
