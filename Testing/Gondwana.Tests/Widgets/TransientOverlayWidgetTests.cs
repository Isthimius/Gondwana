using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.Widgets;
using Gondwana.Widgets.Overlays;

namespace Gondwana.Tests.Widgets;

public sealed class TransientOverlayWidgetTests
{
    [Fact]
    public void Toast_AnchorsInsideViewAndSlidesFromConfiguredEdge()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(
            host,
            new Rectangle(100, 50, 400, 300));

        using var toast = new ToastWidget(
            host,
            view,
            new Size(120, 60),
            "Saved",
            WidgetAnchor.TopRight,
            marginPx: 10)
        {
            HoldDurationSec = null,
            DisposeOnDismiss = false,
            SlideOrigin = ToastSlideOrigin.Right,
            SourceOffsetPx = 8,
            TransitionDurationSec = 0.25f
        };

        toast.ShowToast();

        Assert.Equal(new Rectangle(370, 60, 120, 60), toast.TargetBounds);
        Assert.Equal(new Vector2(508, 60), toast.GetPosition());
        Assert.Equal(ToastState.Entering, toast.CurrentState);

        long entranceTick =
            HighResTimer.GetCurrentTick()
            + HighResTimer.TicksPerSecond;

        toast.Update(entranceTick);

        Assert.Equal(new Vector2(370, 60), toast.GetPosition());
        Assert.Equal(ToastState.Holding, toast.CurrentState);

        toast.Dismiss();
        toast.Update(entranceTick + HighResTimer.TicksPerSecond);

        Assert.Equal(ToastState.Hidden, toast.CurrentState);
        Assert.False(toast.Visible);
    }

    [Fact]
    public void Toast_FadeCanRemainUntilManuallyDismissed()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 640, 480));

        using var toast = new ToastWidget(
            host,
            view,
            new Rectangle(20, 30, 240, 64),
            "Waiting")
        {
            Transition = ToastTransition.Fade,
            TransitionDurationSec = 0f,
            HoldDurationSec = null,
            AnimateDismissal = false,
            DisposeOnDismiss = false
        };

        toast.ShowToast();

        Assert.Equal(ToastState.Holding, toast.CurrentState);
        Assert.True(toast.Visible);

        toast.Dismiss();

        Assert.Equal(ToastState.Hidden, toast.CurrentState);
        Assert.False(toast.Visible);
    }

    [Fact]
    public void Popup_ResolvesProjectionAwareGridSourceWhenShown()
    {
        using var host = new TestRenderSurfaceHost();

        SceneLayer layer = host.Scene.AddLayer(
            columnCount: 8,
            rowCount: 8,
            width: 64,
            height: 32,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.IsometricRhombic);

        SceneLayerTile source = layer[2, 3]!;
        Rectangle sourceBounds = source.DrawLocationWorld;

        using var popup = new PopupWidget(
                host,
                layer,
                new Rectangle(0, 0, 80, 30),
                "+100")
            .BindTo(
                layer,
                new Point(2, 3),
                WidgetAnchor.TopCenter,
                new Point(4, -6));

        popup.FadeInSec = 0f;
        popup.FadeOutSec = 0f;
        popup.DisposeOnComplete = false;
        popup.VelocityPxPerSec = Vector2.Zero;

        popup.ShowPopup();

        var expected = new Vector2(
            sourceBounds.Left + sourceBounds.Width / 2f - 40f + 4f,
            sourceBounds.Top - 15f - 6f);

        Assert.Equal(new Point(2, 3), popup.SourceGridLocation);
        Assert.Same(source, popup.SourceTile);
        Assert.Equal(expected, popup.GetPosition());
    }

    [Fact]
    public void Popup_AppliesMovementAndCompletesAfterLifetime()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 640, 480));

        using var popup = new PopupWidget(
            host,
            view,
            new Rectangle(100, 100, 80, 30),
            "-12")
        {
            LifetimeSec = 0.5f,
            FadeInSec = 0f,
            FadeOutSec = 0f,
            VelocityPxPerSec = new Vector2(20f, -40f),
            DisposeOnComplete = false
        };

        popup.ShowPopup();

        long movementTick =
            HighResTimer.GetCurrentTick()
            + HighResTimer.TicksPerSecond / 4;

        popup.Update(movementTick);

        Assert.True(popup.GetPosition().X > 100f);
        Assert.True(popup.GetPosition().Y < 100f);

        popup.Update(movementTick + HighResTimer.TicksPerSecond);

        Assert.False(popup.Visible);
    }

    private static View AddView(
        TestRenderSurfaceHost host,
        Rectangle bounds)
    {
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.Viewport.TargetRectPx == bounds);
    }
}
