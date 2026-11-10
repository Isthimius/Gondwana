using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using GondwanaMouseButton = Gondwana.Input.Mouse.MouseButton;

namespace Gondwana.Avalonia.Input.Mouse;

/// <summary>
/// Provides a mouse/pointer input adapter for Avalonia applications, tracking pointer position,
/// button states, modifier keys, and scroll events.
/// </summary>
public sealed class AvaloniaMouseAdapter : IMouseAdapter, IDisposable
{
    private readonly Control _control;
    private TopLevel? _topLevel;
    private readonly HashSet<GondwanaMouseButton> _pressed = new();
    private PointF _currentPosition;
    private KeyboardModifierState _modifiers;
    private int _scrollDelta;

    /// <summary>
    /// Gets the current position of the pointer cursor in client (control-local) coordinates.
    /// </summary>
    public Point CurrentPosition => PointerCoordinates.ToScreenPx(_control, _currentPosition);

    /// <summary>
    /// Gets the set of currently pressed mouse buttons.
    /// </summary>
    public HashSet<GondwanaMouseButton> PressedButtons => _pressed;

    /// <summary>
    /// Gets the current state of keyboard modifiers (Shift, Ctrl, Alt).
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers => _modifiers;

    /// <summary>
    /// Gets the accumulated scroll wheel delta since the last read, then resets it to zero.
    /// </summary>
    public int ScrollDelta => Interlocked.Exchange(ref _scrollDelta, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaMouseAdapter"/> class attached to the specified control.
    /// </summary>
    /// <param name="control">The control to monitor for pointer events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is <see langword="null"/>.</exception>
    public AvaloniaMouseAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        // OpenGlControlBase can render in a native child surface (notably WGL).
        // Route input at the Avalonia TopLevel in the tunnel phase, as keyboard
        // input already does, so the GL surface cannot swallow pointer events.
        _control.AttachedToVisualTree += OnAttachedToVisualTree;
        _control.DetachedFromVisualTree += OnDetachedFromVisualTree;

        if (TopLevel.GetTopLevel(_control) is not null)
            ConnectToTopLevel();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        => ConnectToTopLevel();

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        => DisconnectFromTopLevel();

    private void ConnectToTopLevel()
    {
        var topLevel = TopLevel.GetTopLevel(_control);
        if (topLevel is null || ReferenceEquals(topLevel, _topLevel))
            return;

        DisconnectFromTopLevel();
        _topLevel = topLevel;
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void DisconnectFromTopLevel()
    {
        if (_topLevel is null)
            return;

        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _topLevel = null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_control);
        _pressed.Add(MapButton(point.Properties));
        UpdatePosition(e);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed.Remove(MapReleasedButton(e.InitialPressMouseButton));
        UpdatePosition(e);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdatePosition(e);
    }

    private void UpdatePosition(PointerEventArgs e)
    {
        // Pointer events are raised on TopLevel, but positions must remain
        // local to the render control for DPI/presentation normalization.
        var pos = e.GetPosition(_control);
        _currentPosition = new PointF((float)pos.X, (float)pos.Y);

        _modifiers = KeyboardModifierState.None;
        var km = e.KeyModifiers;
        if ((km & KeyModifiers.Shift) != 0) _modifiers |= KeyboardModifierState.Shift;
        if ((km & KeyModifiers.Control) != 0) _modifiers |= KeyboardModifierState.Ctrl;
        if ((km & KeyModifiers.Alt) != 0) _modifiers |= KeyboardModifierState.Alt;
    }

    private static GondwanaMouseButton MapButton(PointerPointProperties props)
    {
        if (props.IsLeftButtonPressed) return GondwanaMouseButton.Left;
        if (props.IsRightButtonPressed) return GondwanaMouseButton.Right;
        if (props.IsMiddleButtonPressed) return GondwanaMouseButton.Middle;
        return GondwanaMouseButton.None;
    }

    private static GondwanaMouseButton MapReleasedButton(global::Avalonia.Input.MouseButton initialPressButton) => initialPressButton switch
    {
        global::Avalonia.Input.MouseButton.Left => GondwanaMouseButton.Left,
        global::Avalonia.Input.MouseButton.Right => GondwanaMouseButton.Right,
        global::Avalonia.Input.MouseButton.Middle => GondwanaMouseButton.Middle,
        _ => GondwanaMouseButton.None
    };

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Avalonia Delta.Y is in lines; positive = scroll up (matches WinForms convention).
        // Multiply by 120 to approximate Windows WHEEL_DELTA units for compatibility.
        var delta = (int)(e.Delta.Y * 120);
        Interlocked.Add(ref _scrollDelta, delta);
    }

    /// <summary>Unsubscribes from the control's pointer events.</summary>
    public void Dispose()
    {
        DisconnectFromTopLevel();
        _control.AttachedToVisualTree -= OnAttachedToVisualTree;
        _control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }
}
