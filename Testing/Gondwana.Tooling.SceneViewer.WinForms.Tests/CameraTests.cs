using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

public sealed class CameraTests
{
    [Fact]
    public void MovementUsesElapsedTimeAndFastSpeedAndResetRestoresZoom()
    {
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(new Adapter());
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480));
        var view = host.ViewManager.Views[0];
        var controller = new ViewerCameraController(view);
        controller.Move(true, false, false, false, false, .05);
        Assert.Equal(-16f, view.Camera.PositionPx.Y);
        controller.Move(false, true, false, false, false, .05);
        Assert.Equal(PointF.Empty, view.Camera.PositionPx);
        controller.Move(false, false, false, true, true, .05);
        Assert.Equal(48f, view.Camera.PositionPx.X);
        controller.Move(false, false, true, false, false, .1);
        Assert.Equal(16f, view.Camera.PositionPx.X);
        controller.Zoom(120);
        Assert.Equal(1.2f, view.Viewport.Zoom, 5);
        controller.Reset();
        Assert.Equal(PointF.Empty, view.Camera.PositionPx);
        Assert.Equal(1f, view.Viewport.Zoom);
        controller.Zoom(int.MaxValue);
        Assert.Equal(ViewerCameraController.MaximumZoom, view.Viewport.Zoom);
        controller.Zoom(int.MinValue);
        Assert.Equal(ViewerCameraController.MinimumZoom, view.Viewport.Zoom);
    }

    [Fact]
    public void DiagonalSpeedIsNormalizedAndOppositeDirectionsCancel()
    {
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(new Adapter());
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480));
        var view = host.ViewManager.Views[0];
        var controller = new ViewerCameraController(view);
        controller.Move(true, false, false, true, false, .1);
        var position = view.Camera.PositionPx;
        Assert.Equal(32, MathF.Sqrt(position.X * position.X + position.Y * position.Y), 4);
        controller.Move(true, true, true, true, true, .1);
        Assert.Equal(position, view.Camera.PositionPx);
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(640, 480)
    {
        public override void Present(SKImage image, SKRectI source, SKRect dest) => image.Dispose();
    }
}
