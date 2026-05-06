using System.Drawing;

namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Provides data for the <see cref="TapGestureRecognizer.Tapped"/> event,
/// including the screen position where the tap occurred.
/// </summary>
public sealed class TappedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the position of the tap in client (control-local) coordinates.
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Gets the identifier of the touch contact point that triggered the tap.
    /// On desktop, mouse-emulated taps always use <c>Id = 0</c>.
    /// </summary>
    public int TouchId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TappedEventArgs"/> class.
    /// </summary>
    /// <param name="touchId">The identifier of the touch contact that triggered the tap.</param>
    /// <param name="position">The position of the tap in client coordinates.</param>
    public TappedEventArgs(int touchId, Point position)
    {
        TouchId = touchId;
        Position = position;
    }
}
