namespace Gondwana.Input.Keyboard;

/// <summary>
/// Represents the type of action performed on a keyboard key, indicating whether the key
/// was pressed, released, or is being held down and generating repeated input events.
/// This enumeration is typically used in keyboard event handling to distinguish between
/// different phases of key interaction.
/// </summary>
public enum KeyAction
{
    /// <summary>
    /// The key was initially pressed down. This action occurs once when the user first pushes
    /// the key, before any repeat events are generated.
    /// </summary>
    Pressed,

    /// <summary>
    /// The key was released after being pressed. This action occurs once when the user lifts
    /// their finger from the key, marking the end of the key press interaction.
    /// </summary>
    Released,

    /// <summary>
    /// The key is being held down and is generating repeated input events. This action occurs
    /// continuously at a system-defined or application-defined rate while the user keeps the key
    /// pressed, typically used to implement auto-repeat behavior for text input or held actions.
    /// </summary>
    Repeated
}
