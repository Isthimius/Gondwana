using System.Drawing;
using Gondwana.Blazor.Rendering;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using BrowserMouseEventArgs = Microsoft.AspNetCore.Components.Web.MouseEventArgs;
using BrowserWheelEventArgs = Microsoft.AspNetCore.Components.Web.WheelEventArgs;
using GondwanaMouseButton = Gondwana.Input.Mouse.MouseButton;

namespace Gondwana.Blazor.Input.Mouse;

/// <summary>
/// Provides a mouse/pointer input adapter for Blazor applications, tracking pointer position,
/// button states, modifier keys, and scroll wheel input.
/// </summary>
public sealed class BlazorMouseAdapter : IMouseAdapter
{
    private readonly HashSet<GondwanaMouseButton> _pressed = new();
    private readonly object _pressedLock = new();
    private Point _currentPosition;
    private KeyboardModifierState _modifiers;
    private int _scrollDelta;

    /// <inheritdoc/>
    public Point CurrentPosition => _currentPosition;

    /// <inheritdoc/>
    public HashSet<GondwanaMouseButton> PressedButtons
    {
        get
        {
            lock (_pressedLock)
                return new HashSet<GondwanaMouseButton>(_pressed);
        }
    }
    /// <inheritdoc/>
    public KeyboardModifierState CurrentKeyboardModifiers => _modifiers;

    /// <inheritdoc/>
    public int ScrollDelta => Interlocked.Exchange(ref _scrollDelta, 0);

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorMouseAdapter"/> and attaches it to
    /// the mouse events on the specified render surface component.
    /// </summary>
    /// <param name="component">The render surface component to capture mouse input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public BlazorMouseAdapter(BlazorBitmapRenderSurfaceComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        component.MouseDown += OnMouseDown;
        component.MouseUp += OnMouseUp;
        component.MouseMove += OnMouseMove;
        component.Wheel += OnWheel;
    }

    private void OnMouseDown(BrowserMouseEventArgs e)
    {
        lock (_pressedLock)
            _pressed.Add(MapButton(e.Button));
        UpdatePosition(e);
    }

    private void OnMouseUp(BrowserMouseEventArgs e)
    {
        lock (_pressedLock)
            _pressed.Remove(MapButton(e.Button));
        UpdatePosition(e);
    }

    private void OnMouseMove(BrowserMouseEventArgs e) => UpdatePosition(e);

    private void OnWheel(BrowserWheelEventArgs e)
    {
        // Browser DeltaY: positive = scroll down. Negate to match the convention used by other
        // Gondwana adapters (positive = scroll up / scroll away from user).
        Interlocked.Add(ref _scrollDelta, (int)(-e.DeltaY));
    }

    private void UpdatePosition(BrowserMouseEventArgs e)
    {
        _currentPosition = new Point((int)e.OffsetX, (int)e.OffsetY);

        _modifiers = KeyboardModifierState.None;
        if (e.ShiftKey) _modifiers |= KeyboardModifierState.Shift;
        if (e.CtrlKey) _modifiers |= KeyboardModifierState.Ctrl;
        if (e.AltKey) _modifiers |= KeyboardModifierState.Alt;
    }

    private static GondwanaMouseButton MapButton(long button) => button switch
    {
        0 => GondwanaMouseButton.Left,
        1 => GondwanaMouseButton.Middle,
        2 => GondwanaMouseButton.Right,
        _ => GondwanaMouseButton.None
    };
}
