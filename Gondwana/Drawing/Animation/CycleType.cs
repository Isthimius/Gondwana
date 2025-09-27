using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// Simple is self-terminating; the other two are repeating
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum CycleType
{
    Simple,
    Repeating,
    PingPong
}
