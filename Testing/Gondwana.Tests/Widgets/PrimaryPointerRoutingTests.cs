using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Tests.Widgets;

/// <summary>Tests widget-owned and unhandled gameplay primary input through actual pollers.</summary>
[Collection("Input poller singleton")]
public sealed class PrimaryPointerRoutingTests : IDisposable
{
    private readonly TestRenderSurfaceHost _host = new();
    private readonly TestTouchAdapter _touch = new();
    private readonly TestMouseAdapter _mouse = new();
    private readonly WidgetInputRouter _router;
    private readonly View _view;
    private readonly List<PrimaryPointerActionEventArgs> _actions = [];
    private long _tick = 1;

    /// <summary>Creates a render surface with simulated mouse and touch input.</summary>
    public PrimaryPointerRoutingTests()
    {
        _host.ViewManager.AddView(new Rectangle(0, 0, 640, 480));
        _view = _host.ViewManager.Views[0];

        TouchEventPoller.Initialize(_touch, new TouchEventConfiguration());
        MouseEventPoller.Initialize(_mouse);
        MouseEventPoller.Instance!.StartMonitoringMouse(timeBetweenEvents: 0);

        _router = new WidgetInputRouter(
            _host, null, MouseEventPoller.Instance, TouchEventPoller.Instance);
        _router.UnhandledPrimaryAction += _actions.Add;
        _router.Start();
    }

    /// <summary>Mouse left press and one-finger tap share a callback and preserve their origin.</summary>
    [Fact]
    public void MouseAndTapShareGameplayAction()
    {
        _mouse.CurrentPosition = new Point(100, 100);
        _mouse.PressedButtons.Add(MouseButton.Left);
        MouseEventPoller.Instance!.PollForEvents(_tick++);

        Tap(new Point(120, 130));

        Assert.Equal(2, _actions.Count);
        Assert.Same(_view, _actions[0].View);
        Assert.Equal(new Point(100, 100), _actions[0].ScreenPositionPx);
        Assert.False(_actions[0].IsTouch);
        Assert.Equal(WidgetInputRouter.MousePointerId, _actions[0].PointerId);
        Assert.Equal(new Point(120, 130), _actions[1].ScreenPositionPx);
        Assert.True(_actions[1].IsTouch);
        Assert.Equal(1, _actions[1].PointerId);
    }

    /// <summary>A visible button consumes both pointer sources before gameplay sees them.</summary>
    [Fact]
    public void WidgetConsumesMouseAndTouch()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));
        var downs = 0;
        button.PointerDown += _ => downs++;

        _mouse.CurrentPosition = new Point(30, 30);
        _mouse.PressedButtons.Add(MouseButton.Left);
        MouseEventPoller.Instance!.PollForEvents(_tick++);
        Tap(new Point(30, 30));

        Assert.Equal(2, downs);
        Assert.Empty(_actions);
    }

    /// <summary>A widget that hides on touch down retains ownership of the contact.</summary>
    [Fact]
    public void HidingWidgetDoesNotLeakTapToGameplay()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));
        button.PointerDown += _ => button.Hide();

        Tap(new Point(30, 30));

        Assert.False(button.Visible);
        Assert.Empty(_actions);
    }

    /// <summary>A newly visible widget may block a game touch on release.</summary>
    [Fact]
    public void WidgetAppearingDuringContactBlocksGameplay()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));
        button.Hide();

        _touch.Begin(1, new Point(30, 30));
        TouchEventPoller.Instance!.PollForEvents(_tick++);
        button.Show();
        _touch.End(1, new Point(30, 30));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Empty(_actions);
    }

    /// <summary>Cancelled contacts, drags, and simultaneous touches do not produce actions.</summary>
    [Fact]
    public void NonTapTouchGesturesDoNotProduceGameplayActions()
    {
        _touch.Begin(1, new Point(150, 150));
        _touch.End(1, new Point(150, 150), TouchPhase.Cancelled);
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        _touch.Begin(2, new Point(150, 150));
        _touch.End(2, new Point(200, 150));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        _touch.Begin(3, new Point(150, 150));
        _touch.Begin(4, new Point(160, 150));
        _touch.End(3, new Point(150, 150));
        _touch.End(4, new Point(160, 150));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Empty(_actions);
    }

    /// <summary>Reused touch IDs after poller reset cannot inherit gameplay ownership.</summary>
    [Fact]
    public void ReusedContactIdAfterTouchPauseRemainsWidgetOwned()
    {
        _touch.Begin(1, new Point(30, 30));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        TouchEventPoller.Instance.StopMonitoringTouch();
        _touch.End(1, new Point(30, 30));
        TouchEventPoller.Instance.PollForEvents(_tick++);
        TouchEventPoller.Instance.StartMonitoringTouch();

        using var button = Button(new Rectangle(10, 10, 120, 50));
        button.PointerDown += _ => button.Hide();

        Tap(new Point(30, 30));

        Assert.Empty(_actions);
    }

    /// <summary>Reused touch IDs after poller reset do not retain stale widget capture.</summary>
    [Fact]
    public void ReusedContactIdAfterTouchPauseAllowsGameplayTap()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));

        _touch.Begin(1, new Point(30, 30));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        TouchEventPoller.Instance.StopMonitoringTouch();
        _touch.End(1, new Point(30, 30));
        TouchEventPoller.Instance.PollForEvents(_tick++);
        TouchEventPoller.Instance.StartMonitoringTouch();

        Tap(new Point(200, 200), 1);

        Assert.Single(_actions);
        Assert.Same(_view, _actions[0].View);
        Assert.Equal(new Point(200, 200), _actions[0].ScreenPositionPx);
        Assert.True(_actions[0].IsTouch);
        Assert.Equal(1, _actions[0].PointerId);
    }

    /// <summary>A tap starting outside a widget but ending over it is blocked on release.</summary>
    [Fact]
    public void TapCrossingWidgetEdgeOnReleaseDoesNotProduceGameplayAction()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));

        _touch.Begin(1, new Point(5, 20));
        _touch.End(1, new Point(15, 20));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Empty(_actions);
    }

    /// <summary>A short qualifying tap remaining outside widgets produces a gameplay action.</summary>
    [Fact]
    public void TapMovingWithinThresholdOutsideWidgetsProducesGameplayAction()
    {
        using var button = Button(new Rectangle(10, 10, 120, 50));

        _touch.Begin(1, new Point(200, 200));
        _touch.End(1, new Point(205, 205));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Single(_actions);
        Assert.Same(_view, _actions[0].View);
        Assert.Equal(new Point(200, 200), _actions[0].ScreenPositionPx);
        Assert.True(_actions[0].IsTouch);
        Assert.Equal(1, _actions[0].PointerId);
    }

    /// <summary>Gameplay input is never delivered outside the host's viewports.</summary>
    [Fact]
    public void PressesOutsideViewAreIgnored()
    {
        _mouse.CurrentPosition = new Point(800, 800);
        _mouse.PressedButtons.Add(MouseButton.Left);
        MouseEventPoller.Instance!.PollForEvents(_tick++);
        Tap(new Point(800, 800));

        Assert.Empty(_actions);
    }

    /// <summary>Stopping the router unsubscribes input and clears prior touch eligibility.</summary>
    [Fact]
    public void StopAndRestartDoesNotLeakOrDuplicateInput()
    {
        _touch.Begin(1, new Point(100, 100));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        _router.Stop();
        _touch.End(1, new Point(100, 100));
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        _router.Start();
        Tap(new Point(150, 150), 2);

        Assert.Single(_actions);
        Assert.Equal(new Point(150, 150), _actions[0].ScreenPositionPx);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _router.Dispose();
        TouchEventPoller.Reset();
        MouseEventPoller.Reset();
        _host.Dispose();
    }

    private ButtonWidget Button(Rectangle bounds)
    {
        var button = new ButtonWidget(_host, _view, bounds, "Action");
        button.Show();
        return button;
    }

    private void Tap(Point point, int id = 1)
    {
        _touch.Begin(id, point);
        _touch.End(id, point);
        TouchEventPoller.Instance!.PollForEvents(_tick++);
    }

    private sealed class TestMouseAdapter : IMouseAdapter
    {
        /// <inheritdoc/>
        public Point CurrentPosition { get; set; }
        /// <inheritdoc/>
        public HashSet<MouseButton> PressedButtons { get; } = [];
        /// <inheritdoc/>
        public KeyboardModifierState CurrentKeyboardModifiers => KeyboardModifierState.None;
        /// <inheritdoc/>
        public int ScrollDelta => 0;
    }

    private sealed class TestTouchAdapter : ITouchAdapter
    {
        private readonly Dictionary<int, TouchPoint> _active = [];
        private readonly Queue<TouchPoint> _began = [];
        private readonly Queue<TouchPoint> _ended = [];

        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ActiveTouches => _active.Values.ToArray();
        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ConsumeBeganTouches() => Drain(_began);
        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ConsumeEndedTouches() => Drain(_ended);

        internal void Begin(int id, Point pos)
        {
            var touch = new TouchPoint(id, pos, TouchPhase.Began);
            _active[id] = touch;
            _began.Enqueue(touch);
        }

        internal void End(int id, Point pos, TouchPhase phase = TouchPhase.Ended)
        {
            _active.Remove(id);
            _ended.Enqueue(new TouchPoint(id, pos, phase));
        }

        private static TouchPoint[] Drain(Queue<TouchPoint> queue)
        {
            var drained = queue.ToArray();
            queue.Clear();
            return drained;
        }
    }
}
