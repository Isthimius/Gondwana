using Gondwana.Timers;

namespace Gondwana.Input.Keyboard;

/// <summary>
/// Cross-platform keyboard handler with throttling and manual input state feeding.
/// </summary>
public sealed class KeyboardHandler
{
    private readonly Dictionary<string, KeyEventConfiguration> _keyConfigs = new();
    private bool _allPause;

    public event Action<KeyDownEventArgs>? KeyDown;

    public long DefaultTicksBetweenKeyEvents { get; internal set; } = 0;

    public bool PauseAllKeyEvents
    {
        get => _allPause;
        set => _allPause = value;
    }

    /// <summary>
    /// Updates internal key states and raises throttled key down events.
    /// </summary>
    /// <param name="tick">Current global tick</param>
    /// <param name="keyStates">Set of currently pressed keys (as strings or codes)</param>
    /// <param name="modifiers">Optional modifier state</param>
    public void Update(long tick, HashSet<string> keyStates, ModifierState? modifiers = null)
    {
        if (_allPause || KeyDown is null) return;

        foreach (var kvp in _keyConfigs)
        {
            var key = kvp.Key;
            var config = kvp.Value;

            if (config.Paused || !keyStates.Contains(key)) continue;

            if (config.ReadyForNextEvent(tick))
            {
                config.LastKeyEvent = tick;
                _keyConfigs[key] = config;

                KeyDown?.Invoke(new KeyDownEventArgs(config, modifiers ?? ModifierState.None));
            }
        }
    }

    public void StartMonitoringKey(string key, double timeBetweenEvents = -1)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = (double)DefaultTicksBetweenKeyEvents / HighResTimer.TicksPerSecond;

        _keyConfigs[key] = new KeyEventConfiguration(key, timeBetweenEvents, false);
    }

    public void StopMonitoringKey(string key) => _keyConfigs.Remove(key);

    public void StopMonitoringAllKeys() => _keyConfigs.Clear();

    public void SetKeyEventPause(string key, bool paused)
    {
        if (_keyConfigs.TryGetValue(key, out var config))
        {
            config.Paused = paused;
            _keyConfigs[key] = config;
        }
    }

    public void SetTimeBetweenEvents(string key, double timeBetweenEvents)
    {
        if (_keyConfigs.TryGetValue(key, out var config))
        {
            config.TimeBetweenEvents = timeBetweenEvents;
            _keyConfigs[key] = config;
        }
    }
}
