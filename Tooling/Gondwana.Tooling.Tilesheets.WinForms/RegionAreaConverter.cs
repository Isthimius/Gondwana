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
            new[] { "X", "Y", "Width", "Height" }.Select(name =>
                TypeDescriptor.CreateProperty(typeof(Rectangle), properties[name]!,
                    new DescriptionAttribute(name switch
                    {
                        "X" => "Horizontal position of the region's left edge in source-image pixels.",
                        "Y" => "Vertical position of the region's top edge in source-image pixels.",
                        "Width" => "Width of the region in source-image pixels.",
                        _ => "Height of the region in source-image pixels."
                    }),
                    NotifyParentPropertyAttribute.Yes)).ToArray(),
            readOnly: true);
    }
}
