using System.Drawing;
using Gondwana.Input.Touch;
using Gondwana.Tests.Input;
using Gondwana.Timers;
using Gondwana.WinForms.Input.Touch;

namespace Gondwana.Tests.WinFormsInput;

/// <summary>
/// Tests platform-neutral touch transition buffering used by the Windows
/// adapter, without requiring a touchscreen or an HWND on Linux CI.
/// </summary>
[Collection(InputPollerCollection.Name)]
public sealed class WinFormsTouchStateTests : IDisposable
{
    [Fact]
    public void FastTapStillProvidesBothEdgesBetweenPolls()
    {
        var state = new WinFormsTouchState();
        state.Begin(7, new Point(24, 32));
        state.End(7, new Point(24, 32));

        Assert.Empty(state.ActiveTouches);
        Assert.Equal(new TouchPoint(7, new Point(24, 32), TouchPhase.Began),
            Assert.Single(state.ConsumeBeginnings()));
        Assert.Equal(new TouchPoint(7, new Point(24, 32), TouchPhase.Ended),
            Assert.Single(state.ConsumeEndings()));
        Assert.Empty(state.ConsumeBeginnings());
        Assert.Empty(state.ConsumeEndings());
    }

    [Fact]
    public void TracksMultipleContactsAndMovementSeparately()
    {
        var state = new WinFormsTouchState();
        state.Begin(1, new Point(10, 20));
        state.Begin(2, new Point(30, 40));
        state.Move(1, new Point(12, 24));
        state.End(2, new Point(31, 41));

        Assert.Equal(2, state.ConsumeBeginnings().Count);
        Assert.Equal(new TouchPoint(2, new Point(31, 41), TouchPhase.Ended),
            Assert.Single(state.ConsumeEndings()));
        Assert.Equal(new TouchPoint(1, new Point(12, 24), TouchPhase.Moved),
            Assert.Single(state.ActiveTouches));
    }

    [Fact]
    public void LostWindowCancelsActiveContactsWithoutLosingBeginnings()
    {
        var state = new WinFormsTouchState();
        state.Begin(10, new Point(100, 110));
        state.Begin(20, new Point(200, 210));
        state.CancelAll();

        Assert.Empty(state.ActiveTouches);
        Assert.Equal(2, state.ConsumeBeginnings().Count);
        Assert.All(state.ConsumeEndings(),
            point => Assert.Equal(TouchPhase.Cancelled, point.Phase));
        Assert.Empty(state.ConsumeEndings());
    }

    [Fact]
    public void DuplicateOrUnknownTransitionsDoNotProducePhantomTaps()
    {
        var state = new WinFormsTouchState();
        state.End(1, Point.Empty);
        state.Move(1, Point.Empty);
        state.Begin(1, new Point(5, 5));
        state.Begin(1, new Point(6, 6));
        state.End(1, new Point(8, 8));
        state.End(1, new Point(8, 8));

        Assert.Single(state.ConsumeBeginnings());
        Assert.Single(state.ConsumeEndings());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SequentialFastTaps_AreRecognizedIndependently_EvenWhenIdsAreReused(bool reuseId)
    {
        var state = new WinFormsTouchState();
        TouchEventPoller.Initialize(state, new TouchEventConfiguration());
        var poller = TouchEventPoller.Instance!;
        var taps = new List<int>();
        poller.TapRecognizer.Tapped += (_, e) => taps.Add(e.TouchId);

        int nextId = reuseId ? 7 : 8;
        state.Begin(7, new Point(20, 20));
        state.End(7, new Point(20, 20));
        state.Begin(nextId, new Point(24, 24));
        state.End(nextId, new Point(24, 24));

        // Both complete native lifecycles occurred before a single engine tick.
        // The adapter hands them off chronologically on successive polls.
        long tick = HighResTimer.TicksPerSecond;
        poller.PollForEvents(tick);
        Assert.Equal([7], taps);
        Assert.Empty(poller.ActiveTouches);
        poller.PollForEvents(tick + HighResTimer.TicksPerSecond / 60);
        Assert.Equal(new[] { 7, nextId }, taps);
        Assert.Empty(poller.ActiveTouches);
    }

    [Fact]
    public void SimultaneousFingers_DoNotBecomeSingleFingerTaps()
    {
        var state = new WinFormsTouchState();
        TouchEventPoller.Initialize(state, new TouchEventConfiguration());
        var poller = TouchEventPoller.Instance!;
        int taps = 0;
        poller.TapRecognizer.Tapped += (_, _) => taps++;

        state.Begin(1, new Point(20, 20));
        state.Begin(2, new Point(30, 30));
        state.End(1, new Point(20, 20));
        state.End(2, new Point(30, 30));
        poller.PollForEvents(HighResTimer.TicksPerSecond);

        Assert.Equal(0, taps);
        Assert.Empty(poller.ActiveTouches);
    }

    [Fact]
    public void EventsArrivingDuringBatchReads_AreProcessedOnTheNextPoll()
    {
        var state = new WinFormsTouchState();
        state.Begin(1, new Point(10, 10));
        state.End(1, new Point(10, 10));

        // Mimic a native callback precisely between the two ITouchAdapter
        // consumption calls made by TouchEventPoller.
        Assert.Equal(1, Assert.Single(state.ConsumeBeganTouches()).Id);
        state.Begin(2, new Point(20, 20));
        state.End(2, new Point(20, 20));
        Assert.Equal(1, Assert.Single(state.ConsumeEndedTouches()).Id);
        Assert.Empty(state.ActiveTouches);

        Assert.Equal(2, Assert.Single(state.ConsumeBeganTouches()).Id);
        Assert.Equal(2, Assert.Single(state.ConsumeEndedTouches()).Id);
        Assert.Empty(state.ActiveTouches);
    }

    [Fact]
    public void LateTapDuringPolling_CannotCreateStuckContacts()
    {
        var state = new WinFormsTouchState();
        TouchEventPoller.Initialize(state, new TouchEventConfiguration());
        var poller = TouchEventPoller.Instance!;
        int taps = 0;
        poller.TapRecognizer.Tapped += (_, _) => taps++;

        state.Begin(1, new Point(10, 10));
        state.End(1, new Point(10, 10));
        poller.TouchBegan += (_, e) =>
        {
            if (e.Touch.Id != 1)
                return;

            // These events happen after both queues were snapshotted by
            // the poller; they must not alter the current batch.
            state.Begin(2, new Point(40, 40));
            state.End(2, new Point(40, 40));
        };

        long tick = HighResTimer.TicksPerSecond;
        poller.PollForEvents(tick);
        poller.PollForEvents(tick + HighResTimer.TicksPerSecond / 60);

        Assert.Equal(2, taps);
        Assert.Empty(poller.ActiveTouches);
    }

    [Theory]
    [InlineData(unchecked((long)0xFF515780), true)]
    [InlineData(unchecked((long)0xFF515781), true)]
    [InlineData(unchecked((long)0xFF515700), false)] // Pen is not touch.
    [InlineData(0L, false)] // Real mouse.
    [InlineData(0x12345678L, false)]
    public void FiltersOnlyTouchPromotedMouseMessages(long extraInfo, bool expected)
    {
        Assert.Equal(expected,
            WinFormsTouchState.IsPromotedTouchMouseMessage(extraInfo));
    }

    public void Dispose() => TouchEventPoller.Reset();
}
