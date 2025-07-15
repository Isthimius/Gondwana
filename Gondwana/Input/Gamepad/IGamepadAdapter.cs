namespace Gondwana.Input.Gamepad;

public interface IGamepadAdapter
{
    string GamepadId { get; }
    IReadOnlyCollection<string> PressedButtons { get; }
    GamepadStickState? LeftStick { get; }
    GamepadStickState? RightStick { get; }
    public float LeftTrigger { get; }
    public float RightTrigger { get; }
}
