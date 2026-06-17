using SkiaSharp;

namespace Gondwana.Demos.SpotBlazor;

internal class ColorItem
{
    internal string Name { get; }
    internal SKColor Color { get; }
    internal SKColor TextColor { get; }

    internal ColorItem(string name, SKColor color, SKColor textColor)
    {
        Name = name;
        Color = color;
        TextColor = textColor;
    }
}
