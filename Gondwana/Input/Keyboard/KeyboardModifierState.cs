namespace Gondwana.Input.Keyboard;

/// <summary>
/// Represents the state of keyboard modifier keys (Shift, Ctrl, Alt) that can be pressed simultaneously
/// with other keys to modify their behavior. This is a flags enumeration, allowing multiple modifiers
/// to be combined to represent complex modifier combinations (e.g., Ctrl | Shift for Ctrl+Shift).
/// </summary>
[Flags]
public enum KeyboardModifierState
{
    /// <summary>
    /// No modifier keys are currently pressed. This represents the default keyboard state
    /// when only regular keys (without modifiers) are being pressed or held.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Shift modifier key is currently pressed. This typically modifies character input
    /// to produce uppercase letters and alternate symbols, and can also modify the behavior
    /// of shortcut keys. Can be combined with <see cref="Ctrl"/> and/or <see cref="Alt"/> for
    /// multi-modifier shortcuts.
    /// </summary>
    Shift = 1,

    /// <summary>
    /// The Control (Ctrl) modifier key is currently pressed. This is commonly used for keyboard
    /// shortcuts and commands (e.g., Ctrl+C for copy, Ctrl+V for paste). Can be combined with
    /// <see cref="Shift"/> and/or <see cref="Alt"/> for multi-modifier shortcuts.
    /// </summary>
    Ctrl = 2,

    /// <summary>
    /// The Alt (Alternate) modifier key is currently pressed. This is used for alternate keyboard
    /// shortcuts, menu access keys, and special character input. Can be combined with <see cref="Shift"/>
    /// and/or <see cref="Ctrl"/> for multi-modifier shortcuts.
    /// </summary>
    Alt = 4
}