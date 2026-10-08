namespace Gondwana.Audio.GSND;

/// <summary>
/// Defines one named audio resource in a GSND file.
/// </summary>
public sealed class AudioResourceDefinition
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source kind.
    /// </summary>
    public AudioResourceSourceKind SourceKind { get; set; } = AudioResourceSourceKind.LooseFile;

    /// <summary>Loose audio file path, preferably relative to the GSND file.</summary>
    public string? FilePath { get; set; }

    /// <summary>GAF path for a packed audio source, preferably relative to the GSND file.</summary>
    public string? AssetsFilePath { get; set; }

    /// <summary>Audio entry name inside a packed GAF source.</summary>
    public string? AssetEntryName { get; set; }

    /// <summary>Source URI for URI-backed audio.</summary>
    public string? SourceUri { get; set; }

    /// <summary>Optional format hint such as .wav or .ogg, required when a packed entry name has no extension.</summary>
    public string? SourceExtension { get; set; }

    /// <summary>
    /// Gets or sets the playback volume.
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the stereo pan.
    /// </summary>
    public float Pan { get; set; }

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether playback repeats when it reaches the end.
    /// </summary>
    public bool IsLooping { get; set; }
}
