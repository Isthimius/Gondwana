using System.ComponentModel;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

/// <summary>Ensures scene and tile property grids give actionable help.</summary>
public sealed class PropertyHelpTests
{
    [Fact]
    public void SceneLayerAndTilePropertiesHaveDescriptions()
    {
        AssertHelp(new SceneDefinition());
        AssertHelp(new SceneLayerDefinition());
        var document = SceneDocument.Create(Path.GetTempPath());
        var layer = document.AddLayer();
        AssertHelp(new TilePropertyAdapter(document, layer, 0, 0, () => { }));

        var parallax = TypeDescriptor.GetProperties(layer)[nameof(SceneLayerDefinition.Parallax)]!;
        Assert.Contains("0.5", parallax.Description);
        Assert.Contains("1", parallax.Description);
        var visible = TypeDescriptor.GetProperties(layer)[nameof(SceneLayerDefinition.Visible)]!;
        Assert.Contains("hides", visible.Description);
    }

    private static void AssertHelp(object model)
    {
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(model))
        {
            if (property.IsBrowsable)
                Assert.False(string.IsNullOrWhiteSpace(property.Description),
                    $"{model.GetType().Name}.{property.Name} must have property-grid help.");
        }
    }
}
