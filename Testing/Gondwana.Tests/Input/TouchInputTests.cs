using System.Drawing;
using Gondwana.Input.Touch;
using Gondwana.Input.Touch.Gestures;

namespace Gondwana.Tests.Input;

[CollectionDefinition("Input poller singleton", DisableParallelization = true)]
public sealed class InputPollerCollection
{
    public const string Name = "Input poller singleton";
}

[Collection(InputPollerCollection.Name)]
public sealed class TouchInputTests : IDisposable
{
    [Fact]
    public void Poller_PreservesContactThatBeginsAndEndsBetweenPolls()
    {
        var adapter = new FakeTouchAdapter();
        TouchEventPoller.Initialize(adapter, new TouchEventConfiguration());
        var poller = TouchEventPoller.Instance!;
        var phases = new List<TouchPhase>();
        poller.TouchBegan += (_, e) => phases.Add(e.Touch.Phase);
        poller.TouchEnded += (_, e) => phases.Add(e.Touch.Phase);

        adapter.Begin(1, new Point(10, 20));
        adapter.End(1, new Point(10, 20));
        poller.PollForEvents(100);

        Assert.Equal([TouchPhase.Began, TouchPhase.Ended], phases);
        Assert.Empty(poller.ActiveTouches);
    }

    [Fact]
    public void Poller_ThrottlesOnlyMovement()
    {
        var adapter = new FakeTouchAdapter();
        TouchEventPoller.Initialize(adapter, new TouchEventConfiguration(secondsBetweenEvents: 1));
        var poller = TouchEventPoller.Instance!;
        var began = 0;
        var moved = 0;
        var ended = 0;
        poller.TouchBegan += (_, _) => began++;
        poller.TouchMoved += (_, _) => moved++;
        poller.TouchEnded += (_, _) => ended++;

        adapter.Begin(1, Point.Empty);
        poller.PollForEvents(1);
        adapter.Move(1, new Point(20, 0));
        poller.PollForEvents(2);
        adapter.End(1, new Point(20, 0));
        poller.PollForEvents(3);

        Assert.Equal(1, began);
        Assert.Equal(0, moved);
        Assert.Equal(1, ended);
    }

    [Fact]
    public void Poller_PauseSuppressesEventsAndResumeStartsFreshContact()
    {
        var adapter = new FakeTouchAdapter();
        var config = new TouchEventConfiguration(isPaused: true);
        TouchEventPoller.Initialize(adapter, config);
        var poller = TouchEventPoller.Instance!;
        var began = 0;
        poller.TouchBegan += (_, _) => began++;

        adapter.Begin(1, Point.Empty);
        poller.PollForEvents(1);
        Assert.Equal(0, began);

        config.IsPaused = false;
        poller.PollForEvents(2);

        Assert.Equal(1, began);
    }

    [Fact]
    public void Poller_NormalizesDiscoveredContactToBeganPhase()
    {
        var adapter = new SnapshotOnlyTouchAdapter(
            new TouchPoint(7, new Point(5, 6), TouchPhase.Moved));
        TouchEventPoller.Initialize(adapter, new TouchEventConfiguration());
        TouchPhase? phase = null;
        TouchEventPoller.Instance!.TouchBegan += (_, e) => phase = e.Touch.Phase;

        TouchEventPoller.Instance.PollForEvents(1);

        Assert.Equal(TouchPhase.Began, phase);
    }

    [Fact]
    public void ShortFastContact_IsTapButNotSwipe()
    {
        var input = new FakeTouchInput();
        using var tap = new TapGestureRecognizer(input);
        using var swipe = new SwipeGestureRecognizer(input);
        var taps = 0;
        var swipes = 0;
        tap.Tapped += (_, _) => taps++;
        swipe.Swiped += (_, _) => swipes++;
        var start = 100L;
        var end = start + Math.Max(1, HighResTimer.TicksPerSecond / 100);

        input.Begin(1, Point.Empty, start);
        input.End(1, new Point(10, 0), end);

        Assert.Equal(1, taps);
        Assert.Equal(0, swipes);
    }

    [Fact]
    public void Poller_ArbitratesOverlappingTapAndSwipeThresholdsInFavorOfSwipe()
    {
        var adapter = new FakeTouchAdapter();
        TouchEventPoller.Initialize(adapter, new TouchEventConfiguration());
        var poller = TouchEventPoller.Instance!;
        poller.TapRecognizer.MaxTapMovementPixels = 40;
        poller.SwipeRecognizer.MinimumSwipeDistancePixels = 10;
        poller.SwipeRecognizer.MinimumSwipeSpeedPixelsPerSecond = 100;
        var taps = 0;
        var swipes = 0;
        poller.TapRecognizer.Tapped += (_, _) => taps++;
        poller.SwipeRecognizer.Swiped += (_, _) => swipes++;
        var start = HighResTimer.TicksPerSecond;

        adapter.Begin(1, Point.Empty);
        poller.PollForEvents(start);
        adapter.End(1, new Point(20, 0));
        poller.PollForEvents(start + HighResTimer.TicksPerSecond / 10);

        Assert.Equal(0, taps);
        Assert.Equal(1, swipes);
    }

    [Fact]
    public void Tap_UsesFinalPositionEvenWithoutMovementEvent()
    {
        var input = new FakeTouchInput();
        using var tap = new TapGestureRecognizer(input);
        var taps = 0;
        tap.Tapped += (_, _) => taps++;

        input.Begin(1, Point.Empty, 100);
        input.End(1, new Point(100, 0), 101);

        Assert.Equal(0, taps);
    }

    [Fact]
    public void MultiTouch_CancelsTapAndSwipeCandidates()
    {
        var input = new FakeTouchInput();
        using var tap = new TapGestureRecognizer(input);
        using var swipe = new SwipeGestureRecognizer(input);
        var gestures = 0;
        tap.Tapped += (_, _) => gestures++;
        swipe.Swiped += (_, _) => gestures++;

        input.Begin(1, Point.Empty, 100);
        input.Begin(2, new Point(10, 0), 101);
        input.End(1, new Point(100, 0), 102);
        input.End(2, new Point(110, 0), 103);

        Assert.Equal(0, gestures);
    }

    [Fact]
    public void Pinch_ReportsLifecycleCenterIdsAndScale()
    {
        var input = new FakeTouchInput();
        using var pinch = new PinchGestureRecognizer(input);
        var events = new List<PinchedEventArgs>();
        pinch.PinchStarted += (_, e) => events.Add(e);
        pinch.PinchUpdated += (_, e) => events.Add(e);
        pinch.PinchEnded += (_, e) => events.Add(e);

        input.Begin(2, new Point(0, 0), 100);
        input.Begin(5, new Point(10, 0), 101);
        input.Move(5, new Point(20, 0), 102);
        input.End(5, new Point(20, 0), 103);

        Assert.Equal(
            [PinchPhase.Began, PinchPhase.Updated, PinchPhase.Ended],
            events.Select(e => e.Phase));
        Assert.Equal([2, 5], events[1].TouchIds);
        Assert.Equal(new PointF(5, 0), events[0].Center);
        Assert.Equal(new PointF(10, 0), events[1].Center);
        Assert.Equal(2.0, events[1].ScaleDelta, 6);
        Assert.Equal(2.0, events[1].TotalScale, 6);
    }

    [Fact]
    public void Reset_DisposesAdapterAndClearsSingleton()
    {
        var adapter = new FakeTouchAdapter();
        TouchEventPoller.Initialize(adapter, new TouchEventConfiguration());

        TouchEventPoller.Reset();

        Assert.True(adapter.IsDisposed);
        Assert.Null(TouchEventPoller.Instance);
    }

    public void Dispose() => TouchEventPoller.Reset();

    private sealed class FakeTouchAdapter : ITouchAdapter, IDisposable
    {
        private readonly Dictionary<int, TouchPoint> _active = new();
        private readonly Queue<TouchPoint> _began = new();
        private readonly Queue<TouchPoint> _ended = new();

        public IReadOnlyList<TouchPoint> ActiveTouches => _active.Values.ToArray();
        public bool IsDisposed { get; private set; }

        public void Begin(int id, Point position)
        {
            var point = new TouchPoint(id, position, TouchPhase.Began);
            _active[id] = point;
            _began.Enqueue(point);
        }

        public void Move(int id, Point position)
            => _active[id] = new TouchPoint(id, position, TouchPhase.Moved);

        public void End(int id, Point position)
        {
            _active.Remove(id);
            _ended.Enqueue(new TouchPoint(id, position, TouchPhase.Ended));
        }

        public IReadOnlyList<TouchPoint> ConsumeBeganTouches() => Drain(_began);
        public IReadOnlyList<TouchPoint> ConsumeEndedTouches() => Drain(_ended);
        public void Dispose() => IsDisposed = true;

        private static IReadOnlyList<TouchPoint> Drain(Queue<TouchPoint> queue)
        {
            var result = queue.ToArray();
            queue.Clear();
            return result;
        }
    }

    private sealed class SnapshotOnlyTouchAdapter(params TouchPoint[] points) : ITouchAdapter
    {
        public IReadOnlyList<TouchPoint> ActiveTouches { get; } = points;
        public IReadOnlyList<TouchPoint> ConsumeEndedTouches() => Array.Empty<TouchPoint>();
    }

    private sealed class FakeTouchInput : ITouchInput
    {
        private readonly Dictionary<int, TouchPoint> _active = new();
        public IReadOnlyList<TouchPoint> ActiveTouches => _active.Values.ToArray();
        public event EventHandler<TouchEventArgs>? TouchBegan;
        public event EventHandler<TouchEventArgs>? TouchMoved;
        public event EventHandler<TouchEventArgs>? TouchEnded;

        public void Begin(int id, Point position, long tick)
        {
            var point = new TouchPoint(id, position, TouchPhase.Began);
            _active[id] = point;
            TouchBegan?.Invoke(this, new TouchEventArgs(point, tick));
        }

        public void Move(int id, Point position, long tick)
        {
            var point = new TouchPoint(id, position, TouchPhase.Moved);
            _active[id] = point;
            TouchMoved?.Invoke(this, new TouchEventArgs(point, tick));
        }

        public void End(int id, Point position, long tick)
        {
            _active.Remove(id);
            TouchEnded?.Invoke(
                this,
                new TouchEventArgs(new TouchPoint(id, position, TouchPhase.Ended), tick));
        }
    }
}
