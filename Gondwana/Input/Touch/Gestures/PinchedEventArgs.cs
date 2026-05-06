namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Provides data for the <see cref="PinchGestureRecognizer.PinchUpdated"/> event,
/// describing the change in scale produced by a two-finger pinch or spread gesture.
/// </summary>
public sealed class PinchedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the scale factor relative to the previous pinch update.
    /// A value greater than <c>1.0</c> indicates the fingers moved apart (zoom in / expand),
    /// while a value less than <c>1.0</c> indicates the fingers moved closer together (zoom out / contract).
    /// A value of exactly <c>1.0</c> means no change in finger separation occurred.
    /// </summary>
    public double ScaleDelta { get; }

    /// <summary>
    /// Gets the current distance in pixels between the two active touch contact points.
    /// </summary>
    public double CurrentDistance { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinchedEventArgs"/> class.
    /// </summary>
    /// <param name="scaleDelta">
    /// The scale factor relative to the previous pinch update.
    /// </param>
    /// <param name="currentDistance">
    /// The current distance in pixels between the two active touch contact points.
    /// </param>
    public PinchedEventArgs(double scaleDelta, double currentDistance)
    {
        ScaleDelta = scaleDelta;
        CurrentDistance = currentDistance;
    }
}
