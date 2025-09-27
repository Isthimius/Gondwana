using System.Drawing;
using Gondwana.Input.Keyboard;

namespace Gondwana.Input.Mouse;

public interface IMouseAdapter
{
    Point CurrentPosition { get; }
    HashSet<MouseButton> PressedButtons { get; }
    KeyboardModifierState CurrentKeyboardModifiers { get; }
    int ScrollDelta { get; }  // Positive = scroll up, negative = scroll down
}