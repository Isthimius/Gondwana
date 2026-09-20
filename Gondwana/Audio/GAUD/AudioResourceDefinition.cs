namespace Gondwana.Audio.GAUD;

/// <summary>
/// Defines one named audio resource in a GAUD file.
/// </summary>
public sealed class AudioResourceDefinition
{
    public string Key { get; set; } = string.Empty;

    public AudioResourceSourceKind SourceKind { get; set; } = AudioResourceSourceKind.LooseFile;

    /// <summary>Loose audio file path, preferably relative to the GAUD file.</summary>
    public string? FilePath { get; set; }

    /// <summary>GAF path for a packed audio source, preferably relative to the GAUD file.</summary>
    public string? AssetsFilePath { get; set; }

    /// <summary>Audio entry name inside a packed GAF source.</summary>
    public string? AssetEntryName { get; set; }

    /// <summary>Source URI for URI-backed audio.</summary>
    public string? SourceUri { get; set; }

    /// <summary>Optional format hint such as .wav or .ogg, required when a packed entry name has no extension.</summary>
    public string? SourceExtension { get; set; }

    public float Volume { get; set; } = 1.0f;

    public float Pan { get; set; }

    public float PlaybackSpeed { get; set; } = 1.0f;

    public bool IsLooping { get; set; }
}
