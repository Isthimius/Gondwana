using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// Simple is self-terminating; the other two are repeating
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum CycleType
{
    /// <summary>
    /// 1 -> 2 -> 3 -> 4 -> stop
    /// </summary>
    Simple,

    /// <summary>
    /// 1 -> 2 -> 3 -> 4 -> 1 -> 2 -> ...
    /// </summary>
    Repeating,

    /// <summary>
    /// 1 -> 2 -> 3 -> 4 -> 3 -> 2 -> 1 -> 2 -> ...
    /// </summary>
    PingPong
}