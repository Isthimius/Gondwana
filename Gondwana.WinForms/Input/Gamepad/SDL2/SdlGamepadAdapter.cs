using Gondwana.Input.Gamepad;
using static SDL2.SDL;

namespace Gondwana.WinForms.Input.Gamepad.SDL2;

public sealed class SdlGamepadAdapter : IGamepadAdapter
{
    private readonly IntPtr _controller;
    private readonly HashSet<string> _pressedButtons = new();

    public string GamepadId { get; }

    public IReadOnlyCollection<string> PressedButtons => _pressedButtons;
    public GamepadStickState? LeftStick { get; private set; }
    public GamepadStickState? RightStick { get; private set; }
    public float LeftTrigger { get; private set; }
    public float RightTrigger { get; private set; }

    public SdlGamepadAdapter(int deviceIndex)
    {
        _controller = SDL_GameControllerOpen(deviceIndex);
        if (_controller == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to open SDL_GameController at index {deviceIndex}");

        GamepadId = $"SDL_CONTROLLER_{deviceIndex}";
    }

    public void Poll()
    {
        _pressedButtons.Clear();

        foreach (SDL_GameControllerButton button in Enum.GetValues(typeof(SDL_GameControllerButton)))
        {
            if (button == SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID)
                continue;

            if (SDL_GameControllerGetButton(_controller, button) == 1)
                _pressedButtons.Add(button.ToString());
        }

        int lx = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
        int ly = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
        int rx = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
        int ry = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);

        int lt = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT);
        int rt = SDL_GameControllerGetAxis(_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT);

        LeftStick = GamepadStickState.FromRaw16(lx, ly);
        RightStick = GamepadStickState.FromRaw16(rx, ry);
        LeftTrigger = NormalizeTrigger(lt);
        RightTrigger = NormalizeTrigger(rt);
    }

    private static float NormalizeTrigger(int raw)
    {
        return raw / 32767f; // SDL uses signed 16-bit axes
    }

    public void Dispose()
    {
        if (_controller != IntPtr.Zero)
            SDL_GameControllerClose(_controller);
    }
}
