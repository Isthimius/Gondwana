using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Timers;

namespace Gondwana.Tests.Input;

/// <summary>
/// Contains regression tests for mouse input.
/// </summary>
[Collection(InputPollerCollection.Name)]
public sealed class MouseInputTests : IDisposable
{
    /// <summary>
    /// Verifies poller does not emit phantom zero scroll after real scroll.
    /// </summary>
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

    /// <summary>
    /// Verifies poller emits equal wheel deltas on consecutive polls.
    /// </summary>
    [Fact]
    public void Poller_EmitsEqualWheelDeltasOnConsecutivePolls()
    {
        var adapter = new FakeMouseAdapter();
        MouseEventPoller.Initialize(adapter, new MouseEventConfiguration(trackMouseMovement: false, secondsBetweenEvents: 0));
        var deltas = new List<int>();
        MouseEventPoller.Instance!.MouseEvent += e => deltas.Add(e.ScrollDelta);

        adapter.ScrollDelta = -120;
        MouseEventPoller.Instance.PollForEvents(100);
        adapter.ScrollDelta = -120;
        MouseEventPoller.Instance.PollForEvents(101);

        Assert.Equal([-120, -120], deltas);
    }

    /// <summary>
    /// Verifies poller advances throttle timestamp after event.
    /// </summary>
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

    /// <summary>
    /// Verifies reset disposes adapter and clears singleton.
    /// </summary>
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

    /// <inheritdoc/>
    public void Dispose() => MouseEventPoller.Reset();

    private sealed class FakeMouseAdapter : IMouseAdapter, IDisposable
    {
        private int _scrollDelta;
        /// <inheritdoc/>
        public Point CurrentPosition { get; set; }
        /// <inheritdoc/>
        public HashSet<MouseButton> PressedButtons { get; } = [];
        /// <inheritdoc/>
        public KeyboardModifierState CurrentKeyboardModifiers => KeyboardModifierState.None;
        /// <inheritdoc/>
        public int ScrollDelta
        {
            get => Interlocked.Exchange(ref _scrollDelta, 0);
            set => _scrollDelta = value;
        }
        /// <summary>
        /// Gets whether the object is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }
}
