namespace Gondwana.Video;

/// <summary>The availability of metadata for the active source.</summary>
public enum VideoMetadataStatus { Unavailable, Pending, Ready, Failed }

/// <summary>An immutable metadata snapshot. Values are authoritative only when status is Ready.</summary>
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
