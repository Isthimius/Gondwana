using Microsoft.Extensions.Logging;
using Gondwana.Input.Gamepad;
using static Gondwana.WinForms.Input.Gamepad.XInput;

namespace Gondwana.WinForms.Input.Gamepad;

public sealed class XInputGamepadManager : IGamepadManager<XInputGamepadAdapter>
{
    public static XInputGamepadManager? Instance { get; private set; }

    private XInputGamepadManager()
    {
        Engine.Logger.LogInformation("XInputGamepadManager initialized. Starting to poll gamepads.");
    }

    public static XInputGamepadManager Start() => Instance = new XInputGamepadManager();

    public static void Stop() => Instance = null;

    private readonly Dictionary<int, XInputGamepadAdapter> _activeAdapters = new();

    public IReadOnlyCollection<XInputGamepadAdapter> ConnectedAdapters => _activeAdapters.Values;

    public void Update()
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

                // ** DO NOT CALL THIS UNBOUNDED!! **
                // ** limit to Engine framerate **
                _activeAdapters[i].Poll(); // Keep state fresh
            }
            else
            {
                _activeAdapters.Remove(i); // Implicitly disposes if needed
            }
        }
    }
}
