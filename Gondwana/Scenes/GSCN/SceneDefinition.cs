namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Represents the root definition of a scene in the GSCN (Gondwana Scene) file format.
/// </summary>
public sealed class SceneDefinition
{
    private SceneDefinitionSource _source = SceneDefinitionSource.None();

    /// <summary>
    /// Gets or sets the stable scene identifier. An empty value generates a new ID when materialized.
    /// </summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets collision-group names in definition order.
    /// </summary>
    public List<string> CollisionGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets named collision profiles used by the scene.
    /// </summary>
    public List<SceneCollisionProfileDefinition> CollisionProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the layers contained by the scene.
    /// </summary>
    public List<SceneLayerDefinition> Layers { get; set; } = [];

    /// <summary>
    /// Gets or sets provenance metadata describing where this definition came from.
    /// </summary>
    public SceneDefinitionSource Source
    {
        get => _source;
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }
}
