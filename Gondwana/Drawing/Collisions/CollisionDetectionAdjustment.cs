using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Collisions;

[JsonConverter(typeof(StringEnumConverter))]
public struct CollisionDetectionAdjustment
{
    public int Top;
    public int Bottom;
    public int Left;
    public int Right;
}