using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Specifies the horizontal alignment of an element.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum HorizontalAlignment
{
    /// <summary>
    /// Align to the left.
    /// </summary>
    Left,
    /// <summary>
    /// Align to the center.
    /// </summary>
    Center,
    /// <summary>
    /// Align to the right.
    /// </summary>
    Right
}