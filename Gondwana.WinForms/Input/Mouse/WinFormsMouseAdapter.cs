using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;

namespace Gondwana.WinForms.Input.Mouse;

public sealed class WinFormsMouseAdapter : IMouseAdapter
{
    private readonly HashSet<MouseButton> _pressed = new();
    private Point _currentPosition;
    private KeyboardModifierState _modifiers;
    private int _scrollDelta;

    public Point CurrentPosition => _currentPosition;
    public HashSet<MouseButton> PressedButtons => _pressed;
    public KeyboardModifierState CurrentKeyboardModifiers => _modifiers;
    public int ScrollDelta => Interlocked.Exchange(ref _scrollDelta, 0); // one-time read & reset

    public WinFormsMouseAdapter(Control control)
    {
        if (control == null)
            throw new ArgumentNullException(nameof(control));

        control.MouseDown += OnMouseDown;
        control.MouseUp += OnMouseUp;
        control.MouseMove += OnMouseMove;
        control.MouseWheel += OnMouseWheel;
    }

    private void OnMouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        _pressed.Add(MapButton(e.Button));
        UpdatePosition(e);
    }

    private void OnMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
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
}