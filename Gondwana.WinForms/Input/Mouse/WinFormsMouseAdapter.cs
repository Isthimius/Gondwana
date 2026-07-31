using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;

namespace Gondwana.WinForms.Input.Mouse;

/// <summary>
/// Provides a mouse input adapter for WinForms applications, tracking mouse position, button states, and scroll events.
/// </summary>
public sealed class WinFormsMouseAdapter : IMouseAdapter, IDisposable
{
    private readonly Control _control;
    private readonly HashSet<MouseButton> _pressed = new();
    private readonly object _pressedLock = new();
    private Point _currentPosition;
    private KeyboardModifierState _modifiers;
    private int _scrollDelta;

    /// <summary>
    /// Gets the current position of the mouse cursor.
    /// </summary>
    public Point CurrentPosition => _currentPosition;
    
    /// <summary>
    /// Gets the set of currently pressed mouse buttons, reconciled against the actual OS button
    /// state to prevent stale "button down" entries that can occur when a MouseUp
    /// event is not received (e.g. because the parent form was temporarily disabled while a modal
    /// dialog was shown). Returns a snapshot copy safe for the caller to iterate.
    /// </summary>
    public HashSet<MouseButton> PressedButtons
    {
        get
        {
            var osButtons = Control.MouseButtons;
            lock (_pressedLock)
            {
                if (!osButtons.HasFlag(MouseButtons.Left)) _pressed.Remove(MouseButton.Left);
                if (!osButtons.HasFlag(MouseButtons.Right)) _pressed.Remove(MouseButton.Right);
                if (!osButtons.HasFlag(MouseButtons.Middle)) _pressed.Remove(MouseButton.Middle);
                return new HashSet<MouseButton>(_pressed);
            }
        }
    }
    
    /// <summary>
    /// Gets the current state of keyboard modifiers (Shift, Ctrl, Alt).
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers => _modifiers;
    
    /// <summary>
    /// Gets the accumulated scroll wheel delta since the last read, then resets it to zero.
    /// </summary>
    public int ScrollDelta => Interlocked.Exchange(ref _scrollDelta, 0); // one-time read & reset

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormsMouseAdapter"/> class attached to the specified control.
    /// </summary>
    /// <param name="control">The control to monitor for mouse events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is <see langword="null"/>.</exception>
    public WinFormsMouseAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        _control.MouseDown += OnMouseDown;
        _control.MouseUp += OnMouseUp;
        _control.MouseMove += OnMouseMove;
        _control.MouseWheel += OnMouseWheel;
    }

    private void OnMouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        lock (_pressedLock)
            _pressed.Add(MapButton(e.Button));
        UpdatePosition(e);
    }

    private void OnMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        lock (_pressedLock)
            _pressed.Remove(MapButton(e.Button));
        UpdatePosition(e);
    }

    private void OnMouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        UpdatePosition(e);
    }

    private void UpdatePosition(System.Windows.Forms.MouseEventArgs e)
    {
        _currentPosition = e.Location;

        _modifiers = KeyboardModifierState.None;
        if (Control.ModifierKeys.HasFlag(Keys.Shift)) _modifiers |= KeyboardModifierState.Shift;
        if (Control.ModifierKeys.HasFlag(Keys.Control)) _modifiers |= KeyboardModifierState.Ctrl;
        if (Control.ModifierKeys.HasFlag(Keys.Alt)) _modifiers |= KeyboardModifierState.Alt;
    }

    private static MouseButton MapButton(MouseButtons btn) => btn switch
    {
        MouseButtons.Left => MouseButton.Left,
        MouseButtons.Right => MouseButton.Right,
        MouseButtons.Middle => MouseButton.Middle,
        _ => MouseButton.None
    };

    private void OnMouseWheel(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        Interlocked.Add(ref _scrollDelta, e.Delta);
    }

    /// <summary>Unsubscribes from the control's mouse events.</summary>
    public void Dispose()
    {
        _control.MouseDown -= OnMouseDown;
        _control.MouseUp -= OnMouseUp;
        _control.MouseMove -= OnMouseMove;
        _control.MouseWheel -= OnMouseWheel;
    }
}
