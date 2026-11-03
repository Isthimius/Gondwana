using System.ComponentModel;
using System.Drawing.Design;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

/// <summary>Guards descriptions on callback-backed and reflected GTS grid fields.</summary>
public sealed class PropertyHelpTests
{
    [Fact]
    public void DefinitionRegionMaskAndFramePropertiesHaveDescriptions()
    {
        var document = TilesheetDocument.Create(Path.GetTempPath());
        var definition = PropertyFields.Definition(document, document.MarkChanged);
        AssertHelp(definition);

        var region = new TilesheetRegionDefinition();
        var regionFields = new PropertyFields();
        regionFields.AddModel(region, document.MarkChanged, "Frames");
        AssertHelp(regionFields);
        Assert.Contains("source-image", TypeDescriptor.GetProperties(regionFields)["Area"]!.Description);

        var maskFields = new PropertyFields();
        maskFields.AddModel(new TilesheetMaskDefinition(), document.MarkChanged);
        AssertHelp(maskFields);

        var frame = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        AssertHelp(frame);
    }

    [Fact]
    public void ImageSizeIsReadonlyAndMaskColorEditsRgbAsOneUndoableValue()
    {
        var document = TilesheetDocument.Create(Path.GetTempPath());
        var mask = new TilesheetMaskDefinition { Red = 2, Green = 10, Blue = 22, Alpha = 117, Tolerance = 6 };
        document.Definition.Mask = mask;
        var fields = PropertyFields.Definition(document, document.MarkChanged, new Size(640, 480));
        var properties = TypeDescriptor.GetProperties(fields);

        var imageSize = properties["Image Size"]!;
        Assert.Equal("Definition", imageSize.Category);
        Assert.True(imageSize.IsReadOnly);
        Assert.Equal(typeof(Size), imageSize.PropertyType);
        Assert.Equal(new Size(640, 480), imageSize.GetValue(fields));
        Assert.True(imageSize.Converter.GetPropertiesSupported());
        Assert.Equal(new[] { "Height", "Width" }, imageSize.Converter.GetProperties(imageSize.GetValue(fields)!)!
            .Cast<PropertyDescriptor>().Select(p => p.Name).Order().ToArray());

        Assert.Null(properties["Red"]);
        Assert.Null(properties["Green"]);
        Assert.Null(properties["Blue"]);
        Assert.Equal("Mask", properties["Color"]!.Category);
        Assert.Equal(typeof(PropertyFields.MaskRgb), properties["Color"]!.PropertyType);
        var initialColor = (PropertyFields.MaskRgb)properties["Color"]!.GetValue(fields)!;
        Assert.Equal(((byte)2, (byte)10, (byte)22), (initialColor.Red, initialColor.Green, initialColor.Blue));
        var colorConverter = properties["Color"]!.Converter;
        Assert.Equal(new[] { "Red", "Green", "Blue" },
            colorConverter.GetProperties(initialColor)!.Cast<PropertyDescriptor>().Select(property => property.Name));
        Assert.Equal("2, 10, 22", colorConverter.ConvertToInvariantString(initialColor));
        var editor = Assert.IsType<MaskColorEditor>(properties["Color"]!.GetEditor(typeof(UITypeEditor)));
        Assert.Equal(UITypeEditorEditStyle.Modal, editor.GetEditStyle(null));
        Assert.True(editor.GetPaintValueSupported(null));

        properties["Color"]!.SetValue(fields, new PropertyFields.MaskRgb(80, 120, 160));
        Assert.Equal((byte)80, mask.Red);
        Assert.Equal((byte)120, mask.Green);
        Assert.Equal((byte)160, mask.Blue);
        Assert.Equal((byte)117, mask.Alpha);
        Assert.Equal((byte)6, mask.Tolerance);
        Assert.True(document.IsDirty);

        document.History.Undo();
        Assert.Equal((byte)2, mask.Red);
        Assert.Equal((byte)10, mask.Green);
        Assert.Equal((byte)22, mask.Blue);
        document.History.Redo();
        Assert.Equal((byte)80, mask.Red);

        properties["Alpha"]!.SetValue(fields, (byte)210);
        properties["Tolerance"]!.SetValue(fields, (byte)11);
        Assert.Equal((byte)210, mask.Alpha);
        Assert.Equal((byte)11, mask.Tolerance);
    }

    private static void AssertHelp(PropertyFields fields)
    {
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(fields))
        {
            Assert.False(string.IsNullOrWhiteSpace(property.Description),
                $"Tilesheet field {property.Name} needs explanatory help.");
        }
    }

    [Fact]
    public void RegionAndFramePropertiesUsePurposefulCategories()
    {
        var document = TilesheetDocument.Create(Path.GetTempPath());
        var region = new TilesheetRegionDefinition();
        var regionFields = new PropertyFields();
        regionFields.AddModel(region, document.MarkChanged, "Frames");
        var properties = TypeDescriptor.GetProperties(regionFields);
        foreach (PropertyDescriptor property in properties)
            Assert.NotEqual("Misc", property.Category);

        Assert.Equal("Region", properties["Name"]!.Category);
        Assert.Equal("Geometry", properties["Area"]!.Category);
        Assert.Equal("Geometry", properties["TileSize"]!.Category);
        Assert.Equal("Geometry", properties["RegionMargin"]!.Category);
        Assert.Equal("Geometry", properties["TilePadding"]!.Category);
        Assert.Equal("Collision", properties["CollisionAdjust"]!.Category);
        var collision = properties["CollisionAdjust"]!;
        Assert.Equal(typeof(Gondwana.Physics.Collisions.CollisionAdjust), collision.PropertyType);
        Assert.Equal(new[] { "Top", "Right", "Bottom", "Left" },
            collision.Converter.GetProperties(collision.GetValue(regionFields)!)!
                .Cast<PropertyDescriptor>().Select(property => property.Name));
        Assert.Equal(new[] { "Name", "Area", "TileSize", "RegionMargin", "Overhang",
            "TilePadding", "CollisionType", "CollisionAdjust" },
            properties.Cast<PropertyDescriptor>().Select(property => property.Name));
        Assert.Equal("Collision", properties["CollisionType"]!.Category);

        var frameFields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        var frameProperties = TypeDescriptor.GetProperties(frameFields);
        Assert.Equal("Collision", frameProperties["CollisionAdjust mode"]!.Category);
        Assert.Equal("Collision", frameProperties["Left"]!.Category);
        Assert.Equal(new[] { "XTile", "YTile", "CollisionAdjust mode", "CollisionType", "Top", "Right", "Bottom", "Left" },
            frameProperties.Cast<PropertyDescriptor>().Select(property => property.Name));
    }


    [Fact]
    public void AreaAndTileSizeExpandIntoEditableGeometryComponents()
    {
        var region = new TilesheetRegionDefinition
        {
            Area = new Rectangle(7, 11, 40, 28),
            TileSize = new Size(16, 12)
        };
        int changes = 0;
        var fields = new PropertyFields();
        fields.AddModel(region, () => changes++, "Frames");
        var properties = TypeDescriptor.GetProperties(fields);

        var area = properties["Area"]!;
        var tileSize = properties["TileSize"]!;
        Assert.Equal(typeof(Rectangle), area.PropertyType);
        Assert.Equal(typeof(Size), tileSize.PropertyType);
        AssertChildren(area, fields, "X", "Y", "Width", "Height");
        Assert.Equal(new[] { "X", "Y", "Width", "Height" },
            area.Converter.GetProperties(area.GetValue(fields)!)!.Cast<PropertyDescriptor>().Select(property => property.Name));
        AssertChildren(tileSize, fields, "Width", "Height");

        area.SetValue(fields, WithComponent(area, fields, "Width", 64));
        area.SetValue(fields, WithComponent(area, fields, "X", 19));
        area.SetValue(fields, WithComponent(area, fields, "Y", 23));
        Assert.Equal(new Rectangle(19, 23, 64, 28), region.Area);

        tileSize.SetValue(fields, WithComponent(tileSize, fields, "Height", 24));
        Assert.Equal(new Size(16, 24), region.TileSize);
        Assert.Equal(4, changes);
        Assert.Null(properties["Area.X"]);
        Assert.Null(properties["Area.Y"]);
    }

    [Fact]
    public void MarginPaddingAndOverhangExpandIntoFourEditableEdges()
    {
        var region = new TilesheetRegionDefinition
        {
            TilePadding = new Spacing(1, 2, 3, 4),
            RegionMargin = new Spacing(5, 6, 7, 8),
            Overhang = new Spacing(9, 10, 11, 12)
        };
        int changes = 0;
        var fields = new PropertyFields();
        fields.AddModel(region, () => changes++, "Frames");
        var properties = TypeDescriptor.GetProperties(fields);

        foreach (string name in new[] { "TilePadding", "RegionMargin", "Overhang" })
        {
            var property = properties[name]!;
            Assert.Equal(typeof(Spacing), property.PropertyType);
            AssertChildren(property, fields, "Top", "Right", "Bottom", "Left");
            Assert.Equal(new[] { "Top", "Right", "Bottom", "Left" },
                property.Converter.GetProperties(property.GetValue(fields)!)!
                    .Cast<PropertyDescriptor>().Select(child => child.Name));
            Assert.Null(properties[name + ".Left"]);
        }

        properties["TilePadding"]!.SetValue(fields, WithComponent(properties["TilePadding"]!, fields, "Left", 21));
        properties["RegionMargin"]!.SetValue(fields, WithComponent(properties["RegionMargin"]!, fields, "Top", 22));
        properties["Overhang"]!.SetValue(fields, WithComponent(properties["Overhang"]!, fields, "Right", 23));
        Assert.Equal(new Spacing(21, 2, 3, 4), region.TilePadding);
        Assert.Equal(new Spacing(5, 22, 7, 8), region.RegionMargin);
        Assert.Equal(new Spacing(9, 10, 23, 12), region.Overhang);
        Assert.Equal(3, changes);

        var converter = properties["Overhang"]!.Converter;
        Assert.Equal(new Spacing(4, 1, 2, 3), converter.ConvertFromInvariantString("1, 2, 3, 4"));
        Assert.Equal("10, 23, 12, 9", converter.ConvertToInvariantString(region.Overhang));
        Assert.Throws<FormatException>(() => converter.ConvertFromInvariantString("1, 2, 3"));
    }

    private static void AssertChildren(PropertyDescriptor parent, PropertyFields fields, params string[] expected)
    {
        Assert.False(parent.IsReadOnly);
        Assert.True(parent.Converter.GetPropertiesSupported());
        Assert.True(parent.Converter.GetCreateInstanceSupported());
        var children = parent.Converter.GetProperties(parent.GetValue(fields)!)!;
        Assert.Equal(expected.OrderBy(name => name),
            children.Cast<PropertyDescriptor>().Select(child => child.Name).OrderBy(name => name));
        if (parent.Name != "TileSize")
            Assert.All(children.Cast<PropertyDescriptor>(),
                child => Assert.False(string.IsNullOrWhiteSpace(child.Description)));
    }

    private static object WithComponent(PropertyDescriptor parent, PropertyFields fields, string component, int value)
    {
        object original = parent.GetValue(fields)!;
        var values = parent.Converter.GetProperties(original)!.Cast<PropertyDescriptor>()
            .ToDictionary(child => child.Name, child => child.GetValue(original)!);
        values[component] = value;
        return parent.Converter.CreateInstance(null, values)!;
    }

    [Fact]
    public void GeometryLabelsAreUniqueWithinEachCategory()
    {
        var document = TilesheetDocument.Create(Path.GetTempPath());
        var region = new TilesheetRegionDefinition();
        var fields = new PropertyFields();
        fields.AddModel(region, document.MarkChanged, "Frames");

        var properties = TypeDescriptor.GetProperties(fields).Cast<PropertyDescriptor>().ToArray();
        foreach (var group in properties.GroupBy(property => property.Category))
            Assert.Equal(group.Count(), group.Select(property => property.DisplayName).Distinct().Count());
        Assert.Equal("Padding", properties.Single(property => property.Name == "TilePadding").DisplayName);
        Assert.Equal("Margin", properties.Single(property => property.Name == "RegionMargin").DisplayName);
        Assert.Equal("Overhang", properties.Single(property => property.Name == "Overhang").DisplayName);
    }

}
