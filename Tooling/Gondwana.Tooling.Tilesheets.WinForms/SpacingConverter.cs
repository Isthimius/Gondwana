using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Gondwana.Drawing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>
/// Expands a Spacing value into four editable edges in the GTS property grid.
/// Reconstructs the value type when a child changes so the parent's setter records the edit.
/// </summary>
public sealed class SpacingConverter : ExpandableObjectConverter
{
    private static readonly string[] Edges = ["Left", "Top", "Right", "Bottom"];

    /// <inheritdoc/>
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc/>
    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object? value, Attribute[]? attributes)
    {
        // Exclude the calculated IsEmpty property: only the four editable edges belong here.
        var properties = TypeDescriptor.GetProperties(typeof(Spacing), attributes ?? []);
        string parentName = context?.PropertyDescriptor?.Name ?? "";
        return new PropertyDescriptorCollection(Edges.Select(edge =>
            TypeDescriptor.CreateProperty(typeof(Spacing), properties[edge]!,
                new DescriptionAttribute(DescribeEdge(parentName, edge)),
                NotifyParentPropertyAttribute.Yes)).ToArray(), readOnly: true);
    }

    private static string DescribeEdge(string parentName, string edge)
    {
        string side = edge.ToLowerInvariant();
        return parentName switch
        {
            "TilePadding" => $"Unused {side}-edge padding around each frame, in source-image pixels.",
            "RegionMargin" => $"Unused {side}-edge margin between the region boundary and its frame grid, in pixels.",
            "Overhang" => $"Artwork extending beyond the tile's logical {side} edge, in pixels.",
            _ => $"Pixel spacing on the {side} edge."
        };
    }

    /// <inheritdoc/>
    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc/>
    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary propertyValues) =>
        new Spacing((int)propertyValues["Left"]!, (int)propertyValues["Top"]!,
            (int)propertyValues["Right"]!, (int)propertyValues["Bottom"]!);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Spacing spacing)
            return $"{spacing.Left}, {spacing.Top}, {spacing.Right}, {spacing.Bottom}";
        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
            return base.ConvertFrom(context, culture, value);

        var components = text.Split(',', StringSplitOptions.TrimEntries);
        var edges = new int[4];
        if (components.Length != edges.Length)
            throw new FormatException("Specify four comma-separated integers: Left, Top, Right, Bottom.");

        for (int i = 0; i < edges.Length; i++)
        {
            if (!int.TryParse(components[i], NumberStyles.Integer, culture ?? CultureInfo.CurrentCulture, out edges[i]))
                throw new FormatException("Specify four comma-separated integers: Left, Top, Right, Bottom.");
        }
        return new Spacing(edges[0], edges[1], edges[2], edges[3]);
    }
}
