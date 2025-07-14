namespace Gondwana.Input.Gamepad;

public interface IGamepadAdapter
{
    string GamepadId { get; }
    IReadOnlyCollection<string> PressedButtons { get; }
    GamepadStickState? LeftStick { get; }
    GamepadStickState? RightStick { get; }
}
