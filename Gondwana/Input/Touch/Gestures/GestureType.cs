namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Identifies the kind of gesture carried by a <see cref="GestureEventArgs"/>.
/// </summary>
public enum GestureType
{
    /// <summary>
    /// A single-finger tap. See <see cref="GestureEventArgs.Tap"/> for position details.
    /// </summary>
    Tap,

    /// <summary>
    /// A single-finger swipe. See <see cref="GestureEventArgs.Swipe"/> for direction and speed details.
    /// </summary>
    Swipe,

    /// <summary>
    /// A two-finger pinch or spread. See <see cref="GestureEventArgs.Pinch"/> for scale delta details.
    /// </summary>
    Pinch,
}
