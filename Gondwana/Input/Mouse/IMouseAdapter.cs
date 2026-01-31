using System.Drawing;
using Gondwana.Input.Keyboard;

namespace Gondwana.Input.Mouse;

/// <summary>
/// Defines an adapter interface for accessing mouse device state, including cursor position,
/// button presses, scroll wheel input, and keyboard modifier states. Implementations of this interface
/// provide abstraction over various mouse input APIs to provide unified access to mouse hardware
/// across different platforms.
/// </summary>
public interface IMouseAdapter
{
    /// <summary>
    /// Gets the current position of the mouse cursor in screen or client coordinates, depending on
    /// the implementation. This position is typically updated continuously as the user moves the mouse
    /// and represents the pixel coordinates where the cursor is currently located.
    /// </summary>
    Point CurrentPosition { get; }

    /// <summary>
    /// Gets the set of mouse buttons that are currently pressed. This collection includes identifiers
    /// for all buttons that are in the down state, such as <see cref="MouseButton.Left"/>,
    /// <see cref="MouseButton.Right"/>, <see cref="MouseButton.Middle"/>, and additional buttons
    /// if supported by the hardware. The collection is empty when no buttons are pressed.
    /// </summary>
    HashSet<MouseButton> PressedButtons { get; }

    /// <summary>
    /// Gets the current state of keyboard modifier keys (Shift, Ctrl, Alt) at the time of mouse input.
    /// This allows mouse event handlers to respond to modified mouse actions, such as Ctrl+Click or
    /// Shift+Drag. The state can be a combination of multiple modifiers using bitwise flags.
    /// </summary>
    KeyboardModifierState CurrentKeyboardModifiers { get; }

    /// <summary>
    /// Gets the accumulated scroll wheel delta since the last poll, measured in implementation-defined units.
    /// Positive values indicate upward scrolling (scrolling away from the user), while negative values
    /// indicate downward scrolling (scrolling toward the user). The magnitude represents the distance
    /// or speed of the scroll. This value is typically reset or accumulated between polling intervals.
    /// </summary>
    int ScrollDelta { get; }  // Positive = scroll up, negative = scroll down
}