using Gondwana.Input.Keyboard;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace Gondwana.Input.Mouse;

public sealed class MouseEventPoller
{
    public static MouseEventPoller? Instance { get; private set; }

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

    public event Action<MouseEventArgs>? MouseEvent;

    private readonly Dictionary<MouseButton, MouseButtonState> _buttonStates = new();
    private Point _lastPosition = new(0, 0);
    private int _lastScrollDelta = 0;

    public IMouseAdapter? Adapter { get; private set; }
    public MouseEventConfiguration? Configuration { get; private set; }

    public Point CurrentPosition => Adapter?.CurrentPosition ?? default;
    public KeyboardModifierState CurrentKeyboardModifiers => Adapter?.CurrentKeyboardModifiers ?? KeyboardModifierState.None;

    public IReadOnlyDictionary<MouseButton, MouseButtonState> ButtonStates => _buttonStates;
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

    internal void PollForEvents(long tick)
    {
        if (Adapter is null) return;
        if ((Configuration?.IsPaused ?? true) || !(Configuration?.ReadyForNextEvent(tick) ?? false)) return;

        var currentPos = Adapter.CurrentPosition;
        var pressed = Adapter.PressedButtons;
        var scrollDelta = Adapter.ScrollDelta;

        bool anyButtonChange = false;

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
        }

        // now decide whether to emit an event
        bool moved = (Configuration?.TrackMouseMovement ?? false) && _lastPosition != currentPos;
        bool scrolled = _lastScrollDelta != scrollDelta;

        if (anyButtonChange || moved || scrolled)
        {
            MouseEvent?.Invoke(new MouseEventArgs(
                Configuration!,
                CurrentKeyboardModifiers,
                ButtonStates,
                _lastPosition,
                currentPos,
                scrollDelta));

            _lastPosition = currentPos;
            _lastScrollDelta = scrollDelta;
        }
    }

    public void StartMonitoringMouse(bool trackMouseMovement = true, double timeBetweenEvents = -1, bool isPaused = false)
    {
        if (timeBetweenEvents < 0)
            timeBetweenEvents = Engine.Instance.Configuration.TimeBetweenMouseEvents;

        Configuration = new MouseEventConfiguration(trackMouseMovement, timeBetweenEvents, isPaused);
    }

    public void StopMonitoringMouse() => Configuration = null;
}
