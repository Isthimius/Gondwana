using System.Drawing;
using Gondwana.Input.Touch;
using Gondwana.WinForms.Input.Touch;

namespace Gondwana.Tests.WinFormsInput;

/// <summary>
/// Tests platform-neutral touch transition buffering used by the Windows
/// adapter, without requiring a touchscreen or an HWND on Linux CI.
/// </summary>
public sealed class WinFormsTouchStateTests
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

        Assert.Equal(new TouchPoint(1, new Point(12, 24), TouchPhase.Moved),
            Assert.Single(state.ActiveTouches));
        Assert.Equal(2, state.ConsumeBeginnings().Count);
        Assert.Equal(new TouchPoint(2, new Point(31, 41), TouchPhase.Ended),
            Assert.Single(state.ConsumeEndings()));
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
}
