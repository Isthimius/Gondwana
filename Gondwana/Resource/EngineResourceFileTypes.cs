namespace Gondwana.Resource;

public enum EngineResourceFileTypes
{
    /// <summary>
    /// Represents an image file type
    /// </summary>
    Image = 0,

    /// <summary>
    /// Represents the audio media type for <see cref="Gondwana.Audio.SoundResourceManager"/>
    /// </summary>
    Audio = 1,

    /// <summary>
    /// Video; not currently supported
    /// </summary>
    Video = 2,

    /// <summary>
    /// Mouse cursor; not currently supported
    /// </summary>
    Cursor = 3,

    /// <summary>
    /// not currently supported
    /// </summary>
    Misc = 4
}