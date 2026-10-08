namespace Gondwana.Video;

/// <summary>The availability of metadata for the active source.</summary>
public enum VideoMetadataStatus
{
    /// <summary>
    /// No metadata is available for the current source.
    /// </summary>
    Unavailable,
    /// <summary>
    /// Metadata discovery is in progress.
    /// </summary>
    Pending,
    /// <summary>
    /// Metadata discovery completed successfully.
    /// </summary>
    Ready,
    /// <summary>
    /// Metadata discovery failed.
    /// </summary>
    Failed
}

/// <summary>An immutable metadata snapshot. Values are authoritative only when status is Ready.</summary>
/// <param name="Status">The availability of the source metadata.</param>
/// <param name="HasAudio">Whether the source contains an audio track.</param>
/// <param name="Duration">The source duration. This value is authoritative only when metadata is ready.</param>
public sealed record VideoMetadata(VideoMetadataStatus Status, bool HasAudio, TimeSpan Duration)
{
    internal static readonly VideoMetadata Unavailable = new(VideoMetadataStatus.Unavailable, false, TimeSpan.Zero);
}

// A generation prevents completion from a replaced source from publishing stale metadata.
internal sealed class VideoMetadataState
{
    private readonly object _gate = new();
    private long _generation;
    private VideoMetadata _current = VideoMetadata.Unavailable;
    internal VideoMetadata Current { get { lock (_gate) return _current; } }
    internal long Begin()
    {
        lock (_gate)
        {
            _current = new(VideoMetadataStatus.Pending, false, TimeSpan.Zero);
            return ++_generation;
        }
    }
    internal bool Complete(long generation, VideoMetadata metadata)
    {
        lock (_gate)
        {
            if (generation != _generation) return false;
            _current = metadata;
            return true;
        }
    }
    internal void Reset()
    {
        lock (_gate) { ++_generation; _current = VideoMetadata.Unavailable; }
    }
}
