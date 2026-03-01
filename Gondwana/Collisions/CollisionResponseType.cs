using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Collisions;

[JsonConverter(typeof(StringEnumConverter))]
public enum CollisionResponseType
{
    Solid,   // push-out / block movement
    Trigger  // do not push-out, just report
}

