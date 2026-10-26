using System.ComponentModel;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms.Tests;

/// <summary>Guards the animation property-grid help for authored properties.</summary>
public sealed class PropertyHelpTests
{
    [Fact]
    public void AnimationPropertiesHaveDescriptions()
    {
        var adapter = new AnimationPropertyAdapter(AnimationDocument.Create(Path.GetTempPath()));
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(adapter))
        {
            if (property.IsBrowsable)
                Assert.False(string.IsNullOrWhiteSpace(property.Description),
                    $"AnimationPropertyAdapter.{property.Name} must have help text.");
        }
    }
}
