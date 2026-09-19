namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Indicates where a scene definition came from.
/// </summary>
public enum SceneDefinitionSourceKind
{
    None = 0,
    LooseDefinitionFile = 1,
    PackedDefinitionFile = 2,
    Generated = 3
}
