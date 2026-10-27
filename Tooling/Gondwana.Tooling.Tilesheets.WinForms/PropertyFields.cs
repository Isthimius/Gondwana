using System.ComponentModel;
using System.Reflection;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal enum Inheritance
{
    /// <summary>
    /// Uses the region default instead of a frame override.
    /// </summary>
    InheritRegion,
    /// <summary>
    /// Uses a frame-specific value instead of the region default.
    /// </summary>
    Override
}
internal enum FrameCollisionChoice
{
    /// <summary>
    /// Uses the region default instead of a frame override.
    /// </summary>
    InheritRegion,
    /// <summary>
    /// Disables collision for this frame.
    /// </summary>
    None,
    /// <summary>
    /// Treats the frame as a blocking collider.
    /// </summary>
    Blocking,
    /// <summary>
    /// Treats the frame as a non-blocking collision trigger.
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

    /// <summary>Adds a callback-backed property descriptor to the grid.</summary>
    /// <typeparam name="T">The property's value type.</typeparam>
    /// <param name="name">Property name displayed in the grid.</param>
    /// <param name="category">Grid category grouping the property.</param>
    /// <param name="get">Callback reading the current value.</param>
    /// <param name="set">Callback assigning a value, or null for a read-only property.</param>
    /// <param name="description">Help text displayed when the property is selected.</param>
    public void Add<T>(string name, string category, Func<T> get, Action<T>? set = null, string description = "")
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException($"Missing help text for {name}.", nameof(description));
        _fields.Add(new Field<T>(name, category, get, set, description));
    }

    /// <summary>Adds writable scalar properties and writable integer components of value-type properties using reflection.</summary>
    /// <param name="model">The original object to read and mutate.</param>
    /// <param name="changed">Callback invoked after writing a property.</param>
    /// <param name="excluded">Top-level property names to omit, matched case-sensitively.</param>
    public void AddModel(object model, Action changed, params string[] excluded)
    {
        foreach (var property in model.GetType().GetProperties().Where(p => p.CanWrite && !excluded.Contains(p.Name)))
        {
            // Group the region's metadata by purpose instead of giving each field its own heading.
            string? category = model is TilesheetRegionDefinition ? property.Name switch
            {
                nameof(TilesheetRegionDefinition.Name) => "Region",
                nameof(TilesheetRegionDefinition.Area) or nameof(TilesheetRegionDefinition.TileSize)
                    or nameof(TilesheetRegionDefinition.TilePadding) or nameof(TilesheetRegionDefinition.RegionMargin)
                    or nameof(TilesheetRegionDefinition.Overhang) => "Geometry",
                nameof(TilesheetRegionDefinition.CollisionAdjust) or nameof(TilesheetRegionDefinition.CollisionType) => "Collision",
                _ => null
            } : null;
            if (property.PropertyType == typeof(string) || property.PropertyType.IsPrimitive || property.PropertyType.IsEnum)
                AddReflected(model, [property], changed, category);
            else if (property.PropertyType.IsValueType)
                foreach (var child in property.PropertyType.GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(int)))
                    AddReflected(model, [property, child], changed, category);
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
        _fields.Add(new ReflectedField(string.Join(".", path.Select(p => p.Name)), category ?? path[0].Name, leaf.PropertyType, Read, Write, DescribeReflected(path)));
    }

    private static string DescribeReflected(PropertyInfo[] path)
    {
        string? parentDescription = path[0].GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (path.Length == 1)
            return parentDescription ?? throw new InvalidOperationException($"Missing help text for {path[0].Name}.");

        string edge = path[^1].Name.ToLowerInvariant();
        return path[0].Name switch
        {
            "Area" => path[^1].Name switch
            {
                "X" => "Horizontal source-image pixel position of the region's left edge.",
                "Y" => "Vertical source-image pixel position of the region's top edge.",
                "Width" => "Width of the source-image region rectangle in pixels.",
                "Height" => "Height of the source-image region rectangle in pixels.",
                _ => throw new InvalidOperationException($"Unexpected Area component: {edge}.")
            },
            "TileSize" => $"Frame {edge} in source-image pixels; changing this adjusts the frame grid.",
            "RegionMargin" => $"Unused {edge}-edge margin between the region rectangle and the frame grid, in pixels.",
            "TilePadding" => $"Unused {edge}-edge padding around each frame; adjacent frames include both adjoining padding edges.",
            "Overhang" => $"Amount of artwork extending beyond the logical tile on its {edge} side, in pixels.",
            "CollisionAdjust" => $"Collision-box inset on the {edge} edge, in pixels. Positive shrinks; negative expands.",
            _ => parentDescription ?? throw new InvalidOperationException($"Missing help text for {path[0].Name}.{path[^1].Name}.")
        };
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
        fields.Add("Name", "Definition", () => model.Name, v => { model.Name = v; changed(); }, "Logical tilesheet name referenced by animations, scenes, and sprites.");
        fields.Add("PremultiplyAlpha", "Definition", () => model.PremultiplyAlpha, v => { model.PremultiplyAlpha = v; changed(); },
            "Multiply color channels by alpha when preparing tilesheet imagery for blending. Masking is configured separately and takes precedence at runtime when enabled.");
        fields.Add("Image.FilePath", "Image", () => model.Image?.FilePath ?? "", v => { document.SetImage(v); changed(); },
            "Relative to the GTS directory. Editing this switches to a loose image source.");
        fields.Add("Source.Kind", "Provenance (read only)", () => model.Source.Kind, description: "Read-only origin type for this tilesheet definition (loose or packed).");
        fields.Add("Source.GtsFilePath", "Provenance (read only)", () => model.Source.GtsFilePath ?? "", description: "Original GTS document path, if loaded from a loose file; read-only.");
        fields.Add("Source.AssetsFilePath", "Provenance (read only)", () => model.Source.AssetsFilePath ?? "", description: "Source GAF package path when this definition was loaded from a packed entry; read-only.");
        fields.Add("Source.AssetEntryName", "Provenance (read only)", () => model.Source.AssetEntryName ?? "", description: "Name of this definition inside the source GAF package; read-only.");
        fields.Add("Mask enabled", "Mask", () => model.Mask is not null, v => { model.Mask = v ? model.Mask ?? new() : null; changed(); }, "Enable color-key masking using the RGB components and tolerance shown below. Alpha is stored but ignored when matching colors.");
        if (model.Mask is { } mask)
            foreach (var property in typeof(TilesheetMaskDefinition).GetProperties().Where(p => p.CanWrite))
                fields.AddReflected(mask, [property], changed, "Mask");
        return fields;
    }

    /// <summary>Builds frame property descriptors, creating an override entry only when a value is edited.</summary>
    /// <param name="document">Document used to find or create the frame override.</param>
    /// <param name="region">Region providing inherited collision defaults.</param>
    /// <param name="x">Zero-based frame column.</param>
    /// <param name="y">Zero-based frame row.</param>
    /// <param name="changed">Callback responsible for notifying the editor of a mutation.</param>
    /// <returns>Property descriptors reading the frame override or inherited region values.</returns>
    public static PropertyFields Frame(TilesheetDocument document, TilesheetRegionDefinition region, int x, int y, Action changed)
    {
        var fields = new PropertyFields();
        TilesheetFrameDefinition? Current() => document.FindFrame(region, x, y);
        fields.Add("XTile", "Coordinates (read only)", () => x, description: "Zero-based frame column within this region; read-only.");
        fields.Add("YTile", "Coordinates (read only)", () => y, description: "Zero-based frame row within this region; read-only.");
        fields.Add("CollisionAdjust mode", "Collision", () => Current()?.CollisionAdjust is null ? Inheritance.InheritRegion : Inheritance.Override,
            v => { document.EditFrame(region, x, y).CollisionAdjust = v == Inheritance.InheritRegion ? null : Current()?.CollisionAdjust ?? region.CollisionAdjust; changed(); }, "InheritRegion uses the region collision insets. Override stores separate insets for this frame.");
        fields.Add("CollisionType", "Collision", () => Current()?.CollisionType is { } t ? Enum.Parse<FrameCollisionChoice>(t.ToString()) : FrameCollisionChoice.InheritRegion,
            v => { document.EditFrame(region, x, y).CollisionType = v == FrameCollisionChoice.InheritRegion ? null : Enum.Parse<TileCollisionType>(v.ToString()); changed(); }, "Choose inherited region behavior, None for no collider, Blocking, or Trigger for non-blocking contact.");
        foreach (var property in typeof(CollisionAdjust).GetProperties().Where(p => p.CanWrite))
        {
            bool inherited = Current()?.CollisionAdjust is null;
            fields.Add(property.Name, "Collision", () => (int)property.GetValue(Current()?.CollisionAdjust ?? region.CollisionAdjust)!,
                inherited ? null : v =>
                {
                    object boxed = Current()!.CollisionAdjust!.Value;
                    property.SetValue(boxed, v);
                    document.EditFrame(region, x, y).CollisionAdjust = (CollisionAdjust)boxed;
                    changed();
                }, inherited ? $"Collision inset on the {property.Name.ToLowerInvariant()} side; inherited from the region. Select Override to edit." : $"Collision inset on the {property.Name.ToLowerInvariant()} side. Positive shrinks the box; negative expands it.");
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

    private sealed class ReflectedField(string name, string category, Type type, Func<object?> get, Action<object?> set, string description)
        : PropertyDescriptor(name, [new CategoryAttribute(category), new DisplayNameAttribute(name.Split('.').Last()), new DescriptionAttribute(description)])
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
