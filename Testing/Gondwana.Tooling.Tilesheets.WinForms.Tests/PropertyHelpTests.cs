using System.ComponentModel;
using System.Drawing;
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
        Assert.Contains("source-image", TypeDescriptor.GetProperties(regionFields)["Area.X"]!.Description);

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
        Assert.Equal("Geometry", properties["Area.X"]!.Category);
        Assert.Equal("Geometry", properties["TilePadding.Left"]!.Category);
        Assert.Equal("Collision", properties["CollisionAdjust.Left"]!.Category);
        Assert.Equal("Collision", properties["CollisionType"]!.Category);

        var frameFields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        var frameProperties = TypeDescriptor.GetProperties(frameFields);
        Assert.Equal("Collision", frameProperties["CollisionAdjust mode"]!.Category);
        Assert.Equal("Collision", frameProperties["Left"]!.Category);
    }


    [Fact]
    public void AreaAndTileSizeExpandIntoEditableWidthAndHeight()
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
        Assert.Equal(typeof(Size), area.PropertyType);
        Assert.Equal(typeof(Size), tileSize.PropertyType);
        foreach (var parent in new[] { area, tileSize })
        {
            Assert.False(parent.IsReadOnly);
            Assert.True(parent.Converter.GetPropertiesSupported());
            var children = parent.Converter.GetProperties(parent.GetValue(fields)!)!;
            Assert.Equal(new[] { "Height", "Width" },
                children.Cast<PropertyDescriptor>().Select(child => child.Name).OrderBy(name => name).ToArray());
        }

        object dimensions = area.GetValue(fields)!;
        area.Converter.GetProperties(dimensions)!["Width"]!.SetValue(dimensions, 64);
        area.SetValue(fields, dimensions);
        Assert.Equal(new Rectangle(7, 11, 64, 28), region.Area);

        dimensions = tileSize.GetValue(fields)!;
        tileSize.Converter.GetProperties(dimensions)!["Height"]!.SetValue(dimensions, 24);
        tileSize.SetValue(fields, dimensions);
        Assert.Equal(new Size(16, 24), region.TileSize);

        // The original region X/Y editors remain available and still update the original Rectangle.
        properties["Area.X"]!.SetValue(fields, 19);
        properties["Area.Y"]!.SetValue(fields, 23);
        Assert.Equal(new Rectangle(19, 23, 64, 28), region.Area);
        Assert.Equal(4, changes);
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
        Assert.Equal("TilePadding.Left", properties.Single(property => property.Name == "TilePadding.Left").DisplayName);
        Assert.Equal("RegionMargin.Left", properties.Single(property => property.Name == "RegionMargin.Left").DisplayName);
        Assert.Equal("Overhang.Left", properties.Single(property => property.Name == "Overhang.Left").DisplayName);
    }

}
