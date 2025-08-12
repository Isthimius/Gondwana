using Gondwana.Input.Keyboard;
using System.Drawing;

namespace Gondwana.Input.Mouse;

public sealed class MouseEventArgs : EventArgs
{
    public MouseEventConfiguration MouseEventConfiguration { get; }
    public KeyboardModifierState CurrentKeyboardModifiers { get; }
    public IReadOnlyDictionary<MouseButton, MouseButtonState> ButtonStates { get; }
    public Point PreviousPosition { get; }
    public Point CurrentPosition { get; }
    public int ScrollDelta { get; }

    public bool IsShift => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Shift);
    public bool IsCtrl => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Ctrl);
    public bool IsAlt => CurrentKeyboardModifiers.HasFlag(KeyboardModifierState.Alt);

    public MouseEventArgs(MouseEventConfiguration mouseEventConfiguration,
                          KeyboardModifierState currentKeyboardModifiers, 
                          IReadOnlyDictionary<MouseButton, MouseButtonState> buttonStates,
                          Point previousPosition,
                          Point currentPosition,
                          int scrollDelta)
    {
        MouseEventConfiguration = mouseEventConfiguration;
        CurrentKeyboardModifiers = currentKeyboardModifiers;
        ButtonStates = buttonStates;
        PreviousPosition = previousPosition;
        CurrentPosition = currentPosition;
        ScrollDelta = scrollDelta;
    }
}
