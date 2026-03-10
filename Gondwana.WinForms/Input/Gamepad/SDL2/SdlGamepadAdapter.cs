using Gondwana.Input.Gamepad;
using static SDL2.SDL;

namespace Gondwana.WinForms.Input.Gamepad.SDL2;

/// <summary>
/// Provides a gamepad adapter implementation using SDL2 for cross-platform gamepad input support.
/// </summary>
public sealed class SdlGamepadAdapter : IGamepadAdapter
{
    private readonly IntPtr _controller;
    private readonly HashSet<string> _pressedButtons = new();

    /// <summary>
    /// Gets the unique identifier for this gamepad.
    /// </summary>
    public string GamepadId { get; }

    /// <summary>
    /// Gets the collection of currently pressed button names.
    /// </summary>
    public IReadOnlyCollection<string> PressedButtons => _pressedButtons;
    
    /// <summary>
    /// Gets the current state of the left analog stick.
    /// </summary>
    public GamepadStickState? LeftStick { get; private set; }
    
    /// <summary>
    /// Gets the current state of the right analog stick.
    /// </summary>
    public GamepadStickState? RightStick { get; private set; }
    
    /// <summary>
    /// Gets the current pressure value of the left trigger (0.0 to 1.0).
    /// </summary>
    public float LeftTrigger { get; private set; }
    
    /// <summary>
    /// Gets the current pressure value of the right trigger (0.0 to 1.0).
    /// </summary>
    public float RightTrigger { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SdlGamepadAdapter"/> class for the specified device.
    /// </summary>
    /// <param name="deviceIndex">The SDL device index of the gamepad to open.</param>
    /// <exception cref="InvalidOperationException">Thrown when the SDL gamepad controller cannot be opened at the specified index.</exception>
    public SdlGamepadAdapter(int deviceIndex)
    {
        _controller = SDL_GameControllerOpen(deviceIndex);
        if (_controller == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to open SDL_GameController at index {deviceIndex}");

        GamepadId = $"SDL_CONTROLLER_{deviceIndex}";
    }

    /// <summary>
    /// Polls the gamepad for the current state of all buttons, analog sticks, and triggers.
    /// </summary>
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

    /// <summary>
    /// Releases all resources used by the <see cref="SdlGamepadAdapter"/> and closes the SDL gamepad controller.
    /// </summary>
    public void Dispose()
    {
        if (_controller != IntPtr.Zero)
            SDL_GameControllerClose(_controller);
    }
}