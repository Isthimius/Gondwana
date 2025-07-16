using static Gondwana.Input.Gamepad.WinForms.XInput;

namespace Gondwana.Input.Gamepad.WinForms;

public sealed class XInputGamepadAdapter : IGamepadAdapter
{
    private readonly int _controllerIndex;
    private readonly HashSet<string> _pressedButtons = new();

    public string GamepadId { get; }

    public IReadOnlyCollection<string> PressedButtons => _pressedButtons;

    public GamepadStickState? LeftStick { get; private set; }
    
    public GamepadStickState? RightStick { get; private set; }

    public float LeftTrigger { get; private set; }

    public float RightTrigger { get; private set; }

    public XInputGamepadAdapter(int controllerIndex = 0)
    {
        _controllerIndex = controllerIndex;
        GamepadId = $"XInput_{controllerIndex}";
    }

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
