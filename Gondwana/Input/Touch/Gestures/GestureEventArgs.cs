namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Provides data for the <see cref="TouchEventPoller.TouchEvent"/>, carrying the details of a
/// recognized gesture. Check <see cref="IsTap"/>, <see cref="IsSwipe"/>, or <see cref="IsPinch"/>
/// to determine which kind of gesture occurred, then access the corresponding nullable property
/// (<see cref="Tap"/>, <see cref="Swipe"/>, or <see cref="Pinch"/>) for the specific data.
/// </summary>
/// <remarks>
/// <para>
/// This follows the same pattern as <c>MouseEventArgs</c>: a single unified event type whose
/// properties make it easy to filter and act on specific interaction kinds. For example:
/// </para>
/// <code>
/// engine.Input.TouchEventPoller.TouchEvent += e =>
/// {
///     if (e.IsTap)   HandleTap(e.Tap!);
///     if (e.IsSwipe) HandleSwipe(e.Swipe!);
///     if (e.IsPinch) HandlePinch(e.Pinch!);
/// };
/// </code>
/// </remarks>
public sealed class GestureEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of gesture that this event represents.
    /// Use <see cref="IsTap"/>, <see cref="IsSwipe"/>, or <see cref="IsPinch"/> as convenient
    /// boolean shorthands, or switch on this value for exhaustive handling.
    /// </summary>
    public GestureType GestureType { get; }

    /// <summary>
    /// Gets data specific to a tap gesture, or <see langword="null"/> when <see cref="GestureType"/>
    /// is not <see cref="GestureType.Tap"/>.
    /// </summary>
    public TappedEventArgs? Tap { get; }

    /// <summary>
    /// Gets data specific to a swipe gesture, or <see langword="null"/> when <see cref="GestureType"/>
    /// is not <see cref="GestureType.Swipe"/>.
    /// </summary>
    public SwipedEventArgs? Swipe { get; }

    /// <summary>
    /// Gets data specific to a pinch or spread gesture, or <see langword="null"/> when
    /// <see cref="GestureType"/> is not <see cref="GestureType.Pinch"/>.
    /// </summary>
    public PinchedEventArgs? Pinch { get; }

    /// <summary>
    /// Gets a value indicating whether this event represents a tap gesture.
    /// When <see langword="true"/>, <see cref="Tap"/> is non-null.
    /// </summary>
    public bool IsTap => GestureType == GestureType.Tap;

    /// <summary>
    /// Gets a value indicating whether this event represents a swipe gesture.
    /// When <see langword="true"/>, <see cref="Swipe"/> is non-null.
    /// </summary>
    public bool IsSwipe => GestureType == GestureType.Swipe;

    /// <summary>
    /// Gets a value indicating whether this event represents a pinch or spread gesture.
    /// When <see langword="true"/>, <see cref="Pinch"/> is non-null.
    /// </summary>
    public bool IsPinch => GestureType == GestureType.Pinch;

    /// <summary>
    /// Initializes a new <see cref="GestureEventArgs"/> for a tap gesture.
    /// </summary>
    /// <param name="tap">Data describing the tap. Must not be <see langword="null"/>.</param>
    public GestureEventArgs(TappedEventArgs tap)
    {
        GestureType = GestureType.Tap;
        Tap = tap ?? throw new ArgumentNullException(nameof(tap));
    }

    /// <summary>
    /// Initializes a new <see cref="GestureEventArgs"/> for a swipe gesture.
    /// </summary>
    /// <param name="swipe">Data describing the swipe. Must not be <see langword="null"/>.</param>
    public GestureEventArgs(SwipedEventArgs swipe)
    {
        GestureType = GestureType.Swipe;
        Swipe = swipe ?? throw new ArgumentNullException(nameof(swipe));
    }

    /// <summary>
    /// Initializes a new <see cref="GestureEventArgs"/> for a pinch gesture.
    /// </summary>
    /// <param name="pinch">Data describing the pinch. Must not be <see langword="null"/>.</param>
    public GestureEventArgs(PinchedEventArgs pinch)
    {
        GestureType = GestureType.Pinch;
        Pinch = pinch ?? throw new ArgumentNullException(nameof(pinch));
    }
}
