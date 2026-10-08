using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Movement;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for wrapped camera.
/// </summary>
public sealed class WrappedCameraTests
{
    /// <summary>
    /// Verifies camera clamps only non wrapped axes.
    /// </summary>
    /// <param name="horizontal">The horizontal value for this test case.</param>
    /// <param name="vertical">The vertical value for this test case.</param>
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

    /// <summary>
    /// Verifies camera follows nearest image across canonicalization.
    /// </summary>
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

    /// <summary>
    /// Verifies camera follow centered x selects wrapped image by tracked axis.
    /// </summary>
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
        /// <summary>
        /// Gets the coordinate space used by the position API.
        /// </summary>
        public MovementSpace PositionSpace { get; } = positionSpace;
        /// <inheritdoc/>
        public SceneLayer SceneLayer { get; } = sceneLayer;
        private Vector2 Position { get; set; } = position;
        /// <summary>
        /// Gets the sprite position in scene-layer grid coordinates.
        /// </summary>
        /// <returns>The sprite position in scene-layer grid coordinates.</returns>
        public Vector2 GetPosition() => Position;
        /// <summary>
        /// Sets the sprite position in scene-layer grid coordinates and invalidates its old and new bounds.
        /// </summary>
        /// <param name="pos">The pos value for this test case.</param>
        public void SetPosition(Vector2 pos) => Position = pos;
    }
}
