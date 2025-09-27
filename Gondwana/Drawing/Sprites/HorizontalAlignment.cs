using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites;

[JsonConverter(typeof(StringEnumConverter))]
public enum HorizontalAlignment
{
    Left,
    Center,
    Right
}
