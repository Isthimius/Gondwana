using System.ComponentModel;
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
}
