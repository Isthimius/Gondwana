using System.Collections.Concurrent;
using System.Collections.ObjectModel;

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

    // Hot path uses int key codes.
    private readonly Dictionary<int, KeyEventConfiguration> _keyConfigs = new();
    private readonly Dictionary<int, bool> _previousPressed = new();

    // Apply monitoring changes on the engine thread to avoid ToList() allocations.
    private readonly ConcurrentQueue<Action> _pendingOps = new();

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
    /// Emits platform-agnostic KeyAction (Pressed / Released / Repeated).
    /// </summary>
    /// <param name="tick">Current global tick</param>
    internal void PollForEvents(long tick)
    {
        if (PauseAllKeyEvents)
            return;

        var adapter = Adapter;
        if (adapter is null)
            return;

        // Apply any pending adds/removes on the engine thread.
        while (_pendingOps.TryDequeue(out var op))
            op();

        var mods = adapter.CurrentKeyboardModifiers;

        foreach (var kvp in _keyConfigs)
        {
            int keyCode = kvp.Key;
            var config = kvp.Value;

            bool currentlyPressed = adapter.IsDown(keyCode);
            bool previouslyPressed = _previousPressed.TryGetValue(keyCode, out var prev) && prev;

            // Transition: not pressed -> pressed
            if (currentlyPressed && !previouslyPressed)
            {
                _previousPressed[keyCode] = true;
                config._lastEventTick = tick;

                KeyDown?.Invoke(new KeyDownEventArgs(config, mods, KeyAction.Pressed));
                continue;
            }

            // Still pressed -> maybe repeated (throttled)
            if (currentlyPressed && previouslyPressed)
            {
                if (!config.IsPaused && config.ReadyForNextEvent(tick))
                {
                    config._lastEventTick = tick;
                    KeyDown?.Invoke(new KeyDownEventArgs(config, mods, KeyAction.Repeated));
                }
                continue;
            }

            // Transition: pressed -> not pressed
            if (!currentlyPressed && previouslyPressed)
            {
                _previousPressed[keyCode] = false;
                KeyDown?.Invoke(new KeyDownEventArgs(config, mods, KeyAction.Released));
            }

            // not pressed && not previously pressed => nothing
        }
    }

    /// <summary>
    /// Primary API: monitor by platform-agnostic key code.
    /// Platform layers (WinForms, SDL, etc.) are responsible for mapping their key enums/names to key codes.
    /// </summary>
    public void StartMonitoringKey(int keyCode, string? displayName = null, double timeBetweenEvents = -1, bool isPaused = false)
    {
        _pendingOps.Enqueue(() =>
        {
            if (timeBetweenEvents < 0)
                timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenKeyboardEvents;

            var name = string.IsNullOrWhiteSpace(displayName) ? keyCode.ToString() : displayName;
            var config = new KeyEventConfiguration(name, timeBetweenEvents, isPaused);

            _keyConfigs[keyCode] = config;

            var adapter = Adapter;
            _previousPressed[keyCode] = adapter?.IsDown(keyCode) ?? false;
        });
    }

    public void StopMonitoringKey(int keyCode)
    {
        _pendingOps.Enqueue(() =>
        {
            _keyConfigs.Remove(keyCode);
            _previousPressed.Remove(keyCode);
        });
    }

    public void StopMonitoringKey(string key)
    {
        if (int.TryParse(key, out int code))
            StopMonitoringKey(code);
    }

    public void StopMonitoringAllKeys()
    {
        _pendingOps.Enqueue(() =>
        {
            _keyConfigs.Clear();
            _previousPressed.Clear();
        });
    }

    /// <summary>
    /// Exposes monitored key configurations. Treat as engine-thread-only unless you add external synchronization.
    /// </summary>
    public IReadOnlyDictionary<int, KeyEventConfiguration> AllKeyConfigs =>
        new ReadOnlyDictionary<int, KeyEventConfiguration>(_keyConfigs);
}
