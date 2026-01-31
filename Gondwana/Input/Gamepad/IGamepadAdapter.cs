namespace Gondwana.Input.Gamepad;

/// <summary>
/// Defines an adapter interface for accessing gamepad device state, including buttons, analog sticks,
/// and triggers. Implementations of this interface provide abstraction over various gamepad input APIs
/// (such as XInput, SDL2, or DirectInput) to provide unified access to gamepad hardware.
/// </summary>
public interface IGamepadAdapter
{
    /// <summary>
    /// Gets the unique identifier for this gamepad device.
    /// This identifier is used to distinguish between multiple connected gamepads and should remain
    /// consistent for the same physical device throughout the application session.
    /// </summary>
    string GamepadId { get; }

    /// <summary>
    /// Gets a read-only collection of button identifiers that are currently pressed on the gamepad.
    /// Button identifiers typically include standard gamepad button names such as "A", "B", "X", "Y",
    /// "Start", "Back", "LeftShoulder", "RightShoulder", and directional pad buttons like "DPadUp", "DPadDown", etc.
    /// The collection is empty when no buttons are pressed.
    /// </summary>
    IReadOnlyCollection<string> PressedButtons { get; }

    /// <summary>
    /// Gets the current state of the left analog stick, or <c>null</c> if the gamepad does not have
    /// a left stick or the stick state is unavailable. The state includes normalized position values
    /// in the range [-1, 1] for both X and Y axes, as well as the raw input values.
    /// </summary>
    GamepadStickState? LeftStick { get; }

    /// <summary>
    /// Gets the current state of the right analog stick, or <c>null</c> if the gamepad does not have