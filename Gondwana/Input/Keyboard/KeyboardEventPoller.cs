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

    // case-insensitive keys
    private readonly Dictionary<string, KeyEventConfiguration> _keyConfigs = new(StringComparer.OrdinalIgnoreCase);

    // track previous pressed state for each monitored key so we can detect transitions
    private readonly Dictionary<string, bool> _previousPressed = new(StringComparer.OrdinalIgnoreCase);

    private KeyboardEventPoller()
    { }

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
    /// Emits platform-agnostic KeyAction (Pressed / Released / Repeated).
    /// </summary>
    /// <param name="tick">Current global tick</param>
    internal void PollForEvents(long tick)
    {
        if (PauseAllKeyEvents || Adapter is null)
            return;

        foreach (var kvp in _keyConfigs.ToList())
        {
            var key = kvp.Key;
            var config = kvp.Value;

            if (Adapter.PressedKeys == null)
                return;

            bool currentlyPressed = Adapter.PressedKeys.Contains(key);
            bool previouslyPressed = _previousPressed.TryGetValue(key, out var prev) && prev;

            // Transition: not pressed -> pressed (initial KeyDown)
            if (currentlyPressed && !previouslyPressed)
            {
                _previousPressed[key] = true;
                config._lastEventTick = tick;
                _keyConfigs[key] = config;
                KeyDown?.Invoke(new KeyDownEventArgs(config, Adapter.CurrentKeyboardModifiers, KeyAction.Pressed));
                continue;
            }

            // Still pressed -> may produce Repeated events (throttled)
            if (currentlyPressed && previouslyPressed)
            {
                if (!config.IsPaused && config.ReadyForNextEvent(tick))
                {
                    config._lastEventTick = tick;
                    _keyConfigs[key] = config;
                    KeyDown?.Invoke(new KeyDownEventArgs(config, Adapter.CurrentKeyboardModifiers, KeyAction.Repeated));
                }
                continue;
            }

            // Transition: pressed -> not pressed (key release)
            if (!currentlyPressed && previouslyPressed)
            {
                _previousPressed[key] = false;
                // Releases should be delivered immediately (not throttled)
                KeyDown?.Invoke(new KeyDownEventArgs(config, Adapter.CurrentKeyboardModifiers, KeyAction.Released));
                continue;
            }

            // not pressed && not previously pressed => nothing
        }
    }

    public void StartMonitoringKey(string key, double timeBetweenEvents = -1, bool isPaused = false)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenKeyboardEvents;

        _keyConfigs[key] = new KeyEventConfiguration(key, timeBetweenEvents, isPaused);

        // initialize previous state from current adapter state if available
        _previousPressed[key] = Adapter?.PressedKeys.Contains(key) ?? false;
    }

    public void StopMonitoringKey(string key)
    {
        _keyConfigs.Remove(key);
        _previousPressed.Remove(key);
    }

    public void StopMonitoringAllKeys()
    {
        _keyConfigs.Clear();
        _previousPressed.Clear();
    }

    public IReadOnlyDictionary<string, KeyEventConfiguration> AllKeyConfigs => _keyConfigs;
}