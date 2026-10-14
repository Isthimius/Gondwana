namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>A physical GSPR document containing independently identified logical definitions.</summary>
public sealed class SpriteDefinitionDocument
{
    /// <summary>Gets or sets the definitions in document order.</summary>
    public List<SpriteDefinition> Definitions { get; set; } = [];
}
