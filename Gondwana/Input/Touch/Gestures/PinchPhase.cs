namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Identifies the lifecycle phase of a two-contact pinch gesture.
/// </summary>
public enum PinchPhase
{
    /// <summary>Exactly two contacts became active and established the pinch baseline.</summary>
    Began,

    /// <summary>The distance or focal point between the active contacts changed.</summary>
    Updated,

    /// <summary>The two-contact pinch ended or was interrupted by another contact.</summary>
    Ended,
}
