namespace Gondwana.Input.Keyboard;

/// <summary>
/// Represents an adapter for keyboard input, providing access to the currently pressed keys and active modifier states.
/// </summary>
/// <remarks>This interface is designed to abstract keyboard input handling, allowing retrieval of pressed keys
/// and modifier states. The implementation of the interface is responsible for keyboard polling, etc.</remarks>
public interface IKeyboardAdapter
{
    /// <summary>
    /// Returns true if the specified platform-agnostic key code is currently down.
    /// Key codes should be stable integers agreed upon by the adapter and the engine.
    /// For WinForms, this should be the Windows Virtual-Key code (0..255).
    /// </summary>
    bool IsDown(int keyCode);

    /// <summary>
    /// Gets the current state of modifier keys, such as Shift, Ctrl, and Alt.
    /// Must be safe to read from the Engine thread at high frequency.
    /// </summary>
    KeyboardModifierState CurrentKeyboardModifiers { get; }
}