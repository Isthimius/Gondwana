namespace Gondwana.Input.Touch;

/// <summary>
/// Represents the configuration for touch event monitoring, including timing controls for
/// event throttling and pause functionality. This configuration is used by
/// <see cref="TouchEventPoller"/> to control how frequently touch events are raised and
/// whether event processing is currently paused.
/// </summary>
public class TouchEventConfiguration : InputEventConfigurationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TouchEventConfiguration"/> class with the
    /// specified event timing and pause settings.
    /// </summary>
    /// <param name="secondsBetweenEvents">
    /// The minimum time interval in seconds that must elapse between consecutive touch events.
    /// Use this to throttle high-frequency touch input. A value of 0 means no throttling is applied,
    /// and events will be generated as frequently as the touch state changes. Default is 0.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for touch is initially paused.
    /// When paused, touch will not generate events even if contacts change.
    /// This is useful for temporarily disabling touch input, such as during cutscenes.
    /// Default is <c>false</c>.
    /// </param>
    public TouchEventConfiguration(double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
    }
}
