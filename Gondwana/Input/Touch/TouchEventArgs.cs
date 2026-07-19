namespace Gondwana.Input.Touch;

/// <summary>
/// Provides data for touch contact events raised by <see cref="ITouchInput"/>,
/// including the touch point and engine tick associated with the event.
/// </summary>
public sealed class TouchEventArgs : EventArgs
{
    /// <summary>
    /// Gets the touch contact point associated with this event.
    /// </summary>
    public TouchPoint Touch { get; }

    /// <summary>
    /// Gets the engine tick at which this touch event was emitted.
    /// </summary>
    public long Tick { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TouchEventArgs"/> class.
    /// </summary>
    /// <param name="touch">The touch contact point that caused the event.</param>
    /// <param name="tick">The engine tick at which the event was emitted.</param>
    public TouchEventArgs(TouchPoint touch, long tick = 0)
    {
        Touch = touch;
        Tick = tick;
    }
}
