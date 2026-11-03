using System.ComponentModel;
using Gondwana.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

/// <summary>Guards clockwise display and conversion of scene tile edge values.</summary>
public sealed class CssEdgeOrderTests
{
    [Fact]
    public void SourceAndEffectiveSpacingUseClockwiseOrder()
    {
        var converter = new SceneSpacingConverter();
        var spacing = new Spacing(left: 11, top: 22, right: 33, bottom: 44);
        var edges = converter.GetProperties(spacing)!.Cast<PropertyDescriptor>().Select(property => property.Name);
        Assert.Equal(new[] { "Top", "Right", "Bottom", "Left" }, edges);
        Assert.Equal("22, 33, 44, 11", converter.ConvertToInvariantString(spacing));
        Assert.Equal(spacing, converter.ConvertFromInvariantString("22, 33, 44, 11"));
    }

    [Fact]
    public void SceneCollisionInsetConverterUsesClockwiseOrder()
    {
        var converter = new CollisionAdjustConverter();
        var insets = new CollisionAdjust(top: 12, bottom: 34, left: 56, right: 78);
        var edges = converter.GetProperties(insets)!.Cast<PropertyDescriptor>().Select(property => property.Name);
        Assert.Equal(new[] { "Top", "Right", "Bottom", "Left" }, edges);
        Assert.Equal("12, 78, 34, 56", converter.ConvertToInvariantString(insets));
        Assert.Equal(insets, converter.ConvertFromInvariantString("12, 78, 34, 56"));
    }
}
