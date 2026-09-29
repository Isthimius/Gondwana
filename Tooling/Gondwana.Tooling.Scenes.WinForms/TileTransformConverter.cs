using System.ComponentModel;
using System.Globalization;
using Gondwana.Drawing;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed class TileTransformConverter : EnumConverter
{
    private static readonly string[] Labels = ["Identity", "Rotate 90°", "Rotate 180°", "Rotate 270°", "Flip Horizontal", "Flip Vertical", "Flip Diagonal", "Flip Anti-Diagonal"];
    public TileTransformConverter() : base(typeof(TileTransform)) { }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is TileTransform transform && Enum.IsDefined(transform)
            ? Labels[(int)transform] : base.ConvertTo(context, culture, value, destinationType);
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && Array.IndexOf(Labels, text) is var index && index >= 0) return (TileTransform)index;
        return base.ConvertFrom(context, culture, value);
    }
}
