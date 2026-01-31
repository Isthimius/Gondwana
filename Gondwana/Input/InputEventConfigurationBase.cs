using Gondwana.Timers;

namespace Gondwana.Input;

/// <summary>
/// Provides a base class for input event configuration that implements throttling and pause functionality
/// for various input types (keyboard, mouse, gamepad). This abstract class manages timing between events
/// using high-resolution ticks and tracks the last event occurrence to enable event rate limiting.
/// Derived classes extend this functionality for specific input device types.
/// </summary>
public abstract class InputEventConfigurationBase
{
    /// <summary>
    /// The tick value (from <see cref="HighResTimer"/>) when the last event was raised for this configuration.
    /// This field is used internally to calculate elapsed time and determine when the next event should be allowed
    /// based on the configured throttling interval. Input pollers update this value when events are raised.
    /// </summary>
    protected internal long _lastEventTick;
    private long _ticksBetweenEvents;

    private InputEventConfigurationBase()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputEventConfigurationBase"/> class with the specified
    /// throttling interval and pause state. This constructor sets up the timing mechanism for event rate limiting.
    /// </summary>
    /// <param name="secondsBetweenEvents">
    /// The minimum time interval in seconds that must elapse between consecutive events.
    /// This value is converted to high-resolution ticks internally for precise timing.
    /// A value of 0 means no throttling is applied, allowing events to fire as frequently as input occurs.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing is initially paused. When paused, events will not be
    /// raised even if input occurs and timing conditions are met. This provides a way to temporarily
    /// disable input without removing the configuration.
    /// </param>
    protected InputEventConfigurationBase(double secondsBetweenEvents, bool isPaused)
    {
        _lastEventTick = 0;
        _ticksBetweenEvents = (long)(secondsBetweenEvents * HighResTimer.TicksPerSecond);
        IsPaused = isPaused;
    }

    /// <summary>
    /// Gets or sets the minimum time interval in seconds that must elapse between consecutive events.
    /// This property controls the throttling rate for input events, preventing excessive event generation
    /// from high-frequency input sources. The value is stored internally as high-resolution ticks for
    /// precise timing calculations.
    /// </summary>
    /// <value>
    /// The time interval in seconds. A value of 0 means no throttling is applied, and events can be
    /// raised as frequently as input changes occur. Higher values reduce event frequency, which can be
    /// useful for rate-limiting button repeat events or reducing processing overhead from rapid input.
    /// </value>
    public double TimeBetweenEvents
    {
        get => (double)_ticksBetweenEvents / HighResTimer.TicksPerSecond;
        set => _ticksBetweenEvents = (long)(value * HighResTimer.TicksPerSecond);
    }

    /// <summary>
    /// Gets or sets a value indicating whether event processing is currently paused.
    /// When set to <c>true</c>, events will not be raised even if input occurs and timing conditions are met.
    /// This property provides a convenient way to temporarily disable input event generation without
    /// removing or reconfiguring the event monitoring setup, useful for modal dialogs, cutscenes,
    /// or transitioning between game states.
    /// </summary>
    /// <value>
    /// <c>true</c> if event processing is paused and events should not be raised; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    public bool IsPaused { get; set; } = false;

    internal bool ReadyForNextEvent(long tick) => tick - _lastEventTick >= _ticksBetweenEvents;
}