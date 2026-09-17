namespace Gondwana.Widgets;

// Internal participation keeps accelerator routing scoped to built-in widgets.
internal interface IWidgetKeyboardFallback
{
    void HandleUnhandledKeyboardInput(WidgetKeyboardEventArgs args);
}
