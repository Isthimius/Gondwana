using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

public sealed class CameraTests
{
    [Fact]
    public void MovementLerpsTowardAndAwayFromRequestedVelocity()
    {
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(new Adapter());
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480));
        var view = host.ViewManager.Views[0];
        var controller = new ViewerCameraController(view);

        controller.Move(true, false, false, false, false, .05);
        float firstY = view.Camera.PositionPx.Y;
        Assert.InRange(firstY, -16f, -.01f);

        controller.Move(true, false, false, false, false, .05);
        float secondY = view.Camera.PositionPx.Y;
        float acceleratingStep = Math.Abs(secondY - firstY);
        Assert.True(acceleratingStep > Math.Abs(firstY));

        controller.Move(false, false, false, false, false, .05);
        float thirdY = view.Camera.PositionPx.Y;
        float deceleratingStep = Math.Abs(thirdY - secondY);
        Assert.InRange(deceleratingStep, .01f, acceleratingStep);

        controller.Reset();
        Assert.Equal(PointF.Empty, view.Camera.PositionPx);

        controller.Move(false, false, false, true, true, .05);
        float fastStep = view.Camera.PositionPx.X;
        controller.Reset();
        controller.Move(false, false, false, true, false, .05);
        float normalStep = view.Camera.PositionPx.X;
        Assert.True(fastStep > normalStep * 2.9f);

        controller.Zoom(120);
        Assert.Equal(1f, view.Viewport.Zoom);
        controller.Reset();
        Assert.Equal(1f, view.Viewport.Zoom);
    }

    [Fact]
    public void DiagonalTargetSpeedIsNormalizedAndOppositeDirectionsCancel()
    {
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(new Adapter());
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480));
        var view = host.ViewManager.Views[0];
        var controller = new ViewerCameraController(view);

        controller.Move(true, false, false, true, false, .1);
        var diagonal = view.Camera.PositionPx;
        float diagonalDistance = MathF.Sqrt(
            diagonal.X * diagonal.X +
            diagonal.Y * diagonal.Y);

        controller.Reset();
        controller.Move(false, false, false, true, false, .1);
        float straightDistance = view.Camera.PositionPx.X;
        Assert.Equal(straightDistance, diagonalDistance, 4);

        controller.Reset();
        controller.Move(true, true, true, true, false, .1);
        Assert.Equal(PointF.Empty, view.Camera.PositionPx);
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(640, 480)
    {
        public override void Present(SKImage image, SKRectI source, SKRect dest) => image.Dispose();
    }
}
