namespace Gondwana.Input.Keyboard;

/// <summary>
/// Cross-platform singleton keyboard handler with throttling and manual input state feeding.
/// </summary>
public sealed class KeyboardEventPoller
{
    /// <summary>
    /// Singleton instance of the <see cref="KeyboardEventPoller"/> class.
    /// </summary>
    public static KeyboardEventPoller? Instance { get; private set; }

    public static void Initialize(IKeyboardAdapter adapter)
    {
        Instance = new KeyboardEventPoller(adapter);
    }

    public event Action<KeyDownEventArgs>? KeyDown;

    private readonly Dictionary<string, KeyEventConfiguration> _keyConfigs = new();

    private KeyboardEventPoller() { }

    private KeyboardEventPoller(IKeyboardAdapter adapter)
    {
        Adapter = adapter;
    }

    /// <summary>
    /// Gets the current keyboard adapter in use.
    /// </summary>
    public IKeyboardAdapter? Adapter { get; private set; }

    /// <summary>
    /// Pauses all key events globally.
    /// </summary>
    public bool PauseAllKeyEvents { get; set; }

    /// <summary>
    /// Updates internal key states and raises throttled key down events.
    /// </summary>
    /// <param name="tick">Current global tick</param>
    /// <param name="keyStates">Set of currently pressed keys (as strings or codes)</param>
    /// <param name="modifiers">Optional modifier state</param>
    internal void PollForEvents(long tick)
    {
        if (PauseAllKeyEvents || Adapter is null) return;

        foreach (var kvp in _keyConfigs)
        {
            var key = kvp.Key;
            var config = kvp.Value;
            
            if (config.Paused || !Adapter.PressedKeys.Contains(key)) continue;

            if (config.ReadyForNextEvent(tick))
            {
                config.LastKeyEvent = tick;
                _keyConfigs[key] = config;

                KeyDown?.Invoke(new KeyDownEventArgs(config, Adapter.CurrentKeyboardModifiers));
            }
        }
    }

    public void StartMonitoringKey(string key, double timeBetweenEvents = -1)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenKeyboardEvents;

        _keyConfigs[key] = new KeyEventConfiguration(key, timeBetweenEvents, false);
    }

    public void StopMonitoringKey(string key) => _keyConfigs.Remove(key);

    public void StopMonitoringAllKeys() => _keyConfigs.Clear();

    public IReadOnlyDictionary<string, KeyEventConfiguration> AllKeyConfigs => _keyConfigs;
}
