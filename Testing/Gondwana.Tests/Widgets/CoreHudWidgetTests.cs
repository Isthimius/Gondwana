using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Layout;

namespace Gondwana.Tests.Widgets;

/// <summary>
/// Contains regression tests for core hud widget.
/// </summary>
public sealed class CoreHudWidgetTests
{
    /// <summary>
    /// Verifies label widget exposes text and screen bounds.
    /// </summary>
    [Fact]
    public void LabelWidget_ExposesTextAndScreenBounds()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var label = new LabelWidget(
            host,
            view,
            new Rectangle(10, 20, 120, 30),
            "Ready");

        label.SetText("Go");

        Assert.Equal("Go", label.Text);
        Assert.Equal(new Rectangle(10, 20, 120, 30), label.Bounds);
    }

    /// <summary>
    /// Verifies progress bar widget fills proportionally in both orientations.
    /// </summary>
    [Fact]
    public void ProgressBarWidget_FillsProportionallyInBothOrientations()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var progress = new ProgressBarWidget(
            host,
            view,
            new Rectangle(10, 20, 104, 24),
            value: 0.25f);

        Assert.Equal(25, progress.FillBounds.Width);
        Assert.Equal(20, progress.FillBounds.Height);

        progress.Orientation = WidgetOrientation.Vertical;
        progress.Value = 0.5f;

        Assert.Equal(100, progress.FillBounds.Width);
        Assert.Equal(10, progress.FillBounds.Height);
        Assert.Equal(progress.TrackBounds.Bottom - progress.Padding, progress.FillBounds.Bottom);
    }

    /// <summary>
    /// Verifies stack panel widget spaces children vertically and horizontally.
    /// </summary>
    [Fact]
    public void StackPanelWidget_SpacesChildrenVerticallyAndHorizontally()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var stack = new StackPanelWidget(
            host,
            DirectDrawingMode.View,
            new PointF(10, 20),
            WidgetOrientation.Vertical,
            spacing: 5f);

        var first = new LabelWidget(host, view, new Rectangle(0, 0, 100, 10), "One");
        var second = new LabelWidget(host, view, new Rectangle(0, 0, 80, 20), "Two");

        stack.AddWidget(first).AddWidget(second);

        Assert.Equal(new Vector2(10, 20), first.GetPosition());
        Assert.Equal(new Vector2(10, 35), second.GetPosition());
        Assert.Equal(new SizeF(100, 35), stack.ContentSize);

        stack.Orientation = WidgetOrientation.Horizontal;

        Assert.Equal(new Vector2(10, 20), first.GetPosition());
        Assert.Equal(new Vector2(115, 20), second.GetPosition());
        Assert.Equal(new SizeF(185, 20), stack.ContentSize);
    }

    /// <summary>
    /// Verifies panel widget moves child with panel anchor.
    /// </summary>
    [Fact]
    public void PanelWidget_MovesChildWithPanelAnchor()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var panel = new PanelWidget(
            host,
            view,
            new Rectangle(40, 50, 200, 100),
            Color.Black);

        var label = new LabelWidget(host, view, new Rectangle(0, 0, 80, 20), "Child");
        panel.AddWidget(label, new Point(10, 12));

        Assert.Equal(new Vector2(50, 62), label.GetPosition());

        panel.SetPosition(100, 200);

        Assert.Equal(new Vector2(110, 212), label.GetPosition());
        Assert.Equal(new Point(100, 200), panel.Bounds.Location);
    }

    /// <summary>
    /// Verifies label widget size and padding use layout invalidating apis.
    /// </summary>
    [Fact]
    public void LabelWidget_SizeAndPaddingUseLayoutInvalidatingApis()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var label = new LabelWidget(
            host,
            view,
            new Rectangle(10, 20, 120, 30),
            "Ready");

        label.Size = new Size(140, 35);
        label.SetPadding(6f, 4f);

        Assert.Equal(new Rectangle(10, 20, 140, 35), label.Bounds);
        Assert.Equal(6f, label.TextBlock.HorizontalPadding);
        Assert.Equal(4f, label.TextBlock.VerticalPadding);
    }

    /// <summary>
    /// Verifies panel widget set panel z order preserves composite child internal z order.
    /// </summary>
    [Fact]
    public void PanelWidget_SetPanelZOrder_PreservesCompositeChildInternalZOrder()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var panel = new PanelWidget(
            host,
            view,
            new Rectangle(40, 50, 200, 100),
            Color.Black);

        using var progress = new ProgressBarWidget(
            host,
            view,
            new Rectangle(0, 0, 120, 20),
            value: 0.5f);

        panel.AddWidget(progress, new Point(10, 12));
        panel.SetPanelZOrder(20);

        Assert.Equal(20, panel.Background.ZOrder);
        Assert.Equal(21, progress.Track.ZOrder);
        Assert.Equal(22, progress.Fill.ZOrder);
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        int zOrder = host.ViewManager.Views.Count;

        host.ViewManager.AddView(bounds, zOrder: zOrder);

        return host.ViewManager.Views.Single(view =>
            view.ZOrder == zOrder &&
            view.Viewport.TargetRectPx == bounds);
    }
}
