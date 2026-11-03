using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Gondwana.Drawing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Displays effective scene tile margins and padding in CSS clockwise order.</summary>
internal sealed class SceneSpacingConverter : ExpandableObjectConverter
{
    private static readonly string[] Edges = ["Top", "Right", "Bottom", "Left"];

    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object? value, Attribute[]? attributes)
    {
        var properties = TypeDescriptor.GetProperties(typeof(Spacing), attributes ?? []);
        return new PropertyDescriptorCollection(Edges.Select(edge =>
            TypeDescriptor.CreateProperty(typeof(Spacing), properties[edge]!,
                new DescriptionAttribute($"Pixel spacing on the {edge.ToLowerInvariant()} edge."),
                NotifyParentPropertyAttribute.Yes)).ToArray(), true);
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;
    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary values) =>
        new Spacing(left: (int)values["Left"]!, top: (int)values["Top"]!,
            right: (int)values["Right"]!, bottom: (int)values["Bottom"]!);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? type) =>
        type == typeof(string) || base.CanConvertTo(context, type);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type type) =>
        type == typeof(string) && value is Spacing spacing
            ? $"{spacing.Top}, {spacing.Right}, {spacing.Bottom}, {spacing.Left}"
            : base.ConvertTo(context, culture, value, type);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        var values = text.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length != 4) throw new FormatException("Specify Top, Right, Bottom, Left.");
        var edges = values.Select(v => int.Parse(v, NumberStyles.Integer, culture ?? CultureInfo.CurrentCulture)).ToArray();
        return new Spacing(left: edges[3], top: edges[0], right: edges[1], bottom: edges[2]);
    }
}
