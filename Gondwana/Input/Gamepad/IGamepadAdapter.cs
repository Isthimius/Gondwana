namespace Gondwana.Input.Gamepad;

public interface IGamepadAdapter
{
    IReadOnlyCollection<string> PressedButtons { get; }

    GamepadStickState? LeftStick { get; }
    GamepadStickState? RightStick { get; }
}
