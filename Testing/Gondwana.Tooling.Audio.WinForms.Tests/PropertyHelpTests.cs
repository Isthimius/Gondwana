using System.ComponentModel;
using Gondwana.Audio.GSND;

namespace Gondwana.Tooling.Audio.WinForms.Tests;

/// <summary>Guards help displayed for audio resources in the GSND editor.</summary>
public sealed class PropertyHelpTests
{
    [Fact]
    public void AudioPropertiesHaveDescriptions()
    {
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(new AudioResourceDefinition()))
        {
            if (property.IsBrowsable)
                Assert.False(string.IsNullOrWhiteSpace(property.Description),
                    $"AudioResourceDefinition.{property.Name} must have help text.");
        }
        Assert.Contains("-1", TypeDescriptor.GetProperties(typeof(AudioResourceDefinition))[nameof(AudioResourceDefinition.Pan)]!.Description);
    }
}
