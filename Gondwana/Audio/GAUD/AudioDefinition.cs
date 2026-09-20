using Newtonsoft.Json;

namespace Gondwana.Audio.GAUD;

/// <summary>
/// Root definition for the GAUD (Gondwana Audio) file format.
/// </summary>
public sealed class AudioDefinition
{
    private AudioDefinitionSource _source = AudioDefinitionSource.None();

    /// <summary>Gets or sets the audio resources declared by this definition.</summary>
    public List<AudioResourceDefinition> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets runtime provenance metadata describing where this definition came from.
    /// Provenance is intentionally not persisted inside GAUD so relative references remain portable.
    /// </summary>
    [JsonIgnore]
    public AudioDefinitionSource Source
    {
        get => _source;
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }
}
