using Gondwana.Input.Gamepad;
using static SDL2.SDL;

namespace Gondwana.WinForms.Input.Gamepad.SDL2;

public sealed class SdlGamepadAdapter : IGamepadAdapter
{
    private readonly IntPtr _joystick;
    private readonly int _deviceIndex;
    private readonly HashSet<string> _pressedButtons = new();

    public string GamepadId { get; }

    public IReadOnlyCollection<string> PressedButtons => _pressedButtons;
    public GamepadStickState? LeftStick { get; private set; }
    public GamepadStickState? RightStick { get; private set; } = null;
    public float LeftTrigger => 0f;
    public float RightTrigger => 0f;

    public SdlGamepadAdapter(int deviceIndex)
    {
        _deviceIndex = deviceIndex;
        _joystick = SDL_JoystickOpen(deviceIndex);
        GamepadId = $"SDL_{deviceIndex}";
    }

    public void Poll()
    {
        _pressedButtons.Clear();

        for (int i = 0; i < SDL_JoystickNumButtons(_joystick); i++)
        {
            if (SDL_JoystickGetButton(_joystick, i) == 1)
                _pressedButtons.Add($"Button{i}");
        }

        int rawX = SDL_JoystickGetAxis(_joystick, 0); // Left stick X
        int rawY = SDL_JoystickGetAxis(_joystick, 1); // Left stick Y
        LeftStick = GamepadStickState.FromRaw16(rawX, rawY);
    }

    public void Dispose()
    {
        if (_joystick != IntPtr.Zero)
        {
            SDL_JoystickClose(_joystick);
        }
    }
}
