namespace Gondwana.Input.Keyboard;

/// <summary>
/// Represents the configuration for keyboard key events, including key identification and timing controls
/// for event throttling. This configuration is used to control how frequently key events are raised
/// and whether event processing is currently paused for a specific key.
/// </summary>
public class KeyEventConfiguration : InputEventConfigurationBase
{
    /// <summary>
    /// Gets the identifier of the keyboard key associated with this configuration.
    /// This can be a key name such as "A", "Enter", "ArrowUp", "Escape", "Space", or any other
    /// platform-specific key identifier. The exact format depends on the keyboard adapter implementation
    /// but typically uses human-readable key names for display and debugging purposes.
    /// </summary>
    public string Key { get; private set; } // Could be "A", "Enter", "ArrowUp", etc.

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyEventConfiguration"/> class with the specified
    /// key identifier and event timing settings.
    /// </summary>
    /// <param name="key">
    /// The identifier of the keyboard key (e.g., "A", "Enter", "ArrowUp", "Escape").
    /// This value is used to identify the key in event data and for display purposes.
    /// The format should match the key naming convention used by the keyboard adapter.
    /// </param>
    /// <param name="secondsBetweenEvents">
    /// The minimum time interval in seconds that must elapse between consecutive events for this key.
    /// Use this to throttle rapid key presses or key repeat events. A value of 0 means no throttling
    /// is applied, and events will be generated as frequently as the key state changes or repeats.
    /// Default is 0.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for this key is initially paused.
    /// When paused, the key will not generate events even if pressed or held down.
    /// This is useful for temporarily disabling specific key bindings without removing their configuration.
    /// Default is false.
    /// </param>
    public KeyEventConfiguration(string key, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        Key = key;
    }

    /// <summary>
    /// Returns a string representation of this key event configuration, including the key identifier,
    /// throttling interval, and pause state. This is useful for debugging and logging purposes.
    /// </summary>
    /// <returns>
    /// A string in the format "KeyEventConfiguration: Key={Key}, TimeBetweenEvents={TimeBetweenEvents}, IsPaused={IsPaused}"
    /// providing a human-readable summary of the configuration state.
    /// </returns>
    public override string ToString()
    {
        return $"KeyEventConfiguration: Key={Key}, TimeBetweenEvents={TimeBetweenEvents}, IsPaused={IsPaused}";
    }
}