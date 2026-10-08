using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>A collection of authored sprites. Empty collections are valid.</summary>
public sealed class SpriteDefinition
{
    [Newtonsoft.Json.JsonIgnore]
    internal DefinitionLoadStamp? LoadStamp { get; set; }

    /// <summary>
    /// Gets or sets the sprites.
    /// </summary>
    public List<SpriteInstanceDefinition> Sprites { get; set; } = [];
    /// <summary>
    /// Gets or sets the tilesheet sources.
    /// </summary>
    public List<SpriteTilesheetSourceDefinition> TilesheetSources { get; set; } = [];
    /// <summary>
    /// Gets or sets the scene sources.
    /// </summary>
    public List<SpriteSceneSourceDefinition> SceneSources { get; set; } = [];

    /// <summary>Load provenance; never persisted as an absolute content dependency.</summary>
    [JsonIgnore]
    public SpriteDefinitionSource Source { get; set; } = SpriteDefinitionSource.None();
}
