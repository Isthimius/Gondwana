using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites;

[JsonConverter(typeof(StringEnumConverter))]
public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom
}