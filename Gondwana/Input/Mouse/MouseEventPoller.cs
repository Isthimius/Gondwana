using System.Drawing;
using Gondwana.Input.Keyboard;

namespace Gondwana.Input.Mouse;

/// <summary>
/// Provides centralized polling and event management for mouse input, including button presses,
/// cursor movement, and scroll wheel activity. This singleton class monitors mouse state and raises
/// events with comprehensive information including button states, position data, scroll deltas,
/// and keyboard modifier states, with support for event throttling and pause functionality.
/// </summary>
public sealed class MouseEventPoller
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="MouseEventPoller"/> class.
    /// This instance is created through the <see cref="Initialize"/> method and provides
    /// centralized access to mouse event polling functionality throughout the application.
    /// </summary>
    public static MouseEventPoller? Instance { get; private set; }

    /// <summary>
    /// Initializes the singleton instance of the <see cref="MouseEventPoller"/> with the specified
    /// mouse adapter and optional configuration. This method replaces the existing instance and
    /// configures it to monitor mouse input through the provided adapter.
    /// </summary>
    /// <param name="adapter">
    /// The mouse adapter that provides access to the current mouse state including cursor position,
    /// button presses, scroll wheel data, and keyboard modifiers. This adapter abstracts
    /// platform-specific mouse input handling.
    /// </param>
    /// <param name="mouseEventConfiguration">
    /// Optional configuration for mouse event monitoring. If <c>null</c>, a default configuration
    /// is created with mouse movement tracking enabled and the throttling interval set from
    /// <see cref="Engine.Configuration.TimeBetweenMouseEvents"/>.
    /// </param>
    public static void Initialize(IMouseAdapter adapter, MouseEventConfiguration? mouseEventConfiguration = null)
    {
        if (mouseEventConfiguration == null)
        {
            mouseEventConfiguration = new MouseEventConfiguration(
                trackMouseMovement: true,
                secondsBetweenEvents: Engine.Instance.Configuration.TimeBetweenMouseEvents);
        }

        Instance = new MouseEventPoller(adapter, mouseEventConfiguration);
    }

    /// <summary>
    /// Occurs when mouse input is detected and the configuration allows the event to be raised
    /// based on throttling settings. This event provides comprehensive mouse state information
    /// including button states (with press/release transitions), cursor position (current and previous),
    /// scroll wheel delta, and keyboard modifier states. Subscribe to this event to handle all
    /// mouse input in your application.
    /// </summary>
    public event Action<MouseEventArgs>? MouseEvent;

    private readonly Dictionary<MouseButton, MouseButtonState> _buttonStates = new();
    private Point _lastPosition = new(0, 0);
    private int _lastScrollDelta = 0;

    /// <summary>
    /// Gets the mouse adapter currently in use, which provides access to the underlying mouse
    /// hardware state including cursor position, pressed buttons, and scroll wheel data.
    /// Returns <c>null</c> if no adapter has been configured.
    /// </summary>
    public IMouseAdapter? Adapter { get; private set; }

    /// <summary>
    /// Gets the current mouse event configuration, which controls movement tracking, throttling intervals,
    /// and pause state. Returns <c>null</c> if mouse monitoring has been stopped or not yet configured.
    /// </summary>
    public MouseEventConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Gets the current position of the mouse cursor in screen or client coordinates.
    /// If no adapter is configured, returns the default <see cref="Point"/> value (0, 0).
    /// This property provides convenient access to the cursor position without requiring
    /// direct access to the adapter.
    /// </summary>
    public Point CurrentPosition => Adapter?.CurrentPosition ?? default;

    /// <summary>
    /// Gets the current state of keyboard modifier keys (Shift, Ctrl, Alt) at the time of the last poll.
    /// If no adapter is configured, returns <see cref="KeyboardModifierState.None"/>.
    /// This allows mouse event handlers to detect modified mouse operations like Ctrl+Click or Shift+Drag.
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers => Adapter?.CurrentKeyboardModifiers ?? KeyboardModifierState.None;

    /// <summary>
    /// Gets a read-only dictionary containing the current state of all monitored mouse buttons,
    /// including whether each button is down, was just pressed, or was just released.
    /// This provides direct access to button states outside of event handling, useful for
    /// polling-based input checks in game loops or update methods.
    /// </summary>
    public IReadOnlyDictionary<MouseButton, MouseButtonState> ButtonStates => _buttonStates;

    /// <summary>
    /// Gets the most recent scroll wheel delta value captured during the last poll.
    /// Positive values indicate upward scrolling, negative values indicate downward scrolling,
    /// and 0 indicates no scrolling. This provides direct access to scroll state outside of
    /// event handling.
    /// </summary>
    public int ScrollDelta => _lastScrollDelta;

    private MouseEventPoller(IMouseAdapter adapter, MouseEventConfiguration? mouseEventConfiguration)
    {
        Adapter = adapter;
        Configuration = mouseEventConfiguration;

        foreach (MouseButton button in Enum.GetValues(typeof(MouseButton)))
        {
            if (button != MouseButton.None)
                _buttonStates[button] = new MouseButtonState();
        }
    }

    /// <summary>
    /// Polls the mouse adapter for input state changes and raises the <see cref="MouseEvent"/>
    /// when appropriate based on the configuration settings. This method should be called regularly
    /// (typically once per frame or game tick) to ensure timely detection of mouse input.
    /// </summary>
    /// <remarks>
    /// This method updates internal button states, tracks cursor position and scroll wheel changes,
    /// and raises events based on the following conditions:
    /// <list type="bullet">
    /// <item><description>A mouse button was pressed or released</description></item>
    /// <item><description>The cursor moved (if <see cref="MouseEventConfiguration.TrackMouseMovement"/> is enabled)</description></item>
    /// <item><description>The scroll wheel was used</description></item>
    /// <item><description>Any button is currently held down</description></item>
    /// </list>
    /// Events are only raised if the configuration is not paused and sufficient time has elapsed
    /// since the last event based on throttling settings.
    /// </remarks>
    /// <param name="tick">
    /// The current game tick or timestamp value, used to calculate elapsed time for event throttling.
    /// This value should be monotonically increasing to ensure correct timing behavior.
    /// </param>
    internal void PollForEvents(long tick)
    {
        if (Adapter is null) return;
        if ((Configuration?.IsPaused ?? true) || !(Configuration?.ReadyForNextEvent(tick) ?? false)) return;

        var currentPos = Adapter.CurrentPosition;
        var pressed = Adapter.PressedButtons;
        var scrollDelta = Adapter.ScrollDelta;

        bool anyButtonChange = false;
        bool isAnyButtonDown = false;

        // update per-button state every cycle
        foreach (var kvp in _buttonStates.ToList())
        {
            var button = kvp.Key;
            var state = kvp.Value;

            bool isCurrentlyDown = pressed.Contains(button);
            bool justPressed = isCurrentlyDown && !state.IsDown;
            bool justReleased = !isCurrentlyDown && state.IsDown;

            state.JustPressed = justPressed;
            state.JustReleased = justReleased;
            state.IsDown = isCurrentlyDown;

            anyButtonChange |= justPressed || justReleased;
            _buttonStates[button] = state;

            if (isCurrentlyDown)
                isAnyButtonDown = true;
        }

        // now decide whether to emit an event
        bool moved = (Configuration?.TrackMouseMovement ?? false) && _lastPosition != currentPos;
        bool scrolled = _lastScrollDelta != scrollDelta;

        if (anyButtonChange || moved || scrolled || isAnyButtonDown)
        {
            MouseEvent?.Invoke(new MouseEventArgs(
                Configuration!,
                CurrentKeyboardModifiers,
                ButtonStates,
                _lastPosition,
                currentPos,
                scrollDelta,
                tick));

            _lastPosition = currentPos;
            _lastScrollDelta = scrollDelta;
        }
    }

    /// <summary>
    /// Starts or reconfigures mouse input monitoring with the specified settings. This method
    /// creates a new configuration that controls movement tracking, event throttling, and pause state.
    /// Call this method to begin monitoring mouse input or to change monitoring settings.
    /// </summary>
    /// <param name="trackMouseMovement">
    /// A value indicating whether mouse cursor movement should be tracked and reported in events.
    /// Set to <c>true</c> to track all mouse movements including hover, or <c>false</c> to only
    /// track explicit actions like button presses and scrolling. Default is <c>true</c>.
    /// </param>
    /// <param name="timeBetweenEvents">
    /// The minimum time interval in seconds between consecutive mouse events. Use this to throttle
    /// high-frequency mouse input. A value of -1 (default) uses the engine's default time between
    /// mouse events from <see cref="Engine.Configuration.TimeBetweenMouseEvents"/>. A value of 0
    /// means no throttling.
    /// </param>
    /// <param name="isPaused">
    /// A value indicating whether event processing for the mouse should be initially paused.
    /// When paused, the mouse will not generate events even if moved, clicked, or scrolled.
    /// Default is <c>false</c>.
    /// </param>
    public void StartMonitoringMouse(bool trackMouseMovement = true, double timeBetweenEvents = -1, bool isPaused = false)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenMouseEvents;

        Configuration = new MouseEventConfiguration(trackMouseMovement, timeBetweenEvents, isPaused);
    }

    /// <summary>
    /// Stops monitoring mouse input by clearing the configuration. After calling this method,
    /// mouse events will no longer be raised until <see cref="StartMonitoringMouse"/> is called again.
    /// This is useful for completely disabling mouse input, such as during specific game states
    /// or when transitioning between scenes.
    /// </summary>
    public void StopMonitoringMouse() => Configuration = null;
}