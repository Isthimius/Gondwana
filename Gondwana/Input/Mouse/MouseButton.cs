namespace Gondwana.Input.Mouse;

/// <summary>
/// Represents mouse button identifiers that can be used individually or combined to represent
/// multiple simultaneous button presses. This is a flags enumeration, allowing multiple buttons
/// to be combined using bitwise operations to track complex button combinations
/// (e.g., Left | Right for simultaneous left and right button presses).
/// </summary>
[Flags]
public enum MouseButton
{
    /// <summary>
    /// No mouse button is pressed. This represents the default mouse state when the cursor
    /// is not actively clicking or when all buttons have been released.
    /// </summary>
    None = 0,

    /// <summary>
    /// The primary (left) mouse button. This is typically used for standard clicking, selecting,
    /// and dragging operations. On most mice, this is the button under the index finger.
    /// Can be combined with <see cref="Right"/> and/or <see cref="Middle"/> for multi-button input.
    /// </summary>
    Left = 1,

    /// <summary>
    /// The secondary (right) mouse button. This is typically used for context menus and alternate
    /// actions. On most mice, this is the button under the middle or ring finger.
    /// Can be combined with <see cref="Left"/> and/or <see cref="Middle"/> for multi-button input.
    /// </summary>
    Right = 2,

    /// <summary>
    /// The middle mouse button, usually implemented as a clickable scroll wheel. This is often
    /// used for special actions like opening links in new tabs or panning in applications.
    /// Can be combined with <see cref="Left"/> and/or <see cref="Right"/> for multi-button input.
    /// </summary>
    Middle = 4
}