using Gondwana.Input.Gamepad;
using Microsoft.Extensions.Logging;
using static Gondwana.WinForms.Input.Gamepad.XInput.XInput;

namespace Gondwana.WinForms.Input.Gamepad.XInput;

/// <summary>
/// Manages XInput-based gamepad connections and state updates for Xbox controllers on Windows.
/// </summary>
public sealed class XInputGamepadManager : IGamepadManager<XInputGamepadAdapter>
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="XInputGamepadManager"/>, if it has been started.
    /// </summary>
    public static XInputGamepadManager? Instance { get; private set; }

    private XInputGamepadManager()
    {
        Engine.Logger.LogInformation("XInputGamepadManager initialized. Starting to poll gamepads.");
    }

    /// <summary>
    /// Starts the XInput gamepad manager and returns the singleton instance.
    /// </summary>
    /// <returns>The singleton <see cref="XInputGamepadManager"/> instance.</returns>
    public static XInputGamepadManager Start()
    {
        if (Instance is not null)
            return Instance;

        return Instance = new XInputGamepadManager();
    }

    /// <summary>
    /// Stops the XInput gamepad manager and clears the singleton instance.
    /// </summary>
    public static void Stop()
    {
        Engine.Logger.LogInformation("Stopping XInputGamepadManager and removing all XInputGamepadAdapter instances.");
        Instance = null;
    }

    private readonly Dictionary<int, XInputGamepadAdapter> _activeAdapters = new();

    /// <summary>
    /// Gets the collection of currently connected XInput gamepad adapters.
    /// </summary>
    public IReadOnlyCollection<XInputGamepadAdapter> ConnectedAdapters => _activeAdapters.Values;

    /// <summary>
    /// Updates the gamepad manager by polling all XInput controller slots (0-3) for connection status and state changes.
    /// </summary>
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