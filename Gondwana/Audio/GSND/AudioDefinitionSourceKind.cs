using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Identifies the provenance of an audio definition.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioDefinitionSourceKind
{
    /// <summary>
    /// No source or option is selected.
    /// </summary>
    None,
    /// <summary>
    /// The definition is stored in a loose file.
    /// </summary>
    LooseDefinitionFile,
    /// <summary>
    /// The definition is stored in an assets file.
    /// </summary>
    PackedDefinitionFile,
    /// <summary>
    /// The definition was generated from runtime state.
    /// </summary>
    Generated
}
