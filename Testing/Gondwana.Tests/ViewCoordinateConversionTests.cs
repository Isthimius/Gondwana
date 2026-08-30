using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests.Rendering.Views;

/// <summary>
/// Verifies the public zoom contract and the invertibility of View coordinate conversions.
/// </summary>
public sealed class ViewCoordinateConversionTests
{
    [Theory]
    [InlineData(1f, 100f)]
    [InlineData(2f, 200f)]
    [InlineData(0.5f, 50f)]
    public void WorldPxToScreenPx_AppliesConventionalZoom(
        float zoom,
        float expectedScreenDisplacement)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100);
        var view = CreateView(
            scene,
            zoom: zoom,
            cameraPositionPx: new PointF(100f, 50f));

        PointF screenPx = view.WorldPxToScreenPx(
            layer,
            new PointF(200f, 50f));

        AssertClose(expectedScreenDisplacement, screenPx.X);
        AssertClose(0f, screenPx.Y);
    }

    [Theory]
    [InlineData(1f, 100f)]
    [InlineData(2f, 50f)]
    [InlineData(0.5f, 200f)]
    public void ScreenPxToWorldPx_AppliesInverseZoom(
        float zoom,
        float expectedWorldDisplacement)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100);
        var view = CreateView(
            scene,
            zoom: zoom,
            cameraPositionPx: new PointF(100f, 50f));

        PointF worldPx = view.ScreenPxToWorldPx(
            layer,
            new PointF(100f, 0f));

        AssertClose(100f + expectedWorldDisplacement, worldPx.X);
        AssertClose(50f, worldPx.Y);
    }

    [Fact]
    public void WorldPxToScreenPx_ReturnsAbsoluteAdapterCoordinates()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100);
        var view = CreateView(
            scene,
            zoom: 2f,
            targetRectPx: new Rectangle(400, 100, 800, 600),
            screenOffsetPx: new PointF(10f, 20f),
            cameraPositionPx: new PointF(100f, 50f));

        PointF screenPx = view.WorldPxToScreenPx(
            layer,
            new PointF(125f, 70f));

        AssertClose(460f, screenPx.X);
        AssertClose(160f, screenPx.Y);
    }

    [Fact]
    public void PointConversions_RoundTripWithCameraOffsetsZoomAndParallax()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            100,
            100,
            parallax: 0.65f);

        var view = CreateView(
            scene,
            zoom: 1.75f,
            targetRectPx: new Rectangle(300, 120, 900, 500),
            screenOffsetPx: new PointF(7f, -3f),
            cameraPositionPx: new PointF(120f, 80f));

        var originalWorldPx = new PointF(450.25f, 275.75f);

        PointF screenPx = view.WorldPxToScreenPx(
            layer,
            originalWorldPx);

        PointF restoredWorldPx = view.ScreenPxToWorldPx(
            layer,
            screenPx);

        AssertClose(originalWorldPx, restoredWorldPx);
    }

    [Fact]
    public void WorldRectToScreenRect_ScalesPositionAndSizeByZoom()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100);
        var view = CreateView(
            scene,
            zoom: 2f,
            cameraPositionPx: new PointF(100f, 50f));

        RectangleF screenRect = view.WorldRectToScreenRect(
            layer,
            new RectangleF(125f, 70f, 40f, 30f));

        AssertClose(
            new RectangleF(50f, 40f, 80f, 60f),
            screenRect);
    }

    [Fact]
    public void RectangleConversions_RoundTripWithCameraOffsetsZoomAndParallax()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            100,
            100,
            parallax: 0.65f);

        var view = CreateView(
            scene,
            zoom: 1.75f,
            targetRectPx: new Rectangle(300, 120, 900, 500),
            screenOffsetPx: new PointF(7f, -3f),
            cameraPositionPx: new PointF(120f, 80f));

        var originalWorldRect =
            new RectangleF(450.25f, 275.75f, 125.5f, 64.25f);

        RectangleF screenRect = view.WorldRectToScreenRect(
            layer,
            originalWorldRect);

        RectangleF restoredWorldRect = view.ScreenRectToWorldRect(
            layer,
            screenRect);

        AssertClose(originalWorldRect, restoredWorldRect);
    }

    [Fact]
    public void ZoomAroundScreenPoint_WhenSnapped_PreservesWorldPointUnderCursor()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            100,
            100,
            parallax: 0.5f);

        var view = CreateView(
            scene,
            zoom: 1f,
            targetRectPx: new Rectangle(100, 50, 800, 600),
            screenOffsetPx: new PointF(20f, 10f),
            cameraPositionPx: new PointF(200f, 100f));

        var cursorScreenPx = new PointF(500f, 350f);
        PointF worldBefore = view.ScreenPxToWorldPx(
            layer,
            cursorScreenPx);

        view.ZoomAroundScreenPoint(
            layer,
            cursorScreenPx,
            targetZoom: 2f,
            durationSeconds: 0f);

        PointF worldAfter = view.ScreenPxToWorldPx(
            layer,
            cursorScreenPx);

        Assert.Equal(2f, view.Viewport.Zoom);
        AssertClose(worldBefore, worldAfter);
    }

    [Theory]
    [InlineData(1f, 800f, 600f)]
    [InlineData(2f, 400f, 300f)]
    [InlineData(0.5f, 1600f, 1200f)]
    public void VisibleWorldSizePx_UsesReciprocalZoom(
        float zoom,
        float expectedWidth,
        float expectedHeight)
    {
        var viewport = new Viewport
        {
            TargetRectPx = new Rectangle(0, 0, 800, 600),
            Zoom = zoom
        };

        AssertClose(expectedWidth, viewport.VisibleWorldSizePx.Width);
        AssertClose(expectedHeight, viewport.VisibleWorldSizePx.Height);
    }

    [Fact]
    public void RenderContext_KeepsViewTransformStableWhenLiveStateChanges()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100, parallax: 0.75f);
        var view = CreateView(
            scene,
            zoom: 1.5f,
            targetRectPx: new Rectangle(100, 50, 800, 600),
            screenOffsetPx: new PointF(12f, -8f),
            cameraPositionPx: new PointF(120f, 80f));

        var worldPoint = new PointF(410f, 260f);
        var worldRect = new RectangleF(390f, 240f, 64f, 48f);
        var screenRect = new RectangleF(250f, 180f, 96f, 72f);

        PointF pointBefore;
        PointF pointAfter;
        RectangleF worldRectBefore;
        RectangleF worldRectAfter;
        RectangleF screenRectBefore;
        RectangleF screenRectAfter;
        Rectangle viewportBefore;
        Rectangle viewportAfter;

        Gondwana.Rendering.RenderContext.Push(view, tick: 1);
        try
        {
            pointBefore = view.WorldPxToScreenPx(layer, worldPoint);
            worldRectBefore = view.WorldRectToScreenRect(layer, worldRect);
            screenRectBefore = view.ScreenRectToWorldRect(layer, screenRect);
            viewportBefore = view.GetRenderViewportTargetRectPx();

            view.Camera.SnapTo(new PointF(500f, 350f));
            view.Viewport.Zoom = 2.5f;
            view.Viewport.TargetRectPx = new Rectangle(300, 200, 1024, 768);
            view.Viewport.ScreenOffsetPx = new PointF(-30f, 40f);

            pointAfter = view.WorldPxToScreenPx(layer, worldPoint);
            worldRectAfter = view.WorldRectToScreenRect(layer, worldRect);
            screenRectAfter = view.ScreenRectToWorldRect(layer, screenRect);
            viewportAfter = view.GetRenderViewportTargetRectPx();
        }
        finally
        {
            Gondwana.Rendering.RenderContext.Pop();
        }

        AssertClose(pointBefore, pointAfter);
        AssertClose(worldRectBefore, worldRectAfter);
        AssertClose(screenRectBefore, screenRectAfter);
        Assert.Equal(viewportBefore, viewportAfter);

        Assert.NotEqual(pointAfter, view.WorldPxToScreenPx(layer, worldPoint));
        Assert.NotEqual(viewportAfter, view.GetRenderViewportTargetRectPx());
    }

    private static View CreateView(
        Scene scene,
        float zoom,
        Rectangle? targetRectPx = null,
        PointF? screenOffsetPx = null,
        PointF? cameraPositionPx = null)
    {
        var viewport = new Viewport
        {
            TargetRectPx = targetRectPx ?? new Rectangle(0, 0, 800, 600),
            ScreenOffsetPx = screenOffsetPx ?? PointF.Empty,
            Zoom = zoom
        };

        var camera = new Camera(scene);
        var view = new View(camera, viewport);

        camera.SnapTo(cameraPositionPx ?? PointF.Empty);

        return view;
    }

    private static void AssertClose(
        float expected,
        float actual,
        float tolerance = 0.001f)
    {
        Assert.InRange(
            actual,
            expected - tolerance,
            expected + tolerance);
    }

    private static void AssertClose(
        PointF expected,
        PointF actual,
        float tolerance = 0.001f)
    {
        AssertClose(expected.X, actual.X, tolerance);
        AssertClose(expected.Y, actual.Y, tolerance);
    }

    private static void AssertClose(
        RectangleF expected,
        RectangleF actual,
        float tolerance = 0.001f)
    {
        AssertClose(expected.X, actual.X, tolerance);
        AssertClose(expected.Y, actual.Y, tolerance);
        AssertClose(expected.Width, actual.Width, tolerance);
        AssertClose(expected.Height, actual.Height, tolerance);
    }
}
