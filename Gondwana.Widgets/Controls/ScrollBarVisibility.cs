namespace Gondwana.Widgets.Controls;

/// <summary>
/// Specifies when a scrollable widget displays its vertical scrollbar.
/// </summary>
public enum ScrollBarVisibility
{
    /// <summary>Displays the scrollbar only when the content overflows the visible area.</summary>
    Auto,

    /// <summary>Always displays the scrollbar while the widget is visible.</summary>
    Always,

    /// <summary>Never displays the scrollbar.</summary>
    Never
}
