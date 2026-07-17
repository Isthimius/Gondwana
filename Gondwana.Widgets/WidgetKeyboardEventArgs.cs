using Gondwana.Input.Keyboard;

namespace Gondwana.Widgets;

public sealed class WidgetKeyboardEventArgs : WidgetEventArgs
{
    public WidgetKeyboardEventArgs(WidgetBase widget,
                                   int key,
                                   KeyAction keyAction,
                                   KeyboardModifierState modifiers,
                                   long tick = 0)
        : base(widget, tick)
    {
        Key = key;
        KeyAction = keyAction;
        Modifiers = modifiers;
    }

    public int Key { get; }

    public KeyAction KeyAction { get; }

    public KeyboardModifierState Modifiers { get; }
}
