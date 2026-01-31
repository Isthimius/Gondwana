namespace Gondwana.Input.Gamepad;

/// <summary>
/// Provides data for gamepad button down events, containing information about the button configuration
/// and the gamepad adapter that generated the event.
/// </summary>
public sealed class GamepadButtonDownEventArgs : EventArgs
{
    /// <summary>
    /// Gets the configuration details for the gamepad button that was pressed, including
    /// button identification and any associated event configuration settings.
    /// </summary>
    public GamepadButtonEventConfiguration Config { get; }

    /// <summary>
    /// Gets the gamepad adapter instance that detected and raised the button down event,
    /// providing access to the current state of the gamepad device.
    /// </summary>
    public IGamepadAdapter Adapter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GamepadButtonDownEventArgs"/> class
    /// with the specified button configuration and gamepad adapter.
    /// </summary>
    /// <param name="config">The configuration details for the button that was pressed.</param>
    /// <param name="adapter">The gamepad adapter that detected the button down event.</param>
    public GamepadButtonDownEventArgs(GamepadButtonEventConfiguration config, IGamepadAdapter adapter)
    {
        Config = config;
        Adapter = adapter;
    }
}