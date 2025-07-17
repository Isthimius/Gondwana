namespace Gondwana.Input.Gamepad;

public sealed class GamepadManagerEventPoller
{
    private readonly Dictionary<string, Dictionary<string, GamepadButtonEventConfiguration>> _configsByGamepadId = new();
    
    /// <summary>
    /// Gets the singleton instance of the <see cref="GamepadManagerEventPoller"/> class.
    /// </summary>
    public static GamepadManagerEventPoller? Instance { get; private set; } = new();

    private GamepadManagerEventPoller() { }

    private GamepadManagerEventPoller(IEnumerable<IGamepadAdapter>? adapters)
    {
        _configsByGamepadId.Clear();

        Adapters = adapters;

        foreach (var adapter in adapters)
        {
            _configsByGamepadId[adapter.GamepadId] = new Dictionary<string, GamepadButtonEventConfiguration>();
        }
    }

    public static void Initialize(IEnumerable<IGamepadAdapter>? adapters)
    {
        Instance = new GamepadManagerEventPoller(adapters);
    }

    public IEnumerable<IGamepadAdapter>? Adapters { get; private set; }

    public bool PauseAllInput { get; set; }

    public event Action<GamepadButtonDownEventArgs>? ButtonDown;

    /// <summary>
    /// Polls the provided gamepad adapters for button and trigger states and raises events.
    /// </summary>
    /// <remarks>This method iterates through the provided gamepad adapters and checks for button press events
    /// based on their configurations. If a button press is detected and the configuration is ready for the next event,
    /// the <see cref="ButtonDown"/> event is invoked. The method respects global input pause settings and individual
    /// button configuration pause states.</remarks>
    /// <param name="tick">The current tick value, used to determine event timing and readiness.</param>
    /// <param name="adapters">A collection of gamepad adapters to poll for button press events.</param>
    public void PollForEvents(long tick)
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

                if (config.Paused || !adapter.PressedButtons.Contains(button)) continue;

                if (config.ReadyForNextEvent(tick))
                {
                    config.LastEventTick = tick;
                    configs[button] = config;

                    ButtonDown?.Invoke(new GamepadButtonDownEventArgs(config, adapter));
                }
            }
        }
    }

    public void StartMonitoringButton(string gamepadId, string button, double timeBetweenEvents = -1)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenGamepadEvents;

        if (!_configsByGamepadId.TryGetValue(gamepadId, out var configMap))
        {
            configMap = new Dictionary<string, GamepadButtonEventConfiguration>();
            _configsByGamepadId[gamepadId] = configMap;
        }

        configMap[button] = new GamepadButtonEventConfiguration(button, timeBetweenEvents, false);
    }

    public void StopMonitoringButton(string gamepadId, string button)
    {
        if (_configsByGamepadId.TryGetValue(gamepadId, out var configMap))
        {
            configMap.Remove(button);
        }
    }

    public void StopMonitoringAllButtons(string gamepadId)
    {
        _configsByGamepadId.Remove(gamepadId);
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, GamepadButtonEventConfiguration>> AllButtonConfigsByGamepadId
        => _configsByGamepadId.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyDictionary<string, GamepadButtonEventConfiguration>)entry.Value);
}
