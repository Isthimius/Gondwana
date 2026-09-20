using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Identifies how a GSND audio resource locates its underlying media.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioResourceSourceKind
{
    LooseFile,
    PackedAsset,
    Uri
}
