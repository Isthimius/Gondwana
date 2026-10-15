namespace Gondwana.Audio.GSND;

/// <summary>A physical GSND document containing independently identified logical definitions.</summary>
public sealed class AudioDefinitionDocument
{
    /// <summary>Gets or sets the definitions in document order.</summary>
    public List<AudioDefinition> Definitions { get; set; } = [];
}
