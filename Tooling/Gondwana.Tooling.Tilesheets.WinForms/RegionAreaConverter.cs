using System.ComponentModel;
using System.Drawing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>Shows only the editable X, Y, Width, and Height components of a GTS region area.</summary>
public sealed class RegionAreaConverter : RectangleConverter
{
    /// <inheritdoc/>
    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object? value, Attribute[]? attributes)
    {
        var properties = base.GetProperties(context, value, attributes);
        return new PropertyDescriptorCollection(
            new[] { "X", "Y", "Width", "Height" }.Select(name => properties[name]!).ToArray(),
            readOnly: true);
    }
}
