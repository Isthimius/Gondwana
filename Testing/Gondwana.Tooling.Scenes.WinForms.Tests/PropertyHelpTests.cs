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

    [Fact]
    public void SceneAndLayerPropertiesAreGroupedByPurpose()
    {
        var scene = TypeDescriptor.GetProperties(new SceneDefinition());
        var layer = TypeDescriptor.GetProperties(new SceneLayerDefinition());
        AssertNotMisc(scene);
        AssertNotMisc(layer);

        Assert.Equal("Identity", scene[nameof(SceneDefinition.ID)]!.Category);
        Assert.Equal("Collision", scene[nameof(SceneDefinition.CollisionProfiles)]!.Category);
        Assert.Equal("Sources", scene[nameof(SceneDefinition.AnimationSources)]!.Category);
        Assert.Equal("Structure", scene[nameof(SceneDefinition.Layers)]!.Category);
        Assert.Equal("Provenance", scene[nameof(SceneDefinition.Source)]!.Category);

        Assert.Equal("Identity", layer[nameof(SceneLayerDefinition.ID)]!.Category);
        Assert.Equal("Grid", layer[nameof(SceneLayerDefinition.TileWidth)]!.Category);
        Assert.Equal("Grid", layer[nameof(SceneLayerDefinition.OriginPx)]!.Category);
        Assert.Equal("Rendering", layer[nameof(SceneLayerDefinition.Parallax)]!.Category);
        Assert.Equal("Debugging", layer[nameof(SceneLayerDefinition.ShowGridLines)]!.Category);
        Assert.Equal("Collision", layer[nameof(SceneLayerDefinition.DefaultTileCollisionProfile)]!.Category);
        Assert.Equal("Content", layer[nameof(SceneLayerDefinition.Tiles)]!.Category);
    }

    private static void AssertNotMisc(PropertyDescriptorCollection properties)
    {
        foreach (PropertyDescriptor property in properties)
            if (property.IsBrowsable)
                Assert.NotEqual("Misc", property.Category);
    }

}
