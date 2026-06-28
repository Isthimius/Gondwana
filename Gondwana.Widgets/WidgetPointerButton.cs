namespace Gondwana.Widgets;

/// <summary>
/// Represents a pointer button used by a widget interaction.
/// </summary>
public enum WidgetPointerButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16,
    Touch = 32,
    Stylus = 64
}