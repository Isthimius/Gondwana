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
}
