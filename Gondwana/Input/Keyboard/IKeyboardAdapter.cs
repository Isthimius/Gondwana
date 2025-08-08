namespace Gondwana.Input.Keyboard;

/// <summary>
/// Represents an adapter for keyboard input, providing access to the currently pressed keys and active modifier states.
/// </summary>
/// <remarks>This interface is designed to abstract keyboard input handling, allowing retrieval of pressed keys
/// and modifier states. The implementation of the interface is responsible for keyboard polling, etc.</remarks>
public interface IKeyboardAdapter
{
    /// <summary>
    /// Gets the collection of keys that are currently pressed.
    /// </summary>
    ICollection<string> PressedKeys { get; }

    /// <summary>
    /// Gets the current state of modifier keys, such as Shift, Ctrl, and Alt.
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers { get; }
}
