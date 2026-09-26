using System.Drawing;
using System.Reflection;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Tests.Widgets;

public sealed class LabelWidgetScrollingTests
{
    private const string LongText = """
        First line of scrollable instructions.
        Second line of scrollable instructions.
        Third line of scrollable instructions.
        Fourth line of scrollable instructions.
        Fifth line of scrollable instructions.
        Sixth line of scrollable instructions.
        Seventh line of scrollable instructions.
        Eighth line of scrollable instructions.
        Ninth line of scrollable instructions.
        Tenth line of scrollable instructions.
        """;

    [Fact]
    public void DefaultPolicyPreservesTraditionalNonInteractiveLabel()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var label = CreateLabel(host, view, LongText);

        Assert.Equal(ScrollBarVisibility.Never, label.VerticalScrollBarVisibility);
        Assert.False(label.IsVerticalScrollBarVisible);
        Assert.False(label.IsInputEnabled);
        Assert.False(label.IsPointerInputEnabled);
        Assert.Equal(0f, label.VerticalScrollOffsetPx);
    }

    [Fact]
    public void AutoScrollbarTracksOverflowAndReservesContentWidth()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var label = CreateLabel(host, view, LongText);

        int fullWidth = label.TextBlock.ScreenBounds.Width;

        label.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        Assert.True(label.IsVerticalScrollBarVisible);
        Assert.True(label.TextBlock.ScreenBounds.Width < fullWidth);
        Assert.True(label.MaximumVerticalScrollOffsetPx > 0f);
        Assert.True(label.VerticalScrollBarThumb.ScreenBounds.Height < label.VerticalScrollBarTrack.ScreenBounds.Height);

        label.VerticalScrollOffsetPx = float.MaxValue;

        Assert.Equal(label.MaximumVerticalScrollOffsetPx, label.VerticalScrollOffsetPx);
        Assert.Equal(
            label.VerticalScrollBarTrack.ScreenBounds.Bottom,
            label.VerticalScrollBarThumb.ScreenBounds.Bottom);

        label.SetText("Short text.");

        Assert.False(label.IsVerticalScrollBarVisible);
        Assert.Equal(fullWidth, label.TextBlock.ScreenBounds.Width);
        Assert.Equal(0f, label.VerticalScrollOffsetPx);
    }

    [Fact]
    public void MouseWheelScrollsAndClampsWithoutTakingFocus()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();

        using var label = CreateLabel(host, view, LongText);
        using var button = new ButtonWidget(host, view, new Rectangle(300, 20, 100, 32), "Other");

        label.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        label.Activate();
        button.Activate();
        router.Focus(button);

        Mouse(router, new Point(30, 30), delta: -120);

        Assert.Equal(label.MouseWheelScrollPixels, label.VerticalScrollOffsetPx);
        Assert.Same(button, router.FocusedWidget);

        Mouse(router, new Point(30, 30), delta: int.MinValue);
        Assert.Equal(label.MaximumVerticalScrollOffsetPx, label.VerticalScrollOffsetPx);

        Mouse(router, new Point(30, 30), delta: int.MaxValue);
        Assert.Equal(0f, label.VerticalScrollOffsetPx);
    }

    [Fact]
    public void TrackClickAndThumbDragMoveThroughScrollableRange()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();

        using var label = CreateLabel(host, view, LongText);
        label.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        label.Activate();

        Rectangle track = label.VerticalScrollBarTrack.ScreenBounds;
        Click(router, new Point(track.Left + 3, track.Bottom - 2));

        Assert.True(label.VerticalScrollOffsetPx > 0f);

        Rectangle thumb = label.VerticalScrollBarThumb.ScreenBounds;
        Point grab = new(thumb.Left + 3, thumb.Top + 3);
        Mouse(router, grab, down: true);
        Mouse(router, new Point(grab.X, track.Bottom + 100), down: true, previous: grab);

        Assert.Equal(label.MaximumVerticalScrollOffsetPx, label.VerticalScrollOffsetPx);

        Mouse(router, new Point(grab.X, track.Top - 100), down: true, previous: new Point(grab.X, track.Bottom + 100));

        Assert.Equal(0f, label.VerticalScrollOffsetPx);

        Mouse(router, grab, release: true);
    }

    private static LabelWidget CreateLabel(TestRenderSurfaceHost host, View view, string text)
        => new LabelWidget(host, view, new Rectangle(10, 20, 220, 90), text)
            .SetFont(SkiaSharp.SKTypeface.Default, 16f)
            .SetPadding(6f, 4f)
            .EnableWrapping();

    private static View AddView(TestRenderSurfaceHost host)
    {
        host.ViewManager.AddView(new Rectangle(0, 0, 640, 480), zOrder: 10);
        return host.ViewManager.Views.Single(view => view.ZOrder == 10);
    }

    private static void Click(WidgetInputRouter router, Point position)
    {
        Mouse(router, position, down: true);
        Mouse(router, position, release: true);
    }

    private static void Mouse(
        WidgetInputRouter router,
        Point position,
        int delta = 0,
        bool down = false,
        bool release = false,
        Point? previous = null)
    {
        var buttons = new Dictionary<MouseButton, MouseButtonState>
        {
            [MouseButton.Left] = new()
            {
                IsDown = down,
                JustPressed = down && !previous.HasValue,
                JustReleased = release
            }
        };

        var args = new MouseEventArgs(
            new MouseEventConfiguration(true),
            KeyboardModifierState.None,
            buttons,
            previous ?? position,
            position,
            delta,
            1);

        typeof(WidgetInputRouter)
            .GetMethod("OnMouseEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(router, [args]);
    }
}
