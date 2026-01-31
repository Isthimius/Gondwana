namespace Gondwana.Input.Keyboard;

/// <summary>
/// Provides data for keyboard key events, including information about the key that was pressed,
/// the action performed (pressed, released, or repeated), and the state of modifier keys at the
/// time of the event. This event args class is used with keyboard input polling to deliver
/// comprehensive keyboard event information to event handlers.
/// </summary>
public sealed class KeyDownEventArgs : EventArgs
{
    /// <summary>
    /// Gets the configuration details for the keyboard key that generated this event, including
    /// the key identifier, throttling settings, and pause state. This configuration controls
    /// how frequently the key can generate events.
    /// </summary>
    public KeyEventConfiguration KeyConfig { get; }

    /// <summary>
    /// Gets the state of keyboard modifier keys (Shift, Ctrl, Alt) at the time this event was generated.
    /// This can be a combination of multiple modifiers when keys like Ctrl+Shift or Ctrl+Alt are pressed
    /// together. Use the convenience properties <see cref="IsShift"/>, <see cref="IsCtrl"/>, and
    /// <see cref="IsAlt"/> to check for specific modifiers.
    /// </summary>
    public KeyboardModifierState Modifiers { get; }

    /// <summary>
    /// Gets the type of action that occurred for this key, indicating whether the key was initially
    /// pressed, released, or is being held down and generating repeated events. This allows handlers
    /// to respond differently to key press, key release, and key repeat scenarios.
    /// </summary>
    public KeyAction KeyAction { get; }

    /// <summary>
    /// Gets a value indicating whether the Shift modifier key was pressed at the time this event was generated.
    /// This is a convenience property that checks if the <see cref="Modifiers"/> flags include
    /// <see cref="KeyboardModifierState.Shift"/>.
    /// </summary>
    public bool IsShift => Modifiers.HasFlag(KeyboardModifierState.Shift);

    /// <summary>
    /// Gets a value indicating whether the Control (Ctrl) modifier key was pressed at the time this event was generated.
    /// This is a convenience property that checks if the <see cref="Modifiers"/> flags include
    /// <see cref="KeyboardModifierState.Ctrl"/>.
    /// </summary>
    public bool IsCtrl => Modifiers.HasFlag(KeyboardModifierState.Ctrl);

    /// <summary>
    /// Gets a value indicating whether the Alt modifier key was pressed at the time this event was generated.
    /// This is a convenience property that checks if the <see cref="Modifiers"/> flags include
    /// <see cref="KeyboardModifierState.Alt"/>.
    /// </summary>
    public bool IsAlt => Modifiers.HasFlag(KeyboardModifierState.Alt);

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyDownEventArgs"/> class with the specified
    /// key configuration, modifier state, and key action.
    /// </summary>
    /// <param name="config">
    /// The configuration details for the key that generated the event, including key identification
    /// and throttling settings.
    /// </param>
    /// <param name="modifiers">
    /// The state of keyboard modifier keys (Shift, Ctrl, Alt) at the time of the event.
    /// This can be a combination of multiple modifiers using bitwise flags.
    /// </param>
    /// <param name="action">
    /// The type of action that occurred for this key (pressed, released, or repeated),
    /// indicating the phase of the key interaction.
    /// </param>
    public KeyDownEventArgs(KeyEventConfiguration config, KeyboardModifierState modifiers, KeyAction action)
    {
        KeyConfig = config;
        Modifiers = modifiers;
        KeyAction = action;
    }
}
