using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Identifies the provenance of an audio definition.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioDefinitionSourceKind
{
    None,
    LooseDefinitionFile,
    PackedDefinitionFile,
    Generated
}
