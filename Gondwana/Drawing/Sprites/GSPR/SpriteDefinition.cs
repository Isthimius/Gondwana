using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>A collection of authored sprites. Empty collections are valid.</summary>
public sealed class SpriteDefinition
{
    public List<SpriteInstanceDefinition> Sprites { get; set; } = [];
    public List<SpriteTilesheetSourceDefinition> TilesheetSources { get; set; } = [];
    public List<SpriteSceneSourceDefinition> SceneSources { get; set; } = [];

    /// <summary>Load provenance; never persisted as an absolute content dependency.</summary>
    [JsonIgnore]
    public SpriteDefinitionSource Source { get; set; } = SpriteDefinitionSource.None();
}
