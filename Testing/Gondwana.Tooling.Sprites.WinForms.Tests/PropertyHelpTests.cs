using System.ComponentModel;
using Gondwana.Drawing.Sprites.GSPR;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

/// <summary>Guards the help shown for authored sprite properties.</summary>
public sealed class PropertyHelpTests
{
    [Fact]
    public void SpritePropertiesHaveDescriptions()
    {
        var model = new SpriteProperties(new SpriteInstanceDefinition());
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(model))
        {
            if (property.IsBrowsable)
                Assert.False(string.IsNullOrWhiteSpace(property.Description),
                    $"SpriteProperties.{property.Name} must explain its effect.");
        }
        Assert.Contains("pixels", TypeDescriptor.GetProperties(model)[nameof(SpriteProperties.NudgeX)]!.Description);
    }

    [Fact]
    public void SpritePropertiesAreGroupedByPurpose()
    {
        var properties = TypeDescriptor.GetProperties(new SpriteProperties(new SpriteInstanceDefinition()));
        foreach (PropertyDescriptor property in properties)
            if (property.IsBrowsable)
                Assert.NotEqual("Misc", property.Category);

        Assert.Equal("Identity", properties[nameof(SpriteProperties.Nickname)]!.Category);
        Assert.Equal("Placement", properties[nameof(SpriteProperties.Position)]!.Category);
        Assert.Equal("Source", properties[nameof(SpriteProperties.Frame)]!.Category);
        Assert.Equal("Animation", properties[nameof(SpriteProperties.AnimationKey)]!.Category);
        Assert.Equal("Rendering", properties[nameof(SpriteProperties.Rotation)]!.Category);
        Assert.Equal("Alignment", properties[nameof(SpriteProperties.NudgeX)]!.Category);
        Assert.Equal("Collision", properties[nameof(SpriteProperties.CollisionType)]!.Category);
        Assert.Equal("Collision adjustment", properties[nameof(SpriteProperties.CollisionLeft)]!.Category);
    }

}
