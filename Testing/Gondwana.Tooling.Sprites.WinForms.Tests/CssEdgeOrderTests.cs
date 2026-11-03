using System.ComponentModel;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

/// <summary>Guards the CSS-style presentation order for sprite collision insets.</summary>
public sealed class CssEdgeOrderTests
{
    [Fact]
    public void CollisionAdjustmentPropertiesAreDeclaredClockwise()
    {
        var order = TypeDescriptor.GetProperties(typeof(SpriteProperties)).Cast<PropertyDescriptor>()
            .Select(property => property.Name)
            .Where(name => name is "CollisionTop" or "CollisionRight" or "CollisionBottom" or "CollisionLeft");
        Assert.Equal(new[] { "CollisionTop", "CollisionRight", "CollisionBottom", "CollisionLeft" }, order);
    }
}
