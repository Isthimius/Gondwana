namespace Gondwana.Widgets;

/// <summary>
/// Base event argument type for Gondwana widget callbacks.
/// </summary>
public abstract class WidgetEventArgs : EventArgs
{
    protected WidgetEventArgs(WidgetBase widget, long tick)
    {
        Widget = widget ?? throw new ArgumentNullException(nameof(widget));
        Tick = tick;
    }

    /// <summary>
    /// Gets the widget that raised the callback.
    /// </summary>
    public WidgetBase Widget { get; }

    /// <summary>
    /// Gets the engine or timer tick associated with the callback.
    /// </summary>
    public long Tick { get; }

    /// <summary>
    /// Gets or sets whether this widget interaction was handled.
    /// </summary>
    public bool Handled { get; set; }
}