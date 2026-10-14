namespace Gondwana.Scenes.GSCN;

/// <summary>A physical GSCN document containing independently identified logical definitions.</summary>
public sealed class SceneDefinitionDocument
{
    /// <summary>Gets or sets the definitions in document order.</summary>
    public List<SceneDefinition> Definitions { get; set; } = [];
}
