namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Represents the image source definition for a tilesheet, specifying where the image data can be loaded from.
/// </summary>
/// <remarks>
/// This class supports multiple image loading strategies: directly from a file path, from an assets file,
/// or from a named entry within an assets file. Only one of these strategies should be populated at a time.
/// </remarks>
public sealed class TilesheetImageDefinition
{
    /// <summary>
    /// Gets or sets the direct file path to the tilesheet image.
    /// </summary>
    /// <remarks>
    /// When specified, the tilesheet image will be loaded directly from this file path.
    /// This property is mutually exclusive with <see cref="AssetsFilePath"/> and <see cref="AssetEntryName"/>.
    /// </remarks>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the assets file containing the tilesheet image.
    /// </summary>
    /// <remarks>
    /// When specified along with <see cref="AssetEntryName"/>, the tilesheet image will be loaded
    /// from the named entry within this assets file. This property should be used in conjunction
    /// with <see cref="AssetEntryName"/> and is mutually exclusive with <see cref="FilePath"/>.
    /// </remarks>
    public string? AssetsFilePath { get; set; }

    /// <summary>
    /// Gets or sets the name of the specific entry within an assets file that contains the tilesheet image.
    /// </summary>
    /// <remarks>
    /// This property works in conjunction with <see cref="AssetsFilePath"/> to identify a specific
    /// image entry within an assets file. It is mutually exclusive with <see cref="FilePath"/>.
    /// </remarks>
    public string? AssetEntryName { get; set; }
}