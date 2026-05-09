namespace Gondwana.Assets;

/// <summary>
/// Defines the types of assets that can be stored and managed by the engine.
/// </summary>
/// <remarks>This enumeration is used to categorize assets in <see cref="AssetsFile"/> and related asset management components.</remarks>
public enum AssetTypes
{
    /// <summary>
    /// Represents an image file type
    /// </summary>
    Image = 0,

    /// <summary>
    /// Represents the audio media type for <see cref="Gondwana.Audio.AudioResourceManager"/>
    /// </summary>
    Audio = 1,

    /// <summary>
    /// Video; supported via platform-specific media players
    /// </summary>
    Video = 2,

    /// <summary>
    /// Mouse cursor; not currently supported
    /// </summary>
    Cursor = 3,

    /// <summary>
    /// Specifies that the content type is a font.
    /// </summary>
    Font = 4,

    /// <summary>
    /// not currently supported
    /// </summary>
    Misc = 5,

    /// <summary>
    /// Represents a scalable vector graphic (SVG) asset.
    /// </summary>
    Svg = 6
}
