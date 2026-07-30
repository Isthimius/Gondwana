using Gondwana.Input.Keyboard;

namespace Gondwana.Widgets;

/// <summary>
/// Provides data for widget keyboard input events routed by <see cref="WidgetInputRouter"/>.
/// </summary>
public sealed class WidgetKeyboardEventArgs : WidgetEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetKeyboardEventArgs"/> class.
    /// </summary>
    /// <param name="widget">The widget that raised the event.</param>
    /// <param name="key">The integer key identifier (as routed from the underlying keyboard adapter).</param>
    /// <param name="keyAction">The key action (pressed, released, repeated).</param>
    /// <param name="modifiers">The keyboard modifier state at the time of the event.</param>
    /// <param name="tick">The engine tick at which the event was emitted.</param>
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

    /// <summary>
    /// Gets the integer key identifier.
    /// </summary>
    public int Key { get; }

    /// <summary>
    /// Gets the action associated with this key event.
    /// </summary>
    public KeyAction KeyAction { get; }

    /// <summary>
    /// Gets the keyboard modifier state at the time of the event.
    /// </summary>
    public KeyboardModifierState Modifiers { get; }
}
