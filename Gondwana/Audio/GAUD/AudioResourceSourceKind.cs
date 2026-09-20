using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Audio.GAUD;

/// <summary>
/// Identifies how a GAUD audio resource locates its underlying media.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioResourceSourceKind
{
    LooseFile,
    PackedAsset,
    Uri
}
