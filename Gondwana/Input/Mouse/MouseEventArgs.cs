using System.Drawing;
using Gondwana.Input.Keyboard;

namespace Gondwana.Input.Mouse;

/// <summary>
/// Provides comprehensive data for mouse events, including cursor position (current and previous),
/// button states with transition information, scroll wheel delta, and keyboard modifier states.
/// This event args class is used with mouse input polling to deliver complete mouse state information
/// to event handlers, enabling sophisticated mouse interaction handling including drag operations,
/// modified clicks, and scroll detection.
/// </summary>
public sealed class MouseEventArgs : EventArgs
{
    /// <summary>
    /// Gets the configuration details for the mouse event monitoring, including throttling settings
    /// and pause state. This configuration controls how frequently mouse events can be generated
    /// and whether event processing is currently active.
    /// </summary>
    public MouseEventConfiguration MouseEventConfiguration { get; }

    /// <summary>
    /// Gets the state of keyboard modifier keys (Shift, Ctrl, Alt) at the time this mouse event was generated.
    /// This can be a combination of multiple modifiers when keys like Ctrl+Shift are pressed together
    /// with mouse actions. Use the convenience properties <see cref="IsShift"/>, <see cref="IsCtrl"/>, 
    /// and <see cref="IsAlt"/> to check for specific modifiers.
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers { get; }

    /// <summary>
    /// Gets a read-only dictionary mapping each monitored mouse button to its current state,
    /// including whether it is down, was just pressed, or was just released. This provides
    /// comprehensive information about all mouse button states and transitions in a single event,
    /// enabling handlers to respond to complex multi-button interactions and detect precise
    /// press/release moments.
    /// </summary>
    public IReadOnlyDictionary<MouseButton, MouseButtonState> ButtonStates { get; }

    /// <summary>
    /// Gets the previous position of the mouse cursor before the current polling interval.
    /// This position, combined with <see cref="CurrentPosition"/>, allows calculation of mouse
    /// movement delta, which is useful for implementing drag operations, camera controls,
    /// and other motion-based interactions.
    /// </summary>
    public Point PreviousPosition { get; }

    /// <summary>
    /// Gets the current position of the mouse cursor at the time this event was generated.
    /// This represents the pixel coordinates where the cursor is located and can be compared
    /// with <see cref="PreviousPosition"/> to determine movement direction and distance.
    /// </summary>
    public Point CurrentPosition { get; }

    /// <summary>
    /// Gets the accumulated scroll wheel delta since the last poll, measured in implementation-defined units.
    /// Positive values indicate upward scrolling (scrolling away from the user), while negative values
    /// indicate downward scrolling (scrolling toward the user). The magnitude represents the distance
    /// or speed of the scroll. A value of 0 indicates no scrolling occurred.
    /// </summary>
    public int ScrollDelta { get; }

    /// <summary>
    /// Gets a value indicating whether the Shift modifier key was pressed at the time this mouse event was generated.
    /// This is a convenience property that checks if the <see cref="CurrentKeyboardModifiers"/> flags include
    /// <see cref="KeyboardModifierState.Shift"/>, commonly used for modified mouse operations like
    /// multi-selection or alternate drag modes.
    /// </summary>
    public bool IsShift => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Shift);

    /// <summary>
    /// Gets a value indicating whether the Control (Ctrl) modifier key was pressed at the time this mouse event was generated.
    /// This is a convenience property that checks if the <see cref="CurrentKeyboardModifiers"/> flags include
    /// <see cref="KeyboardModifierState.Ctrl"/>, commonly used for modified mouse operations like
    /// Ctrl+Click for opening in new tabs or adding to selections.
    /// </summary>
    public bool IsCtrl => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Ctrl);

    /// <summary>
    /// Gets a value indicating whether the Alt modifier key was pressed at the time this mouse event was generated.
    /// This is a convenience property that checks if the <see cref="CurrentKeyboardModifiers"/> flags include
    /// <see cref="KeyboardModifierState.Alt"/>, commonly used for alternate mouse operations or special drag modes.
    /// </summary>
    public bool IsAlt => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Alt);

    /// <summary>
    /// Gets the engine tick at the time this mouse event was generated.
    /// </summary>
    public long Tick { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MouseEventArgs"/> class with comprehensive mouse state information
    /// including configuration, button states, cursor positions, scroll data, and keyboard modifiers.
    /// </summary>
    /// <param name="mouseEventConfiguration">
    /// The configuration details for mouse event monitoring, including throttling and pause settings.
    /// </param>
    /// <param name="currentKeyboardModifiers">
    /// The state of keyboard modifier keys (Shift, Ctrl, Alt) at the time of the mouse event.
    /// This can be a combination of multiple modifiers using bitwise flags.
    /// </param>
    /// <param name="buttonStates">
    /// A read-only dictionary mapping each mouse button to its current state, including down state
    /// and press/release transitions. This provides complete button state information for the event.
    /// </param>
    /// <param name="previousPosition">
    /// The previous position of the mouse cursor before this polling interval, used to calculate
    /// movement deltas for drag operations and motion tracking.
    /// </param>
    /// <param name="currentPosition">
    /// The current position of the mouse cursor at the time of this event, representing where
    /// the cursor is currently located in screen or client coordinates.
    /// </param>
    /// <param name="scrollDelta">
    /// The accumulated scroll wheel delta since the last poll. Positive values indicate upward scrolling,
    /// negative values indicate downward scrolling, and 0 indicates no scrolling occurred.
    /// </param>
    /// <param name="tick">
    /// The timestamp or tick count at the time this mouse event was generated.
    /// </param>
    public MouseEventArgs(MouseEventConfiguration mouseEventConfiguration,
                          KeyboardModifierState currentKeyboardModifiers,
                          IReadOnlyDictionary<MouseButton, MouseButtonState> buttonStates,
                          Point previousPosition,
                          Point currentPosition,
                          int scrollDelta,
                          long tick)
    {
        MouseEventConfiguration = mouseEventConfiguration;
        CurrentKeyboardModifiers = currentKeyboardModifiers;
        ButtonStates = buttonStates;
        PreviousPosition = previousPosition;
        CurrentPosition = currentPosition;
        ScrollDelta = scrollDelta;
        Tick = tick;
    }

    /// <summary>
    /// Determines whether the specified mouse button is currently in the down (pressed) state.
    /// </summary>
    /// <param name="button">The mouse button to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified button is currently pressed; otherwise, <c>false</c>.
    /// Returns <c>false</c> if the button is not present in the tracked state collection.
    /// </returns>
    /// <remarks>
    /// This method provides a safe and convenient way to query button state without directly accessing
    /// the <see cref="ButtonStates"/> dictionary. It avoids exceptions that could occur if a button
    /// is not present and centralizes lookup logic.
    /// </remarks>
    public bool IsButtonDown(MouseButton button)
        => ButtonStates.TryGetValue(button, out var state) && state.IsDown;

    /// <summary>
    /// Determines whether the specified mouse button was pressed during the current polling interval.
    /// </summary>
    /// <param name="button">The mouse button to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified button transitioned from up to down in the current event; otherwise, <c>false</c>.
    /// Returns <c>false</c> if the button is not present in the tracked state collection.
    /// </returns>
    /// <remarks>
    /// This method is useful for detecting discrete click actions without triggering repeatedly while the button
    /// is held down. The <c>JustPressed</c> state is only <c>true</c> for a single polling cycle.
    /// </remarks>
    public bool IsButtonJustPressed(MouseButton button)
        => ButtonStates.TryGetValue(button, out var state) && state.JustPressed;

    /// <summary>
    /// Determines whether the specified mouse button was released during the current polling interval.
    /// </summary>
    /// <param name="button">The mouse button to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified button transitioned from down to up in the current event; otherwise, <c>false</c>.
    /// Returns <c>false</c> if the button is not present in the tracked state collection.
    /// </returns>
    /// <remarks>
    /// This method is useful for detecting the completion of a click or drag action. The <c>JustReleased</c>
    /// state is only <c>true</c> for a single polling cycle.
    /// </remarks>
    public bool IsButtonJustReleased(MouseButton button)
        => ButtonStates.TryGetValue(button, out var state) && state.JustReleased;

    /// <summary>
    /// Gets a value indicating whether the left mouse button is currently pressed.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonDown(MouseButton)"/> for the commonly used
    /// primary mouse button.
    /// </remarks>
    public bool LeftButtonDown => IsButtonDown(MouseButton.Left);

    /// <summary>
    /// Gets a value indicating whether the left mouse button was pressed during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustPressed(MouseButton)"/> and is commonly
    /// used for detecting primary click actions.
    /// </remarks>
    public bool LeftButtonJustPressed => IsButtonJustPressed(MouseButton.Left);

    /// <summary>
    /// Gets a value indicating whether the left mouse button was released during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustReleased(MouseButton)"/> and is commonly
    /// used for detecting the end of click or drag operations.
    /// </remarks>
    public bool LeftButtonJustReleased => IsButtonJustReleased(MouseButton.Left);

    /// <summary>
    /// Gets a value indicating whether the right mouse button is currently pressed.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonDown(MouseButton)"/> for the secondary mouse button,
    /// typically used for context actions.
    /// </remarks>
    public bool RightButtonDown => IsButtonDown(MouseButton.Right);

    /// <summary>
    /// Gets a value indicating whether the right mouse button was pressed during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustPressed(MouseButton)"/> and is commonly
    /// used for context menu or alternate interaction triggers.
    /// </remarks>
    public bool RightButtonJustPressed => IsButtonJustPressed(MouseButton.Right);

    /// <summary>
    /// Gets a value indicating whether the right mouse button was released during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustReleased(MouseButton)"/> and is commonly
    /// used to detect the completion of context interactions.
    /// </remarks>
    public bool RightButtonJustReleased => IsButtonJustReleased(MouseButton.Right);

    /// <summary>
    /// Gets a value indicating whether the middle mouse button is currently pressed.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonDown(MouseButton)"/> for the middle mouse button,
    /// often associated with scroll wheel clicks or special actions.
    /// </remarks>
    public bool MiddleButtonDown => IsButtonDown(MouseButton.Middle);

    /// <summary>
    /// Gets a value indicating whether the middle mouse button was pressed during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustPressed(MouseButton)"/> and can be used
    /// for specialized interactions such as panning or alternate controls.
    /// </remarks>
    public bool MiddleButtonJustPressed => IsButtonJustPressed(MouseButton.Middle);

    /// <summary>
    /// Gets a value indicating whether the middle mouse button was released during the current polling interval.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="IsButtonJustReleased(MouseButton)"/> and is useful
    /// for detecting the completion of middle-button interactions.
    /// </remarks>
    public bool MiddleButtonJustReleased => IsButtonJustReleased(MouseButton.Middle);
}