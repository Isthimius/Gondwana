using Gondwana.Input.Keyboard;
using System.Drawing;

namespace Gondwana.Input.Mouse;

public sealed class MouseEventArgs : EventArgs
{
    MouseEventConfiguration MouseEventConfiguration { get; }
    KeyboardModifierState CurrentKeyboardModifiers { get; }
    IReadOnlyDictionary<MouseButton, MouseButtonState> ButtonStates { get; }
    Point PreviousPosition { get; }
    Point CurrentPosition { get; }
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
