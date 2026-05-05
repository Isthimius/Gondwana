using System.Drawing;

namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Provides data for the <see cref="SwipeGestureRecognizer.Swiped"/> event,
/// including the direction, start and end positions, and the computed speed of the swipe.
/// </summary>
public sealed class SwipedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the primary direction of the swipe gesture.
    /// </summary>
    public SwipeDirection Direction { get; }

    /// <summary>
    /// Gets the position where the swipe gesture began, in client (control-local) coordinates.
    /// </summary>
    public Point StartPosition { get; }

    /// <summary>
    /// Gets the position where the swipe gesture ended, in client (control-local) coordinates.
    /// </summary>
    public Point EndPosition { get; }

    /// <summary>
    /// Gets the speed of the swipe in pixels per second.
    /// This is calculated as the Euclidean distance between <see cref="StartPosition"/> and
    /// <see cref="EndPosition"/> divided by the duration of the contact.
    /// </summary>
    public double SpeedPixelsPerSecond { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwipedEventArgs"/> class.
    /// </summary>
    /// <param name="direction">The primary direction of the swipe.</param>
    /// <param name="startPosition">The position where the swipe began.</param>
    /// <param name="endPosition">The position where the swipe ended.</param>
    /// <param name="speedPixelsPerSecond">The speed of the swipe in pixels per second.</param>
    public SwipedEventArgs(SwipeDirection direction, Point startPosition, Point endPosition, double speedPixelsPerSecond)
    {
        Direction = direction;
        StartPosition = startPosition;
        EndPosition = endPosition;
        SpeedPixelsPerSecond = speedPixelsPerSecond;
    }
}
