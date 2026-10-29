using System.ComponentModel;
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
        Assert.Equal("Geometry", properties["Area"]!.Category);
        Assert.Equal("Geometry", properties["TilePadding"]!.Category);
        Assert.Equal("Collision", properties["CollisionAdjust.Left"]!.Category);
        Assert.Equal("Collision", properties["CollisionType"]!.Category);

        var frameFields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        var frameProperties = TypeDescriptor.GetProperties(frameFields);
        Assert.Equal("Collision", frameProperties["CollisionAdjust mode"]!.Category);
        Assert.Equal("Collision", frameProperties["Left"]!.Category);
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
            AssertChildren(property, fields, "Left", "Top", "Right", "Bottom");
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
        Assert.Equal(new Spacing(1, 2, 3, 4), converter.ConvertFromInvariantString("1, 2, 3, 4"));
        Assert.Equal("9, 10, 23, 12", converter.ConvertToInvariantString(region.Overhang));
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
