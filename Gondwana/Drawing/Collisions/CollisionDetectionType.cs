using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Collisions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CollisionDetectionType
    {
        None,
        All,
        OthersWithColDetect
    }
}