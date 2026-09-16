using System.Drawing;
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
}
