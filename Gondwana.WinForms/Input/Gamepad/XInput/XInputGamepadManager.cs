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
    /// Checks the four XInput controller slots for connection and disconnection changes.
    /// </summary>
    public void UpdateConnections()
    {
        for (int i = 0; i < 4; i++)
        {
            bool isConnected = GetState(i, out _) == 0;

            if (isConnected)
            {
                if (!_activeAdapters.ContainsKey(i))
                    _activeAdapters[i] = new XInputGamepadAdapter(i);
            }
            else
            {
                _activeAdapters.Remove(i);
            }
        }
    }

    /// <summary>
    /// Polls the current state of all connected XInput controllers.
    /// </summary>
    public void Poll()
    {
        foreach (var adapter in _activeAdapters.Values)
            adapter.Poll();
    }

    /// <summary>
    /// Performs the legacy combined connection and state refresh.
    /// </summary>
    [Obsolete("Use UpdateConnections() and Poll() separately.")]
    public void Update()
    {
        UpdateConnections();
        Poll();
    }
}
