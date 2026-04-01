using System.Text.Json.Serialization;
using SkiaSharp;

namespace HWG.Spot;

public class ColorItem
{
    [JsonConstructor]
    private ColorItem() { }

    public string Name { get; }
    public SKColor Color { get; }

    internal ColorItem(string name, SKColor color)
    {
        Name = name;
        Color = color;
    }

    public override string ToString() => Name;
}
