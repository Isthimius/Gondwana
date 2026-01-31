namespace Gondwana.Input.Gamepad;

/// <summary>
/// Represents the configuration for gamepad button events, including button identification
/// and timing controls for event throttling. This configuration is used to control how
/// frequently button events are raised and whether event processing is currently paused.
/// </summary>
public class GamepadButtonEventConfiguration : InputEventConfigurationBase
{
    /// <summary>
    /// Gets the identifier of the gamepad button associated with this configuration.
    /// This typically corresponds to standard gamepad button names such as "A", "B", "X", "Y",
    /// "Start", "Back", or directional pad buttons.
    /// </summary>
    public string Button { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GamepadButtonEventConfiguration"/> class
    /// with the specified button identifier and event timing settings.
    /// </summary>
    /// <param name="button">
    /// The identifier of the gamepad button (e.g., "A", "B", "X", "Y", "Start", "Back").
    /// This value is used to associate the configuration with a specific physical button on the gamepad.
    /// </param>
    /// <param name="secondsBetweenEvents">
    /// The minimum time interval in seconds that must elapse between consecutive events
    /// for this button. Use this to throttle rapid button presses. A value of 0 means
    /// no throttling is applied. Default is 0.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for this button is initially paused.
    /// When paused, the button will not generate events even if pressed. Default is false.
    /// </param>
    public GamepadButtonEventConfiguration(string button, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        Button = button;
    }
}