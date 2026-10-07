using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Overlays;

namespace Gondwana.Tests.Widgets;

/// <summary>Verifies reusable tooltip positioning and lifecycle behavior.</summary>
public sealed class TooltipWidgetTests
{
    [Fact]
    public void TooltipClampsToViewAndDoesNotInterceptInput()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tooltip = new TooltipWidget(
            host,
            view,
            new Size(180, 70));

        Assert.False(tooltip.Visible);
        Assert.False(tooltip.IsInputEnabled);
        Assert.False(tooltip.IsPointerInputEnabled);
        Assert.False(tooltip.IsKeyboardInputEnabled);

        tooltip.ShowTooltip(
            "query.cpu.ms\nTime spent querying visible drawables.",
            new Point(635, 475));

        Assert.True(tooltip.Visible);
        Assert.Equal(
            "query.cpu.ms\nTime spent querying visible drawables.",
            tooltip.Label.Text);
        Assert.True(view.Viewport.TargetRectPx.Contains(tooltip.Bounds));

        tooltip.HideTooltip();

        Assert.False(tooltip.Visible);
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        host.ViewManager.AddView(bounds, zOrder: 0);
        return host.ViewManager.Views.Single();
    }
}
