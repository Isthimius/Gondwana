namespace Gondwana.Input.Gamepad;

/// <summary>
/// Provides centralized polling and event management for gamepad button inputs across multiple gamepad devices.
/// This singleton class monitors registered buttons on connected gamepads and raises events when buttons are pressed,
/// with support for event throttling and pause states at both global and per-button levels.
/// </summary>
public sealed class GamepadEventPoller
{
    private readonly Dictionary<string, Dictionary<string, GamepadButtonEventConfiguration>> _configsByGamepadId = new();

    /// <summary>
    /// Gets the singleton instance of the <see cref="GamepadEventPoller"/> class.
    /// This instance is created automatically on first access and can be reinitialized using the
    /// <see cref="Initialize(IEnumerable{IGamepadAdapter}?)"/> method.
    /// </summary>
    public static GamepadEventPoller? Instance { get; private set; } = new();

    private GamepadEventPoller()
    { }

    private GamepadEventPoller(IEnumerable<IGamepadAdapter>? adapters)
    {
        _configsByGamepadId.Clear();

        Adapters = adapters;
        if (adapters is not null)
        {
            foreach (var adapter in adapters)
            {
                _configsByGamepadId[adapter.GamepadId] = new Dictionary<string, GamepadButtonEventConfiguration>();
            }
        }
    }

    /// <summary>
    /// Initializes the singleton instance of the <see cref="GamepadEventPoller"/> with the specified gamepad adapters.
    /// This method replaces the existing instance and configures it to monitor the provided adapters.
    /// </summary>
    /// <param name="adapters">
    /// A collection of gamepad adapters representing the connected gamepad devices to be monitored.
    /// Pass <c>null</c> to create an instance without any adapters.
    /// </param>
    public static void Initialize(IEnumerable<IGamepadAdapter>? adapters)
    {
        Instance = new GamepadEventPoller(adapters);
    }

    /// <summary>
    /// Gets the collection of gamepad adapters currently being monitored by this poller.
    /// Each adapter represents a physical or virtual gamepad device and provides access to its current state.
    /// </summary>
    public IEnumerable<IGamepadAdapter>? Adapters { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether all input processing is globally paused.
    /// When set to <c>true</c>, the poller will not raise any button events regardless of individual
    /// button configuration pause states. This provides a convenient way to temporarily disable
    /// all gamepad input, such as when a game is paused or a modal dialog is displayed.
    /// </summary>
    public bool PauseAllInput { get; set; }

    /// <summary>
    /// Occurs when a monitored gamepad button is pressed and the button's configuration allows
    /// the event to be raised based on throttling settings. Subscribe to this event to handle
    /// gamepad button down input in your application.
    /// </summary>
    public event Action<GamepadButtonDownEventArgs>? ButtonDown;

    /// <summary>
    /// Polls the configured gamepad adapters for button press events and raises the <see cref="ButtonDown"/>
    /// event for buttons that are currently pressed and ready to generate events based on their configurations.
    /// </summary>
    /// <remarks>
    /// This method should be called regularly (typically once per frame or game tick) to ensure timely
    /// detection of button presses. It iterates through all configured gamepad adapters and checks each
    /// monitored button against its configuration. Events are only raised if:
    /// <list type="bullet">
    /// <item><description><see cref="PauseAllInput"/> is <c>false</c></description></item>
    /// <item><description>The button's configuration is not paused</description></item>
    /// <item><description>The button is currently pressed according to the adapter</description></item>
    /// <item><description>Sufficient time has elapsed since the last event for that button (based on throttling settings)</description></item>
    /// </list>
    /// </remarks>
    /// <param name="tick">
    /// The current game tick or timestamp value, used to calculate elapsed time for event throttling.
    /// This value should be monotonically increasing to ensure correct timing behavior.
    /// </param>
    internal void PollForEvents(long tick)
    {
        if (PauseAllInput || ButtonDown is null || Adapters is null) return;

        foreach (var adapter in Adapters)
        {
            if (!_configsByGamepadId.TryGetValue(adapter.GamepadId, out var configs))
                continue;

            foreach (var kvp in configs)
            {
                var button = kvp.Key;
                var config = kvp.Value;

                if (config.IsPaused || !adapter.PressedButtons.Contains(button)) continue;

                if (config.ReadyForNextEvent(tick))
                {
                    config._lastEventTick = tick;
                    configs[button] = config;

                    ButtonDown?.Invoke(new GamepadButtonDownEventArgs(config, adapter));
                }
            }
        }
    }

    /// <summary>
    /// Registers a button on a specific gamepad for event monitoring with optional throttling and pause configuration.
    /// Once registered, the button will be polled during calls to <see cref="PollForEvents(long)"/> and generate
    /// <see cref="ButtonDown"/> events when pressed.
    /// </summary>
    /// <param name="gamepadId">
    /// The unique identifier of the gamepad device on which the button should be monitored.
    /// This must match the <see cref="IGamepadAdapter.GamepadId"/> of one of the configured adapters.
    /// </param>
    /// <param name="button">
    /// The identifier of the button to monitor (e.g., "A", "B", "X", "Y", "Start", "Back").
    /// This should match the button naming convention used by the gamepad adapter.
    /// </param>
    /// <param name="timeBetweenEvents">
    /// The minimum time interval in seconds between consecutive events for this button.
    /// Use this to throttle rapid button presses. A value of -1 (default) will use the engine's
    /// default time between gamepad events from <see cref="Engine.Configuration.TimeBetweenGamepadEvents"/>.
    /// A value of 0 means no throttling.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for this button should be initially paused.
    /// When paused, the button will not generate events even if pressed. Default is <c>false</c>.
    /// </param>
    public void StartMonitoringButton(string gamepadId, string button, double timeBetweenEvents = -1, bool isPaused = false)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenGamepadEvents;

        if (!_configsByGamepadId.TryGetValue(gamepadId, out var configMap))
        {
            configMap = new Dictionary<string, GamepadButtonEventConfiguration>();
            _configsByGamepadId[gamepadId] = configMap;
        }

        configMap[button] = new GamepadButtonEventConfiguration(button, timeBetweenEvents, isPaused);
    }

    /// <summary>
    /// Unregisters a specific button on a gamepad from event monitoring.
    /// After calling this method, the button will no longer be polled and will not generate
    /// <see cref="ButtonDown"/> events until it is registered again using <see cref="StartMonitoringButton"/>.
    /// </summary>
    /// <param name="gamepadId">
    /// The unique identifier of the gamepad device from which the button should be removed.
    /// </param>
    /// <param name="button">
    /// The identifier of the button to stop monitoring.
    /// </param>
    public void StopMonitoringButton(string gamepadId, string button)
    {
        if (_configsByGamepadId.TryGetValue(gamepadId, out var configMap))
        {
            configMap.Remove(button);
        }
    }

    /// <summary>
    /// Unregisters all buttons on a specific gamepad from event monitoring.
    /// This effectively removes all button configurations for the specified gamepad,
    /// and no buttons on that gamepad will generate events until they are registered again.
    /// </summary>
    /// <param name="gamepadId">
    /// The unique identifier of the gamepad device for which all button monitoring should be stopped.
    /// </param>
    public void StopMonitoringAllButtons(string gamepadId)
    {
        _configsByGamepadId.Remove(gamepadId);
    }

    /// <summary>
    /// Gets a read-only view of all button configurations organized by gamepad ID.
    /// The outer dictionary maps gamepad IDs to inner dictionaries, where each inner dictionary
    /// maps button identifiers to their respective <see cref="GamepadButtonEventConfiguration"/> instances.
    /// This property is useful for inspecting the current monitoring state of all gamepads and buttons.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, GamepadButtonEventConfiguration>> AllButtonConfigsByGamepadId
        => _configsByGamepadId.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyDictionary<string, GamepadButtonEventConfiguration>)entry.Value);
}