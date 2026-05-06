namespace Gondwana.Input.Touch;

/// <summary>
/// Defines a passive adapter interface for accessing touch device state.
/// Implementations update their internal state in response to platform events and expose
/// that state for polling by <see cref="TouchEventPoller"/>. No events are raised directly
/// from an adapter; all event routing is handled by the poller.
/// </summary>
public interface ITouchAdapter
{
    /// <summary>
    /// Gets the list of touch contact points that are currently active (in contact with the surface).
    /// This list reflects the state at the most recent platform pointer event.
    /// An empty list indicates no fingers are currently in contact.
    /// </summary>
    IReadOnlyList<TouchPoint> ActiveTouches { get; }

    /// <summary>
    /// Drains and returns all touch contacts that ended (lifted or cancelled) since the last call.
    /// Each entry preserves the original <see cref="TouchPhase"/> (<see cref="TouchPhase.Ended"/>
    /// or <see cref="TouchPhase.Cancelled"/>) so the poller can distinguish between the two.
    /// This method is intended to be called once per engine tick by the <see cref="TouchEventPoller"/>.
    /// </summary>
    /// <returns>
    /// A snapshot of all contacts that ended since the previous call to this method.
    /// The returned list is cleared from the adapter's internal queue on each call.
    /// </returns>
    IReadOnlyList<TouchPoint> ConsumeEndedTouches();
}
