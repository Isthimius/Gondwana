using Gondwana.Input.Touch.Gestures;

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
public sealed class TouchEventPoller : ITouchInput, IDisposable
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
    /// is created using <see cref="Engine.Instance"/>'s
    /// <see cref="Gondwana.Configuration.EngineConfiguration.TimeBetweenTouchEvents"/> as the
    /// throttle interval.
    /// </param>
    public static void Initialize(ITouchAdapter adapter, TouchEventConfiguration? config = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        config ??= new TouchEventConfiguration(
            secondsBetweenEvents: Engine.Instance.Configuration.TimeBetweenTouchEvents);

        Instance?.Dispose();
        Instance = new TouchEventPoller(adapter, config);
    }

    /// <summary>
    /// Disposes and clears the current singleton instance, including its adapter.
    /// </summary>
    public static void Reset()
    {
        Instance?.Dispose();
        Instance = null;
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
    /// Gets the current touch event configuration, which controls movement-event throttling and pause state.
    /// Returns <c>null</c> if touch monitoring has been stopped or not yet configured.
    /// </summary>
    public TouchEventConfiguration? Configuration { get; private set; }

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchBegan;

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchMoved;

    /// <inheritdoc />
    public event EventHandler<TouchEventArgs>? TouchEnded;

    /// <summary>
    /// Occurs when any recognized gesture — tap, swipe, or pinch — is detected.
    /// Inspect <see cref="GestureEventArgs.IsTap"/>, <see cref="GestureEventArgs.IsSwipe"/>, or
    /// <see cref="GestureEventArgs.IsPinch"/> to determine the gesture kind, then access the
    /// corresponding property (<see cref="GestureEventArgs.Tap"/>, <see cref="GestureEventArgs.Swipe"/>,
    /// or <see cref="GestureEventArgs.Pinch"/>) for the specific data.
    /// </summary>
    /// <remarks>
    /// The internal gesture recognizers (<see cref="TapRecognizer"/>, <see cref="SwipeRecognizer"/>,
    /// <see cref="PinchRecognizer"/>) are owned by this poller instance. They subscribe to this
    /// poller's own touch events and their lifetime is bound to the lifetime of this instance —
    /// when the instance is replaced via <see cref="Initialize"/> the entire object graph
    /// (poller + recognizers + subscriptions) becomes unreachable and is collected together.
    /// </remarks>
    public event Action<GestureEventArgs>? TouchEvent;

    /// <summary>
    /// Gets the tap gesture recognizer used by this poller.
    /// Adjust <see cref="TapGestureRecognizer.MaxTapDurationSeconds"/> and
    /// <see cref="TapGestureRecognizer.MaxTapMovementPixels"/> to tune tap detection thresholds.
    /// </summary>
    public TapGestureRecognizer TapRecognizer { get; }

    /// <summary>
    /// Gets the swipe gesture recognizer used by this poller.
    /// Adjust <see cref="SwipeGestureRecognizer.MinimumSwipeSpeedPixelsPerSecond"/> to tune swipe
    /// detection thresholds.
    /// </summary>
    public SwipeGestureRecognizer SwipeRecognizer { get; }

    /// <summary>
    /// Gets the pinch gesture recognizer used by this poller.
    /// </summary>
    public PinchGestureRecognizer PinchRecognizer { get; }
    private bool _isDisposed;

    private TouchEventPoller(ITouchAdapter adapter, TouchEventConfiguration config)
    {
        Adapter = adapter;
        Configuration = config;

        TapRecognizer = new TapGestureRecognizer(this);
        SwipeRecognizer = new SwipeGestureRecognizer(this);
        PinchRecognizer = new PinchGestureRecognizer(this);
        TapRecognizer.CompetingSwipeRecognizer = SwipeRecognizer;

        TapRecognizer.Tapped += (_, e) => TouchEvent?.Invoke(new GestureEventArgs(e));
        SwipeRecognizer.Swiped += (_, e) => TouchEvent?.Invoke(new GestureEventArgs(e));
        PinchRecognizer.PinchStarted += OnPinch;
        PinchRecognizer.PinchUpdated += OnPinch;
        PinchRecognizer.PinchEnded += OnPinch;
    }

    private void OnPinch(object? sender, PinchedEventArgs e)
        => TouchEvent?.Invoke(new GestureEventArgs(e));

    /// <summary>
    /// Polls the touch adapter for input state changes and raises touch events when appropriate
    /// based on the configuration settings. This method is called once per engine frame.
    /// </summary>
    /// <remarks>
    /// Each call performs the following steps:
    /// <list type="number">
    /// <item><description>
    /// Drains queued beginnings and endings. Lifecycle transitions are never throttled; only
    /// movement events are eligible for throttling.
    /// </description></item>
    /// <item><description>
    /// When not paused, compares <see cref="ITouchAdapter.ActiveTouches"/> against the
    /// last-emitted state. New IDs fire <see cref="TouchBegan"/> immediately; moved contacts
    /// fire <see cref="TouchMoved"/> when the movement throttle interval has elapsed.
    /// </description></item>
    /// <item><description>
    /// Updates <see cref="ActiveTouches"/> from the poller's last-emitted contact state.
    /// During movement throttling, positions therefore intentionally lag adapter state until
    /// the next movement event is emitted.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="tick">
    /// The current engine tick value, used to calculate elapsed time for event throttling.
    /// This value should be monotonically increasing to ensure correct timing behaviour.
    /// </param>
    internal void PollForEvents(long tick)
    {
        if (_isDisposed || Adapter is null) return;

        var began = Adapter.ConsumeBeganTouches();
        var ended = Adapter.ConsumeEndedTouches();

        // A paused/stopped poller deliberately establishes a new logical contact boundary.
        // Drain transient queues, forget prior contacts, and begin fresh on resume.
        if (Configuration is null || Configuration.IsPaused)
        {
            ClearContactState();
            return;
        }

        // Lifecycle transitions are lossless and are never throttled.
        foreach (var beganPoint in began)
        {
            if (_activeTouches.ContainsKey(beganPoint.Id))
                continue;

            var normalized = new TouchPoint(
                beganPoint.Id,
                beganPoint.Position,
                TouchPhase.Began);

            _activeTouches[normalized.Id] = normalized;
            TouchBegan?.Invoke(this, new TouchEventArgs(normalized, tick));
        }

        foreach (var endedPoint in ended)
        {
            // A beginning and ending may both have occurred between engine polls. The queued
            // beginning above ensures consumers still receive a complete lifecycle.
            if (_activeTouches.Remove(endedPoint.Id))
                TouchEnded?.Invoke(this, new TouchEventArgs(endedPoint, tick));
        }

        var currentActive = Adapter.ActiveTouches;

        // Discover active contacts for adapters that use the default empty beginning queue.
        foreach (var point in currentActive)
        {
            if (!_activeTouches.ContainsKey(point.Id))
            {
                var normalized = new TouchPoint(point.Id, point.Position, TouchPhase.Began);
                _activeTouches[normalized.Id] = normalized;
                TouchBegan?.Invoke(this, new TouchEventArgs(normalized, tick));
            }
        }

        bool movementEmitted = false;
        if (Configuration.ReadyForNextEvent(tick))
        {
            foreach (var point in currentActive)
            {
                if (!_activeTouches.TryGetValue(point.Id, out var known) ||
                    known.Position == point.Position)
                    continue;

                var movedPoint = new TouchPoint(point.Id, point.Position, TouchPhase.Moved);
                _activeTouches[point.Id] = movedPoint;
                TouchMoved?.Invoke(this, new TouchEventArgs(movedPoint, tick));
                movementEmitted = true;
            }
        }

        _activeTouchesSnapshot = _activeTouches.Values.ToArray();

        if (movementEmitted)
            Configuration._lastEventTick = tick;
    }

    /// <summary>
    /// Starts or reconfigures touch input monitoring with the specified settings. This method
    /// creates a new configuration that controls event throttling and pause state.
    /// Call this method to begin monitoring touch input or to change monitoring settings.
    /// </summary>
    /// <param name="timeBetweenEvents">
    /// The minimum time interval in seconds between consecutive touch movement events. Touch
    /// beginnings, endings, and cancellations are never throttled. Use the
    /// default time between touch events from <see cref="Engine.Configuration.TimeBetweenTouchEvents"/>
    /// when a value less than zero is supplied. A value of 0 means no throttling.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for touch should be initially paused.
    /// When paused, touch will not generate events even if contacts change.
    /// Default is <c>false</c>.
    /// </param>
    public void StartMonitoringTouch(double timeBetweenEvents = -1, bool isPaused = false)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenTouchEvents;

        Configuration = new TouchEventConfiguration(timeBetweenEvents, isPaused);
    }

    /// <summary>
    /// Stops monitoring touch input by clearing the configuration. After calling this method,
    /// touch events will no longer be raised until <see cref="StartMonitoringTouch"/> is called again.
    /// </summary>
    public void StopMonitoringTouch()
    {
        Configuration = null;
        ClearContactState();
    }

    private void ClearContactState()
    {
        _activeTouches.Clear();
        _activeTouchesSnapshot = Array.Empty<TouchPoint>();
        TapRecognizer.Reset();
        SwipeRecognizer.Reset();
        PinchRecognizer.Reset();
    }

    /// <summary>
    /// Releases recognizers and the current platform adapter.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        TapRecognizer.Dispose();
        SwipeRecognizer.Dispose();
        PinchRecognizer.PinchStarted -= OnPinch;
        PinchRecognizer.PinchUpdated -= OnPinch;
        PinchRecognizer.PinchEnded -= OnPinch;
        PinchRecognizer.Dispose();

        (Adapter as IDisposable)?.Dispose();
        Adapter = null;
        Configuration = null;
        _activeTouches.Clear();
        _activeTouchesSnapshot = Array.Empty<TouchPoint>();

        if (ReferenceEquals(Instance, this))
            Instance = null;
    }
}
