using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Tests;
using Gondwana.Widgets.Menus;

namespace Gondwana.Tests.Widgets;

public sealed class MenuBarWidgetTests
{
    [Fact]
    public void AddMenu_BuildsHeadersAndDropdownItems()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var menuBar = CreateMenuBar(host, view)
            .AddMenu("File", menu => menu
                .AddItem("New", shortcutText: "Ctrl+N")
                .AddSeparator()
                .AddItem("Exit"))
            .AddMenu("Help", menu => menu
                .AddItem("About"));

        Assert.Equal(2, menuBar.Menus.Count);
        Assert.Equal("File", menuBar.Menus[0].Header.Text);
        Assert.Equal(2, menuBar.Menus[0].DropDown.Items.Count);
        Assert.Equal("Ctrl+N", menuBar.Menus[0].DropDown.Items[0].ShortcutText);
    }


    [Fact]
    public void ClosedMenuBar_DoesNotCaptureTheEntireView()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var menuBar = CreateMenuBar(host, view)
            .AddMenu("File", menu => menu.AddItem("Exit"));

        menuBar.DropDownAnimation = MenuDropDownAnimation.None;
        menuBar.Show();

        Assert.False(menuBar.HitTest(view, new Point(500, 400)));
        Assert.True(menuBar.HitTest(view, new Point(10, 10)));
    }

    [Fact]
    public void OpenMenuAt_ClosesPreviouslyOpenMenu()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);

        using var menuBar = CreateMenuBar(host, view)
            .AddMenu("File", menu => menu.AddItem("Exit"))
            .AddMenu("Help", menu => menu.AddItem("About"));

        menuBar.DropDownAnimation = MenuDropDownAnimation.None;
        menuBar.Show();

        menuBar.OpenMenuAt(0);
        Assert.True(menuBar.Menus[0].DropDown.IsOpen);

        menuBar.OpenMenuAt(1);

        Assert.False(menuBar.Menus[0].DropDown.IsOpen);
        Assert.True(menuBar.Menus[1].DropDown.IsOpen);
        Assert.Equal(1, menuBar.OpenMenuIndex);
    }

    [Fact]
    public void InvokingItem_ClosesMenuAndRunsCommand()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        bool invoked = false;

        using var menuBar = CreateMenuBar(host, view)
            .AddMenu("File", menu => menu
                .AddItem("Exit", () => invoked = true));

        menuBar.DropDownAnimation = MenuDropDownAnimation.None;
        menuBar.Show();
        menuBar.OpenMenuAt(0);

        menuBar.Menus[0].DropDown.Items[0].PerformClick();

        Assert.True(invoked);
        Assert.Equal(-1, menuBar.OpenMenuIndex);
        Assert.False(menuBar.Menus[0].DropDown.IsOpen);
    }

    [Fact]
    public void DisabledItem_DoesNotRunCommand()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host);
        bool invoked = false;

        using var menuBar = CreateMenuBar(host, view)
            .AddMenu("Edit", menu => menu
                .AddItem("Undo", () => invoked = true, enabled: false));

        menuBar.DropDownAnimation = MenuDropDownAnimation.None;
        menuBar.Show();
        menuBar.OpenMenuAt(0);

        menuBar.Menus[0].DropDown.Items[0].PerformClick();

        Assert.False(invoked);
        Assert.Equal(0, menuBar.OpenMenuIndex);
    }

    private static MenuBarWidget CreateMenuBar(TestRenderSurfaceHost host,
                                                View view)
    {
        return new MenuBarWidget(
            host,
            view,
            new Rectangle(
                view.Viewport.TargetRectPx.X,
                view.Viewport.TargetRectPx.Y,
                view.Viewport.TargetRectPx.Width,
                30));
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        var bounds = new Rectangle(0, 0, 640, 480);
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.ZOrder == 0 &&
            view.Viewport.TargetRectPx == bounds);
    }
}
