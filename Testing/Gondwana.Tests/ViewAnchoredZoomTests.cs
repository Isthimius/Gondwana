using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests.Rendering.Views;

public sealed class ViewAnchoredZoomTests
{
    [Fact]
    public void ZoomAroundScreenPoint_WhenAnimated_PreservesWorldAnchorEveryUpdate()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            100,
            100,
            parallax: 0.65f);

        var viewport = new Viewport
        {
            TargetRectPx = new Rectangle(100, 50, 800, 600),
            ScreenOffsetPx = new PointF(20f, 10f),
            Zoom = 1f
        };

        var camera = new Camera(scene);
        var view = new View(camera, viewport);
        camera.SnapTo(new PointF(200f, 100f));

        var screenAnchor = new PointF(500f, 350f);
        PointF worldAnchor = view.ScreenPxToWorldPx(layer, screenAnchor);

        view.ZoomAroundScreenPoint(
            layer,
            screenAnchor,
            targetZoom: 2f,
            durationSeconds: 0.75f);

        for (int i = 0; i < 5; i++)
        {
            view.Update(0.15f);

            PointF currentWorldAnchor =
                view.ScreenPxToWorldPx(layer, screenAnchor);

            AssertClose(worldAnchor, currentWorldAnchor);
        }

        Assert.Equal(2f, viewport.Zoom);
        Assert.False(viewport.IsZoomAnimating);
    }

    [Fact]
    public void ZoomAroundScreenPoint_WhenRetargeted_PreservesAnchorWithoutWobble()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100);

        var viewport = new Viewport
        {
            TargetRectPx = new Rectangle(0, 0, 800, 600),
            Zoom = 1f
        };

        var camera = new Camera(scene);
        var view = new View(camera, viewport);
        camera.SnapTo(new PointF(80f, 40f));

        var screenAnchor = new PointF(375f, 225f);
        PointF worldAnchor = view.ScreenPxToWorldPx(layer, screenAnchor);

        view.ZoomAroundScreenPoint(
            layer,
            screenAnchor,
            targetZoom: 1.5f,
            durationSeconds: 0.75f);

        view.Update(0.20f);
        AssertClose(
            worldAnchor,
            view.ScreenPxToWorldPx(layer, screenAnchor));

        view.ZoomAroundScreenPoint(
            layer,
            screenAnchor,
            targetZoom: 2f,
            durationSeconds: 0.75f);

        for (int i = 0; i < 5; i++)
        {
            view.Update(0.15f);
            AssertClose(
                worldAnchor,
                view.ScreenPxToWorldPx(layer, screenAnchor));
        }

        Assert.Equal(2f, viewport.Zoom);
    }

    private static void AssertClose(
        PointF expected,
        PointF actual,
        float tolerance = 0.001f)
    {
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
    }
}
