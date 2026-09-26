using System.Drawing;
using System.Numerics;
using System.Reflection;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Tests.Widgets;

public sealed class ListBoxScrollingTests
{
    [Fact]
    public void VisibilityAndContentWidthFollowPolicyAndItemChanges()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var list = CreateList(host, view, 3);
        Assert.Equal(4, list.VisibleItemCount);
        Assert.False(list.IsVerticalScrollBarVisible);
        int fullWidth = list.Children.OfType<TextBlock>().First().ScreenBounds.Width;
        list.AddItem("four").AddItem("five").AddItem("six");
        Assert.True(list.IsVerticalScrollBarVisible);
        Assert.True(list.Children.OfType<TextBlock>().First().ScreenBounds.Width < fullWidth);
        list.SelectedIndex = 0;
        Assert.True(list.SelectionHighlight.ScreenBounds.Right < list.VerticalScrollBarTrack.ScreenBounds.Left);
        list.RemoveAt(5).RemoveAt(4);
        Assert.False(list.IsVerticalScrollBarVisible);
        Assert.Equal(fullWidth, list.Children.OfType<TextBlock>().First().ScreenBounds.Width);
        list.VerticalScrollBarVisibility = ScrollBarVisibility.Always;
        Assert.True(list.IsVerticalScrollBarVisible);
        Assert.Equal(list.VerticalScrollBarTrack.ScreenBounds, list.VerticalScrollBarThumb.ScreenBounds);
        list.SetItems(Items(20));
        list.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
        Assert.False(list.IsVerticalScrollBarVisible);
        list.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        list.TopIndex = 10;
        list.ClearItems();
        Assert.False(list.IsVerticalScrollBarVisible);
        Assert.Equal(0, list.TopIndex);
    }

    [Theory]
    [InlineData(ScrollBarVisibility.Auto, 3, false)]
    [InlineData(ScrollBarVisibility.Auto, 20, true)]
    [InlineData(ScrollBarVisibility.Always, 0, true)]
    [InlineData(ScrollBarVisibility.Never, 20, false)]
    public void HideShowRestoresOnlyRequiredVisuals(ScrollBarVisibility policy, int count, bool expected)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var list = CreateList(host, view, count);
        list.VerticalScrollBarVisibility = policy;
        list.Hide();
        list.SetItems(Items(count));
        Assert.False(list.Visible);
        Assert.False(list.IsVerticalScrollBarVisible);
        list.Show();
        Assert.Equal(expected, list.IsVerticalScrollBarVisible);
        Assert.Equal(expected, list.VerticalScrollBarTrack.Visible);
        Assert.Equal(expected, list.VerticalScrollBarThumb.Visible);
        Assert.False(list.SelectionHighlight.Visible);
    }

    [Fact]
    public void ThumbSizePositionAndZOrderFollowListStateAndMovement()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var list = CreateList(host, view, 8);
        int initialHeight = list.VerticalScrollBarThumb.ScreenBounds.Height;
        list.SetItems(Items(20));
        Assert.True(list.VerticalScrollBarThumb.ScreenBounds.Height < initialHeight);
        list.SetItems(Items(1000));
        Assert.Equal(list.VerticalScrollBarMinimumThumbHeight, list.VerticalScrollBarThumb.ScreenBounds.Height);
        list.SetItems(Items(20));
        var track = list.VerticalScrollBarTrack.ScreenBounds;
        Assert.Equal(track.Top, list.VerticalScrollBarThumb.ScreenBounds.Top);
        list.TopIndex = 8;
        Assert.InRange(list.VerticalScrollBarThumb.ScreenBounds.Top, track.Top + 1, track.Bottom - 1);
        list.TopIndex = int.MaxValue;
        Assert.Equal(track.Bottom, list.VerticalScrollBarThumb.ScreenBounds.Bottom);
        var thumb = list.VerticalScrollBarThumb.ScreenBounds;
        list.SetPosition(list.GetPosition() + new Vector2(30, 40));
        thumb.Offset(30, 40);
        Assert.Equal(thumb, list.VerticalScrollBarThumb.ScreenBounds);
        list.SetListBoxZOrder(100);
        Assert.Equal(103, list.VerticalScrollBarTrack.ZOrder);
        Assert.Equal(104, list.VerticalScrollBarThumb.ZOrder);
        list.ItemHeight = 12;
        Assert.Equal(8, list.VisibleItemCount);
        Assert.Equal(12, list.TopIndex);
        Assert.Equal(list.VerticalScrollBarTrack.ScreenBounds.Bottom, list.VerticalScrollBarThumb.ScreenBounds.Bottom);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(202)]
    public void RoutedWheelOverContentTrackOrThumbClampsWithoutChangingSelection(int x)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        using var list = CreateList(host, view, 20);
        list.Activate();
        list.SelectedIndex = 0;
        Mouse(router, new Point(x, 30), delta: -120);
        Assert.Equal(3, list.TopIndex);
        Assert.Equal(0, list.SelectedIndex);
        Assert.False(list.SelectionHighlight.Visible);
        Mouse(router, new Point(x, 80), delta: -120);
        Assert.Equal(6, list.TopIndex);
        Mouse(router, new Point(x, 30), delta: 120);
        Assert.Equal(3, list.TopIndex);
        Mouse(router, new Point(x, 30), delta: int.MaxValue);
        Assert.Equal(0, list.TopIndex);
        Assert.True(list.SelectionHighlight.Visible);
        list.MouseWheelScrollItems = int.MaxValue;
        Mouse(router, new Point(x, 30), delta: int.MinValue);
        Assert.Equal(16, list.TopIndex);
        Assert.Equal(0, list.SelectedIndex);
        list.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
        Mouse(router, new Point(x, 30), delta: 120);
        Assert.Equal(0, list.TopIndex);
    }

    [Fact]
    public void WheelUsesTopmostEligibleWidgetAndDoesNotStealFocus()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        using var lower = CreateList(host, view, 20);
        using var upper = CreateList(host, view, 20);
        lower.Activate();
        upper.Activate();
        router.Focus(lower);
        Mouse(router, new Point(30, 30), delta: -120);
        Assert.Equal(3, upper.TopIndex);
        Assert.Equal(0, lower.TopIndex);
        Assert.Same(lower, router.FocusedWidget);
        upper.IsPointerInputEnabled = false;
        Mouse(router, new Point(30, 30), delta: -120);
        Assert.Equal(3, lower.TopIndex);
        lower.Hide();
        Mouse(router, new Point(30, 30), delta: -120);
        Assert.Equal(3, lower.TopIndex);
        Assert.Equal(3, upper.TopIndex);
    }

    [Fact]
    public void TrackPagesAndCapturedDragReachesBothEndsWithoutCommitting()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        using var list = CreateList(host, view, 20);
        list.Activate();
        int commits = 0;
        list.SelectionCommitted += _ => commits++;
        var track = list.VerticalScrollBarTrack.ScreenBounds;
        var below = new Point(track.Left + 3, track.Bottom - 2);
        Click(router, below);
        Assert.Equal(4, list.TopIndex);
        Click(router, new Point(track.Left + 3, track.Top + 1));
        Assert.Equal(0, list.TopIndex);
        for (int i = 0; i < 8; i++)
            Click(router, below);
        Assert.Equal(16, list.TopIndex);
        var thumb = list.VerticalScrollBarThumb.ScreenBounds;
        var grab = new Point(thumb.Left + 3, thumb.Top + 3);
        Mouse(router, grab, down: true);
        Mouse(router, new Point(grab.X, -100), down: true, previous: grab);
        Assert.Equal(0, list.TopIndex);
        Mouse(router, new Point(grab.X, 300), down: true, previous: new Point(grab.X, -100));
        Assert.Equal(16, list.TopIndex);
        // Release over a row: the scrollbar gesture must not select or commit it.
        Mouse(router, new Point(30, 40), release: true);
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal(0, commits);
        Click(router, new Point(30, 40));
        Assert.Equal(1, commits);
    }

    [Fact]
    public void KeyboardScrollsSelectionBackIntoViewAfterWheelOrDirectScrolling()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var list = CreateList(host, view, 20);
        Key(list, 35);
        Assert.Equal(19, list.SelectedIndex);
        Assert.Equal(16, list.TopIndex);
        Assert.Equal(list.VerticalScrollBarTrack.ScreenBounds.Bottom, list.VerticalScrollBarThumb.ScreenBounds.Bottom);
        list.TopIndex = 0;
        Key(list, 35); // End with the same selection still restores visibility.
        Assert.Equal(16, list.TopIndex);
        Key(list, 33);
        Assert.Equal(15, list.SelectedIndex);
        Key(list, 38);
        Assert.Equal(14, list.SelectedIndex);
        Key(list, 40);
        Assert.Equal(15, list.SelectedIndex);
        Key(list, 36);
        Assert.Equal(0, list.TopIndex);
        Key(list, 34);
        Assert.Equal(4, list.SelectedIndex);
        Assert.Equal(1, list.TopIndex);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(20, true)]
    public void ComboBoxInheritsScrollingAndPreservesCommitAndFocusBehavior(int count, bool scrollbar)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        using var combo = new ComboBoxWidget(host, view, new Rectangle(10, 20, 200, 32), Items(count), 100);
        using var other = new ButtonWidget(host, view, new Rectangle(300, 20, 100, 32), "Other");
        other.Activate();
        combo.OpenDropDown();
        Assert.Same(combo.DropDown, router.FocusedWidget);
        Assert.Equal(scrollbar, combo.DropDown.IsVerticalScrollBarVisible);
        Assert.Equal(scrollbar, combo.DropDown.VerticalScrollBarThumb.Visible);
        Mouse(router, new Point(30, 70), delta: -120);
        Assert.Equal(scrollbar ? 3 : 0, combo.DropDown.TopIndex);
        Assert.True(combo.IsDropDownOpen);
        Key(combo.DropDown, 40);
        Key(combo.DropDown, 13);
        Assert.False(combo.IsDropDownOpen);
        combo.OpenDropDown();
        router.Focus(other);
        Assert.False(combo.IsDropDownOpen);
        Assert.Same(other, router.FocusedWidget);
        combo.OpenDropDown();
        Click(router, new Point(30, 30));
        Assert.False(combo.IsDropDownOpen);
    }

    [Fact]
    public void HiddenOrDisabledListReleasesCapturedThumb()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        using var list = CreateList(host, view, 20);
        list.Activate();
        var thumb = list.VerticalScrollBarThumb.ScreenBounds;
        var grab = new Point(thumb.Left + 3, thumb.Top + 3);
        Mouse(router, grab, down: true);
        list.Hide().Show();
        Mouse(router, new Point(grab.X, 100), down: true, previous: grab);
        Assert.Equal(0, list.TopIndex);
        Mouse(router, grab, release: true);
        Mouse(router, grab, down: true);
        list.IsPointerInputEnabled = false;
        list.IsPointerInputEnabled = true;
        Mouse(router, new Point(grab.X, 100), previous: grab);
        Assert.Equal(0, list.TopIndex);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    public void SceneLayerThumbDragUsesViewScaleAndWrappedInstanceOffset(float zoom)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        view.Viewport.Zoom = zoom;
        var layer = host.Scene.AddLayer(20, 20, width: 32, height: 32);
        using var list = new ListBoxWidget(host, layer, new Rectangle(10, 20, 200, 100), Items(20));
        var screenThumb = list.VerticalScrollBarThumb.GetDrawLocationScreen(view);
        var screenTrack = list.VerticalScrollBarTrack.GetDrawLocationScreen(view);
        var instanceOffset = new PointF(640, 0);
        var grab = new PointF(screenThumb.Left + 3 * zoom + instanceOffset.X * zoom, screenThumb.Top + 3 * zoom);
        void Pointer(string method, PointF position, int id = 7)
        {
            var args = new WidgetPointerEventArgs(list, view, position, WidgetPointerButtonEnum.Touch, pointerId: id);
            typeof(WidgetPointerEventArgs).GetProperty(nameof(WidgetPointerEventArgs.WrappedOffsetWorldPx))!
                .SetValue(args, instanceOffset);
            typeof(WidgetBase).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(list, [args]);
        }
        Pointer("DispatchPointerDown", grab);
        Pointer("DispatchPointerMove", new PointF(grab.X, screenTrack.Bottom + 10), id: 8);
        Assert.Equal(0, list.TopIndex); // A second touch cannot move the captured thumb.
        Pointer("DispatchPointerMove", new PointF(grab.X, screenTrack.Bottom + 10));
        Assert.Equal(16, list.TopIndex);
        Pointer("DispatchPointerMove", new PointF(grab.X, screenTrack.Top - 10));
        Assert.Equal(0, list.TopIndex);
        Pointer("DispatchPointerUp", grab);
    }

    [Fact]
    public void OversizedRowsRemainInsideListBounds()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var list = CreateList(host, view, 20);
        list.ItemHeight = 200;
        list.SelectedIndex = 0;
        Assert.True(list.Bounds.Contains(list.SelectionHighlight.ScreenBounds));
        Assert.All(list.Children.OfType<TextBlock>(), row =>
        {
            Assert.True(list.Bounds.Contains(row.ScreenBounds));
            Assert.True(row.ScreenBounds.Right < list.VerticalScrollBarTrack.ScreenBounds.Left);
        });
    }

    private static IEnumerable<string> Items(int count) => Enumerable.Range(0, count).Select(i => $"Item {i}");

    private static ListBoxWidget CreateList(TestRenderSurfaceHost host, View view, int count)
        => new(host, view, new Rectangle(10, 20, 200, 100), Items(count));

    private static View AddView(TestRenderSurfaceHost host)
    {
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480), zOrder: 10);
        return host.ViewManager.Views.Single(view => view.ZOrder == 10);
    }

    private static void Key(WidgetBase widget, int key)
        => typeof(WidgetBase).GetMethod("DispatchKeyboardInput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(widget, [new WidgetKeyboardEventArgs(widget, key, KeyAction.Pressed, KeyboardModifierState.None)]);

    private static void Click(WidgetInputRouter router, Point position)
    {
        Mouse(router, position, down: true);
        Mouse(router, position, release: true);
    }

    private static void Mouse(WidgetInputRouter router, Point position, int delta = 0,
                              bool down = false, bool release = false, Point? previous = null)
    {
        var buttons = new Dictionary<MouseButton, MouseButtonState>
        {
            [MouseButton.Left] = new() { IsDown = down, JustPressed = down, JustReleased = release }
        };
        var args = new MouseEventArgs(new MouseEventConfiguration(true), KeyboardModifierState.None,
                                      buttons, previous ?? position, position, delta, 1);
        typeof(WidgetInputRouter).GetMethod("OnMouseEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(router, [args]);
    }
}
