using SkiaSharp;

namespace HWG.Spot;

internal class ColorItem
{
    internal string Name { get; }
    internal SKColor Color { get; }

    internal ColorItem(string name, SKColor color)
    {
        Name = name;
        Color = color;
    }

    public override string ToString() => Name;
}
