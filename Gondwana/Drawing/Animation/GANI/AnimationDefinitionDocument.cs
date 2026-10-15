namespace Gondwana.Drawing.Animation.GANI;

/// <summary>A physical GANI document containing independently identified logical definitions.</summary>
public sealed class AnimationDefinitionDocument
{
    /// <summary>Gets or sets the definitions in document order.</summary>
    public List<AnimationDefinition> Definitions { get; set; } = [];
}
