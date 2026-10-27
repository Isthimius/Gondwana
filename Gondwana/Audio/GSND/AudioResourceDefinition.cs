using System.ComponentModel;
namespace Gondwana.Audio.GSND;

/// <summary>
/// Defines one named audio resource in a GSND file.
/// </summary>
public sealed class AudioResourceDefinition
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    [Category("Identity")]
    [Description("Unique resource name used by the audio system to find and play this sound.")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source kind.
    /// </summary>
    [Category("Source")]
    [Description("Where the audio is loaded from: a loose file, an entry in a GAF package, or a URI.")]
    public AudioResourceSourceKind SourceKind { get; set; } = AudioResourceSourceKind.LooseFile;

    /// <summary>Loose audio file path, preferably relative to the GSND file.</summary>
    [Category("Source")]
    [Description("Audio file location for a loose-file source, normally relative to the GSND document.")]
    public string? FilePath { get; set; }

    /// <summary>GAF path for a packed audio source, preferably relative to the GSND file.</summary>
    [Category("Source")]
    [Description("GAF package file containing the audio, normally relative to the GSND document.")]
    public string? AssetsFilePath { get; set; }

    /// <summary>Audio entry name inside a packed GAF source.</summary>
    [Category("Source")]
    [Description("Name of the audio entry inside the selected GAF package.")]
    public string? AssetEntryName { get; set; }

    /// <summary>Source URI for URI-backed audio.</summary>
    [Category("Source")]
    [Description("URI used to fetch audio when SourceKind selects a URI source.")]
    public string? SourceUri { get; set; }

    /// <summary>Optional format hint such as .wav or .ogg, required when a packed entry name has no extension.</summary>
    [Category("Source")]
    [Description("Optional format hint such as .wav or .ogg; needed for packed sources whose entry name has no file extension.")]
    public string? SourceExtension { get; set; }

    /// <summary>
    /// Gets or sets the playback volume.
    /// </summary>
    [Category("Playback")]
    [Description("Playback volume multiplier. 1 is the original level; 0 is silent; values between 0 and 1 are quieter.")]
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the stereo pan.
    /// </summary>
    [Category("Playback")]
    [Description("Stereo balance. -1 favors the left channel, 0 centers, and +1 favors the right channel.")]
    public float Pan { get; set; }

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    [Category("Playback")]
    [Description("Rate multiplier for playback. 1 is normal speed; below 1 is slower; above 1 is faster.")]
    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether playback repeats when it reaches the end.
    /// </summary>
    [Category("Playback")]
    [Description("Whether the sound automatically restarts after reaching its end.")]
    public bool IsLooping { get; set; }
}
