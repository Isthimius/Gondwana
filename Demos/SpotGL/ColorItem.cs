using System.Text.Json.Serialization;
using SkiaSharp;

namespace Gondwana.Demos.SpotGL;

internal class ColorItem
{
    [JsonConstructor]
    private ColorItem() { }

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
