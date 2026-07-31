using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Timers;

namespace Gondwana.Tests.Input;

[Collection(InputPollerCollection.Name)]
public sealed class MouseInputTests : IDisposable
{
    [Fact]
    public void Poller_DoesNotEmitPhantomZeroScrollAfterRealScroll()
    {
        var adapter = new FakeMouseAdapter();
        MouseEventPoller.Initialize(
            adapter,
            new MouseEventConfiguration(trackMouseMovement: false));
        var deltas = new List<int>();
        MouseEventPoller.Instance!.MouseEvent += e => deltas.Add(e.ScrollDelta);

        adapter.ScrollDelta = 120;
        MouseEventPoller.Instance.PollForEvents(100);
        adapter.ScrollDelta = 0;
        MouseEventPoller.Instance.PollForEvents(101);

        Assert.Equal([120], deltas);
    }

    [Fact]
    public void Poller_AdvancesThrottleTimestampAfterEvent()
    {
        var adapter = new FakeMouseAdapter();
        MouseEventPoller.Initialize(
            adapter,
            new MouseEventConfiguration(
                trackMouseMovement: true,
                secondsBetweenEvents: 1));
        var events = 0;
        MouseEventPoller.Instance!.MouseEvent += _ => events++;
        var start = HighResTimer.TicksPerSecond;

        adapter.CurrentPosition = new Point(1, 0);
        MouseEventPoller.Instance.PollForEvents(start);
        adapter.CurrentPosition = new Point(2, 0);
        MouseEventPoller.Instance.PollForEvents(start + 1);

        Assert.Equal(1, events);
    }

    [Fact]
    public void Reset_DisposesAdapterAndClearsSingleton()
    {
        var adapter = new FakeMouseAdapter();
        MouseEventPoller.Initialize(
            adapter,
            new MouseEventConfiguration(trackMouseMovement: true));

        MouseEventPoller.Reset();

        Assert.True(adapter.IsDisposed);
        Assert.Null(MouseEventPoller.Instance);
    }

    public void Dispose() => MouseEventPoller.Reset();

    private sealed class FakeMouseAdapter : IMouseAdapter, IDisposable
    {
        public Point CurrentPosition { get; set; }
        public HashSet<MouseButton> PressedButtons { get; } = [];
        public KeyboardModifierState CurrentKeyboardModifiers => KeyboardModifierState.None;
        public int ScrollDelta { get; set; }
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }
}
