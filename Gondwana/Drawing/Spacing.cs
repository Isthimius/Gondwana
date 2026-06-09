using Newtonsoft.Json;

namespace Gondwana.Drawing;

/// <summary>
/// Represents dimensions in pixels when spacing Tilesheets
/// </summary>
/// <param name="Left">The number of pixels on the left boundary.</param>
/// <param name="Top">The number of pixels on the top boundary.</param>
/// <param name="Right">The number of pixels on the right boundary.</param>
/// <param name="Bottom">The number of pixels on the bottom boundary.</param>
public record struct Spacing(
    [property: JsonProperty("left")] int Left,
    [property: JsonProperty("top")] int Top,
    [property: JsonProperty("right")] int Right,
    [property: JsonProperty("bottom")] int Bottom)
{
    /// <summary>
    /// Represents a spacing with no extension in any direction (all values are zero).
    /// </summary>
    public static readonly Spacing None = new(0, 0, 0, 0);

    /// <summary>
    /// Gets a value indicating whether this spacing has no extension in any direction.
    /// Returns <see langword="true"/> if all spacing values (Left, Top, Right, Bottom) are zero;
    /// otherwise, <see langword="false"/>.
    /// </summary>
    [JsonIgnore]
    public readonly bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}
