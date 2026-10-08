using System.ComponentModel;
using System.Reflection;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal enum Inheritance
{
    /// <summary>
    /// Specifies inherit region.
    /// </summary>
    InheritRegion,
    /// <summary>
    /// Specifies override.
    /// </summary>
    Override
}
internal enum FrameCollisionChoice
{
    /// <summary>
    /// Specifies inherit region.
    /// </summary>
    InheritRegion,
    /// <summary>
    /// Disables collision for this frame.
    /// </summary>
    None,
    /// <summary>
    /// Specifies blocking.
    /// </summary>
    Blocking,
    /// <summary>
    /// Specifies trigger.
    /// </summary>
    Trigger
}

/// <summary>Scalar property-grid adapters mutate the original model, including boxed value types.</summary>
internal sealed class PropertyFields : CustomTypeDescriptor
{
    private readonly List<PropertyDescriptor> _fields = [];
    /// <inheritdoc/>
    public override PropertyDescriptorCollection GetProperties() => new(_fields.ToArray());
    /// <inheritdoc/>
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => GetProperties();
    /// <inheritdoc/>
    public override object GetPropertyOwner(PropertyDescriptor? pd) => this;

    /// <summary>
    /// Adds the supplied entry to the property fields.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="name">The name.</param>
    /// <param name="category">The property-grid category.</param>
    /// <param name="get">The function that reads the property value.</param>
    /// <param name="set">The optional action that writes the property value.</param>
    /// <param name="description">The description shown for the property.</param>
    public void Add<T>(string name, string category, Func<T> get, Action<T>? set = null, string description = "") =>
        _fields.Add(new Field<T>(name, category, get, set, description));

    /// <summary>
    /// Adds editable property descriptors for the supplied model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="changed">The callback invoked when a property changes.</param>
    /// <param name="excluded">The model property names to omit.</param>
    public void AddModel(object model, Action changed, params string[] excluded)
    {
        foreach (var property in model.GetType().GetProperties().Where(p => p.CanWrite && !excluded.Contains(p.Name)))
        {
            if (property.PropertyType == typeof(string) || property.PropertyType.IsPrimitive || property.PropertyType.IsEnum)
                AddReflected(model, [property], changed);
            else if (property.PropertyType.IsValueType)
                foreach (var child in property.PropertyType.GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(int)))
                    AddReflected(model, [property, child], changed);
        }
    }

    private void AddReflected(object model, PropertyInfo[] path, Action changed, string? category = null)
    {
        var leaf = path[^1];
        object? Read()
        {
            object? value = model;
            foreach (var p in path) value = p.GetValue(value);
            return value;
        }
        void Write(object? value)
        {
            if (path.Length == 1) leaf.SetValue(model, value);
            else
            {
                var boxed = path[0].GetValue(model)!;
                leaf.SetValue(boxed, value);
                path[0].SetValue(model, boxed);
            }
            changed();
        }
        _fields.Add(new ReflectedField(string.Join(".", path.Select(p => p.Name)), category ?? path[0].Name, leaf.PropertyType, Read, Write));
    }

    /// <summary>
    /// Builds property descriptors for a tilesheet definition.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    /// <param name="changed">The callback invoked when a property changes.</param>
    /// <returns>The property descriptors for the tilesheet definition.</returns>
    public static PropertyFields Definition(TilesheetDocument document, Action changed)
    {
        var model = document.Definition;
        var fields = new PropertyFields();
        fields.Add("Name", "Definition", () => model.Name, v => { model.Name = v; changed(); });
        fields.Add("PremultiplyAlpha", "Definition", () => model.PremultiplyAlpha, v => { model.PremultiplyAlpha = v; changed(); },
            "Stored independently of Mask. Runtime gives Mask precedence when enabled.");
        fields.Add("Image.FilePath", "Image", () => model.Image?.FilePath ?? "", v => { document.SetImage(v); changed(); },
            "Relative to the GTS directory. Editing this switches to a loose image source.");
        fields.Add("Source.Kind", "Provenance (read only)", () => model.Source.Kind);
        fields.Add("Source.GtsFilePath", "Provenance (read only)", () => model.Source.GtsFilePath ?? "");
        fields.Add("Source.AssetsFilePath", "Provenance (read only)", () => model.Source.AssetsFilePath ?? "");
        fields.Add("Source.AssetEntryName", "Provenance (read only)", () => model.Source.AssetEntryName ?? "");
        fields.Add("Mask enabled", "Mask", () => model.Mask is not null, v => { model.Mask = v ? model.Mask ?? new() : null; changed(); });
        if (model.Mask is { } mask)
            foreach (var property in typeof(TilesheetMaskDefinition).GetProperties().Where(p => p.CanWrite))
                fields.AddReflected(mask, [property], changed, "Mask");
        return fields;
    }

    /// <summary>
    /// Builds property descriptors for a selected frame.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <param name="changed">The callback invoked when a property changes.</param>
    /// <returns>The property descriptors for the selected frame.</returns>
    public static PropertyFields Frame(TilesheetDocument document, TilesheetRegionDefinition region, int x, int y, Action changed)
    {
        var fields = new PropertyFields();
        TilesheetFrameDefinition? Current() => document.FindFrame(region, x, y);
        fields.Add("XTile", "Coordinates (read only)", () => x);
        fields.Add("YTile", "Coordinates (read only)", () => y);
        fields.Add("CollisionAdjust mode", "Collision", () => Current()?.CollisionAdjust is null ? Inheritance.InheritRegion : Inheritance.Override,
            v => { document.EditFrame(region, x, y).CollisionAdjust = v == Inheritance.InheritRegion ? null : Current()?.CollisionAdjust ?? region.CollisionAdjust; changed(); });
        fields.Add("CollisionType", "Collision", () => Current()?.CollisionType is { } t ? Enum.Parse<FrameCollisionChoice>(t.ToString()) : FrameCollisionChoice.InheritRegion,
            v => { document.EditFrame(region, x, y).CollisionType = v == FrameCollisionChoice.InheritRegion ? null : Enum.Parse<TileCollisionType>(v.ToString()); changed(); });
        foreach (var property in typeof(CollisionAdjust).GetProperties().Where(p => p.CanWrite))
        {
            bool inherited = Current()?.CollisionAdjust is null;
            fields.Add(property.Name, "CollisionAdjust", () => (int)property.GetValue(Current()?.CollisionAdjust ?? region.CollisionAdjust)!,
                inherited ? null : v =>
                {
                    object boxed = Current()!.CollisionAdjust!.Value;
                    property.SetValue(boxed, v);
                    document.EditFrame(region, x, y).CollisionAdjust = (CollisionAdjust)boxed;
                    changed();
                }, inherited ? "Inherited from region. Choose Override to edit." : "Positive insets; negative expands.");
        }
        return fields;
    }

    private sealed class Field<T>(string name, string category, Func<T> get, Action<T>? set, string description)
        : PropertyDescriptor(name, [new CategoryAttribute(category), new DescriptionAttribute(description)])
    {
        /// <inheritdoc/>
        public override Type ComponentType => typeof(PropertyFields);
        /// <inheritdoc/>
        public override Type PropertyType => typeof(T);
        /// <inheritdoc/>
        public override bool IsReadOnly => set is null;
        /// <inheritdoc/>
        public override object? GetValue(object? component) => get();
        /// <inheritdoc/>
        public override void SetValue(object? component, object? value) => set?.Invoke((T)value!);
        /// <inheritdoc/>
        public override bool CanResetValue(object component) => false;
        /// <inheritdoc/>
        public override void ResetValue(object component) { }
        /// <inheritdoc/>
        public override bool ShouldSerializeValue(object component) => false;
    }

    private sealed class ReflectedField(string name, string category, Type type, Func<object?> get, Action<object?> set)
        : PropertyDescriptor(name, [new CategoryAttribute(category), new DisplayNameAttribute(name.Split('.').Last())])
    {
        /// <inheritdoc/>
        public override Type ComponentType => typeof(PropertyFields);
        /// <inheritdoc/>
        public override Type PropertyType => type;
        /// <inheritdoc/>
        public override bool IsReadOnly => false;
        /// <inheritdoc/>
        public override object? GetValue(object? component) => get();
        /// <inheritdoc/>
        public override void SetValue(object? component, object? value) => set(value);
        /// <inheritdoc/>
        public override bool CanResetValue(object component) => false;
        /// <inheritdoc/>
        public override void ResetValue(object component) { }
        /// <inheritdoc/>
        public override bool ShouldSerializeValue(object component) => false;
    }
}
