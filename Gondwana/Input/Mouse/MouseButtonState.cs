namespace Gondwana.Input.Mouse;

/// <summary>
/// Represents the current and transitional state of a mouse button, tracking whether the button
/// is currently held down and whether press or release events occurred in the current polling interval.
/// This structure is typically used to detect button state changes and differentiate between
/// continuous button holds and momentary press/release actions.
/// </summary>
public struct MouseButtonState
{
    /// <summary>
    /// Indicates whether the mouse button is currently in the down (pressed) state.
    /// This value remains true for the entire duration that the button is held down,
    /// from the initial press until the button is released.
    /// </summary>
    public bool IsDown;

    /// <summary>
    /// Indicates whether the mouse button transitioned from up to down (was pressed) during
    /// the current polling interval. This is true only for the single frame or poll where
    /// the button press occurred, making it useful for detecting the exact moment of a button press
    /// without responding repeatedly while the button is held down.
    /// </summary>
    public bool JustPressed;

    /// <summary>
    /// Indicates whether the mouse button transitioned from down to up (was released) during
    /// the current polling interval. This is true only for the single frame or poll where
    /// the button release occurred, making it useful for detecting the exact moment of a button release
    /// and responding to completed click actions.
    /// </summary>
    public bool JustReleased;
}