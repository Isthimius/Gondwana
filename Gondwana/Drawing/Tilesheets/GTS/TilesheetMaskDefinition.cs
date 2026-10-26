using System.ComponentModel;
namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Defines a color mask used to identify and process specific colors within a tilesheet image.
/// </summary>
/// <remarks>
/// The mask uses RGBA color components with a tolerance value to match colors within a specified range.
/// This is commonly used for transparency masking, color keying, or identifying specific tile boundaries.
/// </remarks>
public sealed class TilesheetMaskDefinition
{
    /// <summary>
    /// Gets or sets the red component of the mask color (0-255).
    /// </summary>
    [Description("Red component (0 to 255) of the image color matched by the mask.")]
    public byte Red { get; set; }

    /// <summary>
    /// Gets or sets the green component of the mask color (0-255).
    /// </summary>
    [Description("Green component (0 to 255) of the image color matched by the mask.")]
    public byte Green { get; set; }

    /// <summary>
    /// Gets or sets the blue component of the mask color (0-255).
    /// </summary>
    [Description("Blue component (0 to 255) of the image color matched by the mask.")]
    public byte Blue { get; set; }

    /// <summary>
    /// Gets or sets the alpha (opacity) component of the mask color (0-255).
    /// </summary>
    /// <value>Defaults to 255 (fully opaque).</value>
    [Description("Alpha component (0 to 255) of the mask color; 255 is fully opaque.")]
    public byte Alpha { get; set; } = 255;

    /// <summary>
    /// Gets or sets the tolerance value for color matching, allowing colors within a range to be considered matching.
    /// </summary>
    /// <value>Defaults to 5. Higher values allow more variation in color matching.</value>
    /// <remarks>
    /// A tolerance of 0 requires exact color matches, while higher values allow colors within the specified
    /// range to match. For example, a tolerance of 5 would match colors where each component (R, G, B, A)
    /// differs by no more than 5 from the mask color.
    /// </remarks>
    [Description("Maximum allowed difference per color component when matching the mask color. 0 requires an exact match; larger values match more colors.")]
    public byte Tolerance { get; set; } = 5;
}
