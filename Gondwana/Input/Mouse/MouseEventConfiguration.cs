namespace Gondwana.Input.Mouse;

/// <summary>
/// Represents the configuration for mouse event monitoring, including movement tracking settings
/// and timing controls for event throttling. This configuration is used to control how frequently
/// mouse events are raised, whether mouse movement should be tracked, and whether event processing
/// is currently paused.
/// </summary>
public class MouseEventConfiguration : InputEventConfigurationBase
{
    /// <summary>
    /// Gets or sets a value indicating whether mouse cursor movement should be tracked and reported
    /// in mouse events. When enabled, mouse events will include position updates even when no buttons
    /// are pressed, allowing detection of mouse hover, cursor tracking, and movement-based interactions.
    /// When disabled, events may only be raised for button presses, releases, or other explicit actions,
    /// potentially reducing event frequency and processing overhead.
    /// </summary>
    public bool TrackMouseMovement { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MouseEventConfiguration"/> class with the specified
    /// movement tracking and event timing settings.
    /// </summary>
    /// <param name="trackMouseMovement">
    /// A value indicating whether mouse cursor movement should be tracked and reported in events.
    /// Set to <c>true</c> to track all mouse movements including hover, or <c>false</c> to only
    /// track explicit actions like button presses and scrolling.
    /// </param>
    /// <param name="secondsBetweenEvents">
    /// The minimum time interval in seconds that must elapse between consecutive mouse events.
    /// Use this to throttle high-frequency mouse input. A value of 0 means no throttling is applied,
    /// and events will be generated as frequently as the mouse state changes. Default is 0.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for the mouse is initially paused.
    /// When paused, the mouse will not generate events even if moved, clicked, or scrolled.
    /// This is useful for temporarily disabling mouse input, such as when a modal dialog is displayed
    /// or during cutscenes. Default is <c>false</c>.
    /// </param>
    public MouseEventConfiguration(bool trackMouseMovement, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        TrackMouseMovement = trackMouseMovement;
    }
}