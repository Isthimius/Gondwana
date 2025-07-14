using static Gondwana.Input.Gamepad.WinForms.XInput;

namespace Gondwana.Input.Gamepad.WinForms
{
    public sealed class XInputGamepadAdapter : IGamepadAdapter
    {
        public string GamepadId { get; }

        private readonly int _controllerIndex;
        private readonly HashSet<string> _pressedButtons = new();
        public IReadOnlyCollection<string> PressedButtons => _pressedButtons;

        public GamepadStickState? LeftStick { get; private set; }
        public GamepadStickState? RightStick { get; private set; }

        private const float MaxStickValue = 32767f;

        public XInputGamepadAdapter(int controllerIndex = 0)
        {
            _controllerIndex = controllerIndex;
            GamepadId = $"XInput{controllerIndex}";
        }

        public void Poll()
        {
            _pressedButtons.Clear();

            if (GetState(_controllerIndex, out var state) != 0)
                return; // Controller not connected

            var buttons = (XInputButtons)state.Gamepad.wButtons;
            foreach (XInputButtons button in Enum.GetValues<XInputButtons>())
            {
                if ((buttons & button) != 0)
                    _pressedButtons.Add(button.ToString());
            }

            LeftStick = GamepadStickState.FromRaw16(state.Gamepad.sThumbLX, state.Gamepad.sThumbLY);
            RightStick = GamepadStickState.FromRaw16(state.Gamepad.sThumbRX, state.Gamepad.sThumbRY);
        }

        // TODO: add triggers, DPad, bumpers, etc. if needed
    }
}
