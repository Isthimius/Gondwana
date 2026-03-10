using Gondwana.Input.Gamepad;
using static Gondwana.WinForms.Input.Gamepad.XInput.XInput;

namespace Gondwana.WinForms.Input.Gamepad.XInput;

/// <summary>
/// Provides a gamepad adapter implementation using XInput for Xbox controller support on Windows.
/// </summary>
public sealed class XInputGamepadAdapter : IGamepadAdapter
{
    private readonly int _controllerIndex;
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
    /// Initializes a new instance of the <see cref="XInputGamepadAdapter"/> class for the specified controller index.
    /// </summary>
    /// <param name="controllerIndex">The XInput controller index (0-3). Default is 0.</param>
    public XInputGamepadAdapter(int controllerIndex = 0)
    {
        _controllerIndex = controllerIndex;
        GamepadId = $"XInput_{controllerIndex}";
    }

    /// <summary>
    /// Polls the gamepad for the current state of all buttons, analog sticks, and triggers.
    /// If calling in a tight loop, ensure the calls are throttled to the engine's framerate.
    /// </summary>
    internal void Poll()
    {
        _pressedButtons.Clear();

        if (GetState(_controllerIndex, out var state) != 0)
            return; // Controller not connected

        var buttons = (XInputButtons)state.Gamepad.wButtons;
        foreach (XInputButtons button in Enum.GetValues<XInputButtons>())
        {
            if ((buttons & button) != 0)
                _pressedButtons.Add(button.ToString());

            if (LeftTrigger > 0.5f)
                _pressedButtons.Add("LeftTrigger");

            if (RightTrigger > 0.5f)
                _pressedButtons.Add("RightTrigger");
        }

        LeftStick = GamepadStickState.FromRaw16(state.Gamepad.sThumbLX, state.Gamepad.sThumbLY);
        RightStick = GamepadStickState.FromRaw16(state.Gamepad.sThumbRX, state.Gamepad.sThumbRY);
        LeftTrigger = state.Gamepad.bLeftTrigger / 255f;
        RightTrigger = state.Gamepad.bRightTrigger / 255f;
    }
}