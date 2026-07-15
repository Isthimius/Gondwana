namespace Gondwana.Widgets;

/// <summary>
/// Represents a pointer button used by a widget interaction.
/// </summary>
public enum WidgetPointerButtonEnum
{
    /// <summary>
    /// No button.
    /// </summary>
    None = 0,

    /// <summary>
    /// Left mouse button.
    /// </summary>
    Left = 1,

    /// <summary>
    /// Right mouse button.
    /// </summary>
    Right = 2,

    /// <summary>
    /// Middle mouse button.
    /// </summary>
    Middle = 4,

    /// <summary>
    /// First extended mouse button (typically "back" button).
    /// </summary>
    XButton1 = 8,

    /// <summary>
    /// Second extended mouse button (typically "forward" button).
    /// </summary>
    XButton2 = 16,

    /// <summary>
    /// Touch input.
    /// </summary>
    Touch = 32,

    /// <summary>
    /// Stylus input.
    /// </summary>
    Stylus = 64
}