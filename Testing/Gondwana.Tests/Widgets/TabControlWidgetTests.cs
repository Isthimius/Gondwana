using System.Drawing;
using System.Numerics;
using System.Reflection;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Tests.Widgets;

/// <summary>
/// Verifies layout, selection, input, and reordering behavior for <see cref="TabControlWidget"/>.
/// </summary>
public sealed class TabControlWidgetTests
{
    /// <summary>
    /// Verifies that one-row headers share the available control width and expose a common content area.
    /// </summary>
    [Fact]
    public void AddTab_DefaultSingleRow_DistributesHeadersAcrossControlWidth()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tabs = new TabControlWidget(host, view, new Rectangle(10, 20, 360, 220));

        TabPageWidget general = tabs.AddTab("General", mnemonic: 'G');
        TabPageWidget audio = tabs.AddTab("Audio", mnemonic: 'A');
        TabPageWidget video = tabs.AddTab("Video", mnemonic: 'V');

        Assert.Equal(1, tabs.DisplayedTabRowCount);
        Assert.Equal(3, tabs.DisplayedTabColumnCount);
        Assert.Equal(new Rectangle(10, 20, 120, 32), general.HeaderBounds);
        Assert.Equal(new Rectangle(130, 20, 120, 32), audio.HeaderBounds);
        Assert.Equal(new Rectangle(250, 20, 120, 32), video.HeaderBounds);
        Assert.Equal(new Rectangle(10, 52, 360, 188), general.ContentBounds);
        Assert.Equal(general.ContentBounds, audio.ContentBounds);
        Assert.Equal(general.ContentBounds, video.ContentBounds);
    }

    /// <summary>
    /// Verifies that a fixed row count arranges tab headers in row-major order.
    /// </summary>
    [Fact]
    public void TabRowCount_UsesConfiguredNumberOfRows()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tabs = new TabControlWidget(host, view, new Rectangle(0, 0, 300, 220))
        {
            TabRowCount = 2
        };

        TabPageWidget first = tabs.AddTab("One");
        TabPageWidget second = tabs.AddTab("Two");
        TabPageWidget third = tabs.AddTab("Three");
        TabPageWidget fourth = tabs.AddTab("Four");
        TabPageWidget fifth = tabs.AddTab("Five");

        Assert.Equal(2, tabs.DisplayedTabRowCount);
        Assert.Equal(3, tabs.DisplayedTabColumnCount);
        Assert.Equal(new Rectangle(0, 0, 100, 32), first.HeaderBounds);
        Assert.Equal(new Rectangle(200, 0, 100, 32), third.HeaderBounds);
        Assert.Equal(new Rectangle(0, 32, 100, 32), fourth.HeaderBounds);
        Assert.Equal(new Rectangle(100, 32, 100, 32), fifth.HeaderBounds);
        Assert.Equal(new Rectangle(0, 64, 300, 156), second.ContentBounds);
    }

    /// <summary>
    /// Verifies that automatic rows preserve the preferred column capacity as tabs are added.
    /// </summary>
    [Fact]
    public void AutoExpandRows_AddsRowsUsingPreferredTabWidth()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tabs = new TabControlWidget(host, view, new Rectangle(0, 0, 360, 220))
        {
            AutoExpandRows = true,
            PreferredTabWidth = 120
        };

        tabs.AddTab("One");
        tabs.AddTab("Two");
        tabs.AddTab("Three");
        TabPageWidget fourth = tabs.AddTab("Four");

        Assert.Equal(2, tabs.DisplayedTabRowCount);
        Assert.Equal(3, tabs.DisplayedTabColumnCount);
        Assert.Equal(new Rectangle(0, 32, 120, 32), fourth.HeaderBounds);
        Assert.Equal(new Rectangle(0, 64, 360, 156), fourth.ContentBounds);
    }

    /// <summary>
    /// Verifies that only the selected page remains visible and that page children use content-relative offsets.
    /// </summary>
    [Fact]
    public void SelectingPage_HidesInactivePageAndKeepsChildOffsetsContentRelative()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tabs = new TabControlWidget(host, view, new Rectangle(10, 20, 300, 180));
        TabPageWidget first = tabs.AddTab("First");
        TabPageWidget second = tabs.AddTab("Second");
        using var child = new ButtonWidget(host, view, new Rectangle(0, 0, 100, 30), "Child");

        first.AddWidget(child, new Point(12, 18));
        tabs.Show();

        Assert.True(first.Background.Visible);
        Assert.False(second.Background.Visible);
        Assert.Equal(new Vector2(22, 70), child.GetPosition());

        tabs.SelectTab(second);

        Assert.False(first.Background.Visible);
        Assert.True(second.Background.Visible);
    }

    /// <summary>
    /// Verifies that an unhandled Alt mnemonic selects the matching page through widget keyboard fallback routing.
    /// </summary>
    [Fact]
    public void AltMnemonic_SelectsMatchingPageAfterFocusedWidgetDeclinesInput()
    {
        using var host = new TestRenderSurfaceHost();
        using var router = new WidgetInputRouter(host, null, null, null);
        View view = AddView(host);
        router.Start();

        using var tabs = new TabControlWidget(host, view, new Rectangle(0, 0, 300, 180));
        tabs.AddTab("General", mnemonic: 'G');
        TabPageWidget audio = tabs.AddTab("Audio", mnemonic: 'A');
        tabs.Show();

        RouteKeyboardInput(router, 'A', KeyboardModifierState.Alt);

        Assert.Same(audio, tabs.SelectedTab);
    }

    /// <summary>
    /// Verifies that programmatic and drag-based reordering retain the selected page and report the move.
    /// </summary>
    [Fact]
    public void ReorderingTabs_PreservesSelectedPageAndSupportsPointerDrag()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        using var tabs = new TabControlWidget(host, view, new Rectangle(0, 0, 360, 180))
        {
            IsTabReorderingEnabled = true,
            TabReorderDragThresholdPx = 1f
        };

        TabPageWidget first = tabs.AddTab("One");
        TabPageWidget second = tabs.AddTab("Two");
        TabPageWidget third = tabs.AddTab("Three");
        tabs.SelectTab(second);

        (TabPageWidget Page, int From, int To)? move = null;
        tabs.TabReordered += (page, from, to) => move = (page, from, to);

        tabs.MoveTab(2, 0);

        Assert.Equal(new[] { third, first, second }, tabs.Tabs);
        Assert.Same(second, tabs.SelectedTab);
        Assert.NotNull(move);
        Assert.Same(third, move.Value.Page);
        Assert.Equal(2, move.Value.From);
        Assert.Equal(0, move.Value.To);

        WidgetBase[] headers = tabs.ChildWidgets
            .Where(widget => widget.CanReceiveFocus)
            .OrderBy(widget => widget.GetDrawLocationScreen(view).Left)
            .ToArray();
        WidgetBase firstHeader = headers[0];
        WidgetBase thirdHeader = headers[^1];
        PointF start = GetCenter(firstHeader.GetDrawLocationScreen(view));
        PointF destination = GetCenter(thirdHeader.GetDrawLocationScreen(view));

        DispatchPointer(firstHeader, "DispatchPointerDown", view, start, WidgetPointerButtonEnum.Left, pointerId: 1);
        DispatchPointer(firstHeader,
                        "DispatchPointerMove",
                        view,
                        destination,
                        WidgetPointerButtonEnum.Left,
                        pointerId: 1);
        DispatchPointer(firstHeader, "DispatchPointerUp", view, destination, WidgetPointerButtonEnum.Left, pointerId: 1);

        Assert.Equal(new[] { first, second, third }, tabs.Tabs);
        Assert.Same(second, tabs.SelectedTab);
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.ZOrder == 0 &&
            view.Viewport.TargetRectPx == bounds);
    }

    private static void DispatchPointer(WidgetBase widget,
                                        string method,
                                        View view,
                                        PointF position,
                                        WidgetPointerButtonEnum button,
                                        int pointerId)
    {
        var args = new WidgetPointerEventArgs(
            widget,
            view,
            position,
            button,
            deltaPx: Vector2.Zero,
            pointerId: pointerId);

        typeof(WidgetBase)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(widget, [args]);
    }

    private static PointF GetCenter(RectangleF bounds)
    {
        return new PointF(
            bounds.Left + bounds.Width / 2f,
            bounds.Top + bounds.Height / 2f);
    }

    private static void RouteKeyboardInput(WidgetInputRouter router,
                                           int key,
                                           KeyboardModifierState modifiers)
    {
        typeof(WidgetInputRouter)
            .GetMethod("RouteKeyboardInput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(router, [key, KeyAction.Pressed, modifiers]);
    }
}
