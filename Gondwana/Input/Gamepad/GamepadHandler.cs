using Gondwana.Timers;

namespace Gondwana.Input.Gamepad;

public sealed class GamepadHandler
{
    public static GamepadHandler Instance { get; } = new();
    private GamepadHandler() { }

    private readonly Dictionary<string, GamepadButtonEventConfiguration> _buttonConfigs = new();
    private bool _paused;

    public long DefaultTicksBetweenEvents { get; set; } = HighResTimer.TicksPerSecond / 10;
    public event Action<GamepadButtonDownEventArgs>? ButtonDown;

    public void Update(long tick, IGamepadAdapter? adapter = null)
    {
        if (_paused || ButtonDown is null || adapter is null)
            return;

        foreach (var kvp in _buttonConfigs)
        {
            var button = kvp.Key;
            var config = kvp.Value;

            if (config.Paused || !adapter.PressedButtons.Contains(button)) continue;

            if (config.ReadyForNextEvent(tick))
            {
                config.LastEventTick = tick;
                _buttonConfigs[button] = config;

                ButtonDown?.Invoke(new GamepadButtonDownEventArgs(config, adapter));
            }
        }
    }

    public void StartMonitoringButton(string button, double timeBetweenEvents = -1)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = (double)DefaultTicksBetweenEvents / HighResTimer.TicksPerSecond;

        _buttonConfigs[button] = new GamepadButtonEventConfiguration(button, timeBetweenEvents, false);
    }

    public void StopMonitoringButton(string button) => _buttonConfigs.Remove(button);
}
