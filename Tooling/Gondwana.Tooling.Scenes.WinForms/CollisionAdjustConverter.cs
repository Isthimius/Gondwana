using System.ComponentModel;
using System.Globalization;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Edits a complete value rather than mutating a boxed collision struct.</summary>
internal sealed class CollisionAdjustConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            var parts = text.Split(',');
            if (parts.Length != 4) throw new FormatException("Enter four integer insets: top, bottom, left, right.");
            var insets = parts.Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new CollisionAdjust(insets[0], insets[1], insets[2], insets[3]);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is CollisionAdjust inset
            ? FormattableString.Invariant($"{inset.Top}, {inset.Bottom}, {inset.Left}, {inset.Right}")
            : base.ConvertTo(context, culture, value, destinationType);
}
