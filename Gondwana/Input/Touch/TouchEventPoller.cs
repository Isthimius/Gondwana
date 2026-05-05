namespace Gondwana.Input.Touch;

/// <summary>
/// Provides centralized polling and event management for touch input. This singleton class
/// monitors touch adapter state each engine frame, detects contact transitions, and raises
/// events with comprehensive touch point information, with support for event throttling and
/// pause functionality.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TouchEventPoller"/> implements <see cref="ITouchInput"/> so that all gesture
/// recognizers (<c>TapGestureRecognizer</c>, <c>SwipeGestureRecognizer</c>,
/// <c>PinchGestureRecognizer</c>) work unchanged.
/// </para>
/// <para>
/// The adapter (<see cref="ITouchAdapter"/>) is a passive state holder updated by platform events
/// on the UI thread. The poller is called by the engine each frame to diff state, detect
/// transitions, and raise events on the engine thread.
/// </para>
/// </remarks>
public sealed class TouchEventPoller : ITouchInput
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="TouchEventPoller"/> class.
    /// This instance is created through the <see cref="Initialize"/> method and provides
    /// centralized access to touch event polling functionality throughout the application.
    /// </summary>
    public static TouchEventPoller? Instance { get; private set; }

    /// <summary>
    /// Initializes the singleton instance of the <see cref="TouchEventPoller"/> with the specified
    /// touch adapter and optional configuration. This method replaces the existing instance and
    /// configures it to monitor touch input through the provided adapter.
    /// </summary>
    /// <param name="adapter">
    /// The touch adapter that provides access to the current touch state including active contacts
    /// and ended contacts. This adapter abstracts platform-specific touch input handling.
    /// </param>
    /// <param name="config">
    /// Optional configuration for touch event monitoring. If <c>null</c>, a default configuration
    /// is created using <see cref="Engine.Configuration"/>'s
    /// <see cref="Gondwana.Configuration.EngineConfiguration.TimeBetweenTouchEvents"/> as the
    /// throttle interval.
    /// </param>
    public static void Initialize(ITouchAdapter adapter, TouchEventConfiguration? config = null)
    {
        config ??= new TouchEventConfiguration(
            secondsBetweenEvents: Engine.Instance.Configuration.TimeBetweenTouchEvents);
        Instance = new TouchEventPoller(adapter, config);
    }

    private readonly Dictionary<int, TouchPoint> _activeTouches = new();
    private TouchPoint[] _activeTouchesSnapshot = Array.Empty<TouchPoint>();

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ActiveTouches => _activeTouchesSnapshot;

    /// <summary>
    /// Gets the touch adapter currently in use, which provides access to the underlying touch
    /// state including active contacts and ended contacts.
    /// Returns <c>null</c> if no adapter has been configured.
    /// </summary>
    public ITouchAdapter? Adapter { get; private set; }

    /// <summary>
    /// Gets the current touch event configuration, which controls throttling interval and pause state.
    /// Returns <c>null</c> if touch monitoring has been stopped or not yet configured.
    /// </summary>
    public TouchEventConfiguration? Configuration { get; private set; }

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchBegan;

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchMoved;

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchEnded;

    private TouchEventPoller(ITouchAdapter adapter, TouchEventConfiguration config)
    {
        Adapter = adapter;
        Configuration = config;
    }

    /// <summary>
    /// Polls the touch adapter for input state changes and raises touch events when appropriate
    /// based on the configuration settings. This method is called once per engine frame.
    /// </summary>
    /// <remarks>
    /// Each call performs the following steps:
    /// <list type="number">
    /// <item><description>
    /// Drains <see cref="ITouchAdapter.ConsumeEndedTouches"/>, fires <see cref="TouchEnded"/>
    /// for each known contact, and removes it from tracked state.
    /// </description></item>
    /// <item><description>
    /// Compares <see cref="ITouchAdapter.ActiveTouches"/> against the last-known snapshot:
    /// new IDs fire <see cref="TouchBegan"/>; same ID with a moved position fires <see cref="TouchMoved"/>.
    /// </description></item>
    /// <item><description>Updates the internal active-contact snapshot.</description></item>
    /// </list>
    /// Events are only raised when the configuration is not paused and sufficient time has elapsed
    /// since the last event based on throttling settings.
    /// </remarks>
    /// <param name="tick">
    /// The current engine tick value, used to calculate elapsed time for event throttling.
    /// This value should be monotonically increasing to ensure correct timing behaviour.
    /// </param>
    internal void PollForEvents(long tick)
    {
        if (Adapter is null) return;
        if (Configuration is null || Configuration.IsPaused || !Configuration.ReadyForNextEvent(tick)) return;

        // Step 1: drain ended/cancelled contacts and fire TouchEnded
        var ended = Adapter.ConsumeEndedTouches();
        foreach (var endedPoint in ended)
        {
            if (_activeTouches.Remove(endedPoint.Id))
            {
                TouchEnded?.Invoke(this, new TouchEventArgs(endedPoint));
            }
        }

        // Step 2: diff active contacts against last-known snapshot
        var currentActive = Adapter.ActiveTouches;
        foreach (var point in currentActive)
        {
            if (!_activeTouches.TryGetValue(point.Id, out var known))
            {
                // New contact
                _activeTouches[point.Id] = point;
                TouchBegan?.Invoke(this, new TouchEventArgs(point));
            }
            else if (known.Position != point.Position)
            {
                // Existing contact moved. The poller owns phase semantics for transition events,
                // so TouchPhase.Moved is always correct here regardless of the adapter's stored phase.
                var movedPoint = new TouchPoint(point.Id, point.Position, TouchPhase.Moved);
                _activeTouches[point.Id] = movedPoint;
                TouchMoved?.Invoke(this, new TouchEventArgs(movedPoint));
            }
        }

        // Step 3: update snapshot and throttle tick
        _activeTouchesSnapshot = _activeTouches.Values.ToArray();
        Configuration._lastEventTick = tick;
    }

    /// <summary>
    /// Starts or reconfigures touch input monitoring with the specified settings. This method
    /// creates a new configuration that controls event throttling and pause state.
    /// Call this method to begin monitoring touch input or to change monitoring settings.
    /// </summary>
    /// <param name="timeBetweenEvents">
    /// The minimum time interval in seconds between consecutive touch events. A value of 0
    /// (default) means no throttling.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for touch should be initially paused.
    /// When paused, touch will not generate events even if contacts change.
    /// Default is <c>false</c>.
    /// </param>
    public void StartMonitoringTouch(double timeBetweenEvents = 0, bool isPaused = false)
    {
        Configuration = new TouchEventConfiguration(timeBetweenEvents, isPaused);
    }

    /// <summary>
    /// Stops monitoring touch input by clearing the configuration. After calling this method,
    /// touch events will no longer be raised until <see cref="StartMonitoringTouch"/> is called again.
    /// </summary>
    public void StopMonitoringTouch() => Configuration = null;
}
