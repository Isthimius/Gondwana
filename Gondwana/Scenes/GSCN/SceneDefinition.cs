using System.ComponentModel;
namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Represents the root definition of a scene in the GSCN (Gondwana Scene) file format.
/// </summary>
public sealed class SceneDefinition
{
    [Newtonsoft.Json.JsonIgnore]
    internal DefinitionLoadStamp? LoadStamp { get; set; }

    private SceneDefinitionSource _source = SceneDefinitionSource.None();

    /// <summary>
    /// Gets or sets the stable scene identifier. An empty value generates a new ID when materialized.
    /// </summary>
    [Category("Identity")]
    [Description("Unique scene key used to find and instantiate this scene. Leave blank to generate an identifier when materialized.")]
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets collision-group names in definition order.
    /// </summary>
    [Category("Collision")]
    [Description("Names of collision groups defined for this scene; group order is retained in the saved definition.")]
    public List<string> CollisionGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets named collision profiles used by the scene.
    /// </summary>
    [Category("Collision")]
    [Description("Named collision rules available to the scene's tiles and sprites.")]
    public List<SceneCollisionProfileDefinition> CollisionProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets authoring-time locations for logical GTS dependencies used by this scene.
    /// Runtime materialization continues to resolve frames through registered tilesheet names.
    /// </summary>
    [Category("Sources")]
    [Description("GTS files or packed entries referenced by the scene for tile artwork; these are authoring-time source locations.")]
    public List<SceneTilesheetSourceDefinition> TilesheetSources { get; set; } = [];

    /// <summary>
    /// Gets or sets authoring-time locations for logical GANI dependencies used by this scene.
    /// Runtime materialization continues to resolve animations through registered cycle keys.
    /// </summary>
    [Category("Sources")]
    [Description("GANI files or packed entries referenced by the scene for animation cycles; these are authoring-time source locations.")]
    public List<SceneAnimationSourceDefinition> AnimationSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the layers contained by the scene.
    /// </summary>
    [Category("Structure")]
    [Description("Scene layers, drawn according to their ZOrder. Each layer has its own dimensions, visibility, and scrolling behavior.")]
    public List<SceneLayerDefinition> Layers { get; set; } = [];

    /// <summary>
    /// Gets or sets provenance metadata describing where this definition came from.
    /// </summary>
    [Category("Provenance")]
    [Description("Origin metadata indicating which loose scene file or packed asset contains this scene definition.")]
    public SceneDefinitionSource Source
    {
        get => _source;
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }
}
