using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Specifies the vertical alignment of a sprite or element.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum VerticalAlignment
{
    /// <summary>
    /// Align to the top.
    /// </summary>
    Top,

    /// <summary>
    /// Align to the middle (center).
    /// </summary>
    Middle,

    /// <summary>
    /// Align to the bottom.
    /// </summary>
    Bottom
}