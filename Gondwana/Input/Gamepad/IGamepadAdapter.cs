namespace Gondwana.Input.Gamepad;

/// <summary>
/// Defines an adapter interface for accessing gamepad input state and information
/// </summary>
public interface IGamepadAdapter
{
    /// <summary>
    /// Gets the unique identifier for the gamepad device
    /// </summary>
    string GamepadId { get; }

    /// <summary>
    /// Gets a read-only collection of button identifiers that are currently pressed on the gamepad
    /// </summary>
    IReadOnlyCollection<string> PressedButtons { get; }

    /// <summary>
    /// Gets the current state of the left analog stick, or null if not available
    /// </summary>
    GamepadStickState? LeftStick { get; }

    /// <summary>
    /// Gets the current state of the right analog stick, or null if not available
    /// </summary>
    GamepadStickState? RightStick { get; }

    /// <summary>
    /// Gets the current pressure value of the left trigger, typically ranging from 0.0 (not pressed) to 1.0 (fully pressed)
    /// </summary>
    public float LeftTrigger { get; }

    /// <summary>
    /// Gets the current pressure value of the right trigger, typically ranging from 0.0 (not pressed) to 1.0 (fully pressed)
    /// </summary>
    public float RightTrigger { get; }
}