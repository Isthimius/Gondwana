using Gondwana.Input.Keyboard;
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

        if ((!Configuration?.IsPaused ?? false) && (Configuration?.ReadyForNextEvent(tick) ?? false))
        {
            var currentPos = Adapter.CurrentPosition;
            var pressed = Adapter.PressedButtons;
            
            if (_buttonStates.Values.Any(s => s.JustPressed || s.JustReleased) ||
                                        (Configuration.TrackMouseMovement && (_lastPosition != currentPos)))
            {
                foreach (var kvp in _buttonStates)
                {
                    var button = kvp.Key;
                    var state = kvp.Value;

                    bool isCurrentlyDown = pressed.Contains(button);

                    state.JustPressed = isCurrentlyDown && !state.IsDown;
                    state.JustReleased = !isCurrentlyDown && state.IsDown;
                    state.IsDown = isCurrentlyDown;
                    _lastScrollDelta = Adapter.ScrollDelta;

                    _buttonStates[button] = state;
                }

                MouseEvent?.Invoke(new MouseEventArgs(Configuration, CurrentKeyboardModifiers, ButtonStates, _lastPosition, currentPos, _lastScrollDelta));

                _lastPosition = currentPos;
            }
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
