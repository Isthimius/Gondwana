using Gondwana.Input.Keyboard.WinForms;
using Microsoft.Extensions.Logging;

namespace Gondwana.Input.Keyboard;

/// <summary>
/// Cross-platform singleton keyboard handler with throttling and manual input state feeding.
/// </summary>
public sealed class KeyboardHandler
{
    private readonly Dictionary<string, KeyEventConfiguration> _keyConfigs = new();

    /// <summary>
    /// Singleton instance of the KeyboardHandler.
    /// </summary>
    public static KeyboardHandler Instance { get; private set; }

    /// <summary>
    /// Prevents external instantiation.
    /// </summary>
    private KeyboardHandler() { }

    private KeyboardHandler(IKeyboardAdapter adapter)
    {
        CurrentAdapter = adapter;
    }

    public static void Initialize(IKeyboardAdapter adapter)
    {
        Instance = new KeyboardHandler(adapter);
    }

    /// <summary>
    /// Gets the current keyboard adapter in use.
    /// </summary>
    public IKeyboardAdapter? CurrentAdapter { get; private set; }

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
    public void Update(long tick)
    {
        if (PauseAllKeyEvents || CurrentAdapter is null) return;

        if (CurrentAdapter.PressedKeys.Any())
            Engine.Logger.LogInformation("KeyboardHandler Update: CurrentAdapter.PressedKeys = {PressedKeys}", string.Join(", ", CurrentAdapter.PressedKeys));

        foreach (var kvp in _keyConfigs)
        {
            var key = kvp.Key;
            var config = kvp.Value;
            
            if (config.Paused || !CurrentAdapter.PressedKeys.Contains(key)) continue;

            if (config.ReadyForNextEvent(tick))
            {
                config.LastKeyEvent = tick;
                _keyConfigs[key] = config;
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
