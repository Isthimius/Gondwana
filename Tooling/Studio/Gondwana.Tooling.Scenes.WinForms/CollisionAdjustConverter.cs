using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Edits a complete value rather than mutating a boxed collision struct.</summary>
internal sealed class CollisionAdjustConverter : ExpandableObjectConverter
{

    private static readonly string[] Edges = ["Top", "Right", "Bottom", "Left"];

    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object? value, Attribute[]? attributes)
    {
        var properties = TypeDescriptor.GetProperties(typeof(CollisionAdjust), attributes ?? []);
        return new PropertyDescriptorCollection(Edges.Select(edge =>
            TypeDescriptor.CreateProperty(typeof(CollisionAdjust), properties[edge]!,
                new DescriptionAttribute($"Collision inset on the {edge.ToLowerInvariant()} side."),
                NotifyParentPropertyAttribute.Yes)).ToArray(), true);
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary values) =>
        new CollisionAdjust((int)values["Top"]!, (int)values["Bottom"]!, (int)values["Left"]!, (int)values["Right"]!);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            var parts = text.Split(',');
            if (parts.Length != 4) throw new FormatException("Enter four integer insets: top, right, bottom, left.");
            var insets = parts.Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new CollisionAdjust(top: insets[0], bottom: insets[2], left: insets[3], right: insets[1]);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is CollisionAdjust inset
            ? FormattableString.Invariant($"{inset.Top}, {inset.Right}, {inset.Bottom}, {inset.Left}")
            : base.ConvertTo(context, culture, value, destinationType);
}
