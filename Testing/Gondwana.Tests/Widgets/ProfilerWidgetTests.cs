using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Hud;

namespace Gondwana.Tests.Widgets;

public sealed class ProfilerWidgetTests
{
    [Fact]
    public void MeasurementVisibility_SupportsGlobalAndSourceSpecificOverrides()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(12, 12, 400, 300));

        Assert.False(widget.IsCollectionActive);
        Assert.False(widget.Visible);
        Assert.True(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));

        widget.SetMeasurementVisible("cycle.cpu.ms", false);
        Assert.False(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));

        widget.SetMeasurementVisible("Engine", "cycle.cpu.ms", true);
        Assert.True(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));

        widget.SetSourceVisible("Engine", false);
        Assert.False(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));
    }

    [Fact]
    public void SelectedMode_ShowsOnlyExplicitlySelectedMeasurements()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(0, 0, 400, 300))
        {
            MeasurementVisibilityMode = ProfilerMeasurementVisibilityMode.Selected
        };

        Assert.False(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));

        widget.SetMeasurementVisible("cycle.cpu.ms", true);

        Assert.True(widget.IsMeasurementVisible("Engine", "cycle.cpu.ms"));
        Assert.False(widget.IsMeasurementVisible("Engine", "background.cpu.ms"));
    }

    [Fact]
    public void DisplayOptions_ExposeBoundsRefreshAndScrollableLabel()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(20, 30, 420, 310));

        widget.Size = new Size(460, 330);
        widget.RefreshInterval = TimeSpan.FromMilliseconds(500);
        widget.ShowHeader = false;
        widget.ShowSnapshotMetadata = false;
        widget.ShowSourceHeaders = false;
        widget.ShowUnavailableMeasurements = true;

        Assert.Equal(new Rectangle(20, 30, 460, 330), widget.Bounds);
        Assert.Equal(TimeSpan.FromMilliseconds(500), widget.RefreshInterval);
        Assert.False(widget.ShowHeader);
        Assert.False(widget.ShowSnapshotMetadata);
        Assert.False(widget.ShowSourceHeaders);
        Assert.True(widget.ShowUnavailableMeasurements);
        Assert.Equal(Gondwana.Widgets.Controls.ScrollBarVisibility.Auto, widget.Display.VerticalScrollBarVisibility);
    }

    [Fact]
    public void RuntimeContextAndAdditionalLines_AreOptInAndComposable()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(0, 0, 400, 300))
        {
            HeaderText = "Custom diagnostics",
            ContextInfo = ProfilerContextInfo.Scene | ProfilerContextInfo.View,
            ShowSnapshotMetadata = false
        };

        ProfilerWidgetExtensionContext? observedContext = null;
        widget.AdditionalLinesProvider = context =>
        {
            observedContext = context;
            return ["Application: test"];
        };

        widget.Refresh();

        Assert.NotNull(observedContext);
        Assert.Same(host, observedContext.RenderSurfaceHost);
        Assert.Same(view, observedContext.View);
        Assert.Contains("Custom diagnostics", widget.Display.Text);
        Assert.Contains("Application: test", widget.Display.Text);
        Assert.Contains("Animating tiles:", widget.Display.Text);
        Assert.Contains("Layers / grid cells:", widget.Display.Text);
        Assert.Contains("Camera:", widget.Display.Text);
        Assert.Contains("Viewport:", widget.Display.Text);
    }

    [Fact]
    public void RuntimeContext_DefaultsToNone()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(0, 0, 400, 300));

        Assert.Equal(ProfilerContextInfo.None, widget.ContextInfo);
        Assert.Null(widget.AdditionalLinesProvider);
        Assert.Equal("Gondwana Runtime Profiler", widget.HeaderText);
    }

    [Fact]
    public void RefreshInterval_RejectsExcessiveDisplayPolling()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(0, 0, 400, 300));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            widget.RefreshInterval = TimeSpan.FromMilliseconds(1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            widget.RefreshInterval = TimeSpan.FromMinutes(2));
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        host.ViewManager.AddView(bounds, zOrder: 0);
        return host.ViewManager.Views.Single();
    }
}
