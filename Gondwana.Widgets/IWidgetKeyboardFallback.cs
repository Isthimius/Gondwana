namespace Gondwana.Widgets;

// Internal participation keeps accelerator routing scoped to built-in widgets.
internal interface IWidgetKeyboardFallback
{
    /// <summary>
    /// Handles keyboard input that was not consumed by the focused widget.
    /// </summary>
    /// <param name="args">The event data.</param>
    void HandleUnhandledKeyboardInput(WidgetKeyboardEventArgs args);
}
