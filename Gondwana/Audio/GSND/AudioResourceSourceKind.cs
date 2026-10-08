using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Audio.GSND;

/// <summary>
/// Identifies how a GSND audio resource locates its underlying media.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioResourceSourceKind
{
    /// <summary>
    /// The resource is loaded from a loose file.
    /// </summary>
    LooseFile,
    /// <summary>
    /// The resource is loaded from an assets file.
    /// </summary>
    PackedAsset,
    /// <summary>
    /// The resource is loaded from a URI.
    /// </summary>
    Uri
}
