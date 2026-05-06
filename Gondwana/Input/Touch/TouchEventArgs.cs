namespace Gondwana.Input.Touch;

/// <summary>
/// Provides data for touch contact events raised by <see cref="ITouchInput"/>,
/// including the details of the touch point that triggered the event.
/// </summary>
public sealed class TouchEventArgs : EventArgs
{
    /// <summary>
    /// Gets the touch contact point associated with this event, including its identifier,
    /// screen position, and current lifecycle phase.
    /// </summary>
    public TouchPoint Touch { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TouchEventArgs"/> class with the specified
    /// touch contact point.
    /// </summary>
    /// <param name="touch">
    /// The touch contact point that caused this event to be raised.
    /// </param>
    public TouchEventArgs(TouchPoint touch)
    {
        Touch = touch;
    }
}
