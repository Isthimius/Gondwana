namespace Gondwana.Input.Touch;

/// <summary>
/// Represents a touch input source that tracks active contact points and raises lifecycle events
/// when fingers (or pointer-emulated contacts) begin, move, or end on a surface.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for translating platform-specific pointer or
/// touch events into Gondwana <see cref="TouchPoint"/> values and maintaining the
/// <see cref="ActiveTouches"/> collection. On desktop platforms where no hardware touch screen is
/// available, adapters may emulate a single touch point using the mouse cursor.
/// </remarks>
public interface ITouchInput
{
    /// <summary>
    /// Gets the list of touch contact points that are currently active (in contact with the surface).
    /// This list is updated before each event is raised and reflects the state at the time of the last
    /// pointer action. An empty list indicates no fingers are currently in contact.
    /// </summary>
    IReadOnlyList<TouchPoint> ActiveTouches { get; }

    /// <summary>
    /// Occurs when a new touch contact begins — a finger or pointer first makes contact with the surface.
    /// The <see cref="TouchEventArgs.Touch"/> property contains the new contact point with
    /// <see cref="TouchPhase.Began"/>.
    /// </summary>
    event EventHandler<TouchEventArgs> TouchBegan;

    /// <summary>
    /// Occurs when an active touch contact moves across the surface.
    /// The <see cref="TouchEventArgs.Touch"/> property contains the updated contact point with
    /// <see cref="TouchPhase.Moved"/>.
    /// </summary>
    event EventHandler<TouchEventArgs> TouchMoved;

    /// <summary>
    /// Occurs when an active touch contact ends — a finger or pointer lifts from the surface,
    /// or the contact is cancelled by the system.
    /// The <see cref="TouchEventArgs.Touch"/> property contains the final contact point with
    /// <see cref="TouchPhase.Ended"/> or <see cref="TouchPhase.Cancelled"/>.
    /// </summary>
    event EventHandler<TouchEventArgs> TouchEnded;
}
