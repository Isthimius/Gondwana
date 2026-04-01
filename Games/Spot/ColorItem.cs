using System.Text.Json.Serialization;
using SkiaSharp;

namespace HWG.Spot;

internal class ColorItem
{
    [JsonConstructor]
    private ColorItem() { }

    internal string Name { get; }
    internal SKColor Color { get; }

    internal ColorItem(string name, SKColor color)
    {
        Name = name;
        Color = color;
    }
}
