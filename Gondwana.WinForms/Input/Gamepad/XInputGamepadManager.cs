using Microsoft.Extensions.Logging;
using static Gondwana.WinForms.Input.Gamepad.XInput;

namespace Gondwana.WinForms.Input.Gamepad;

public sealed class XInputGamepadManager
{
    // TODO: include manager class in Engine; generic manager should be top-level singleton there
    public static XInputGamepadManager? Instance { get; private set; }

    private XInputGamepadManager()
    {
        Engine.Logger.LogInformation("XInputGamepadManager initialized. Starting to poll gamepads.");
        Engine.Instance.AfterEngineCycle += (_) => Instance?.Update();
    }

    private readonly Dictionary<int, XInputGamepadAdapter> _activeAdapters = new();

    public IReadOnlyCollection<XInputGamepadAdapter> ConnectedAdapters => _activeAdapters.Values;

    public static XInputGamepadManager Start() => Instance = new XInputGamepadManager();

    public static void Stop() => Instance = null;

    private void Update()
    {
        for (int i = 0; i < 4; i++)
        {
            bool isConnected = GetState(i, out _) == 0;

            if (isConnected)
            {
                if (!_activeAdapters.ContainsKey(i))
                {
                    _activeAdapters[i] = new XInputGamepadAdapter(i);
                }

                _activeAdapters[i].Poll(); // Keep state fresh
            }
            else
            {
                _activeAdapters.Remove(i); // Implicitly disposes if needed
            }
        }
    }
}
