using Gondwana.Timers;

namespace Gondwana.Input.Gamepad;

public sealed class GamepadHandler
{
    public static GamepadHandler Instance { get; } = new();
    private GamepadHandler() { }

    private readonly Dictionary<string, Dictionary<string, GamepadButtonEventConfiguration>> _configsByGamepadId = new();

    public long DefaultTicksBetweenEvents { get; set; } = HighResTimer.TicksPerSecond / 10;
    public event Action<GamepadButtonDownEventArgs>? ButtonDown;

    public bool PauseAllInput { get; set; }

    public void Update(long tick, IEnumerable<IGamepadAdapter> adapters)
    {
        if (PauseAllInput || ButtonDown is null || adapters is null) return;

        foreach (var adapter in adapters)
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
