namespace Gondwana.Audio;

/// <summary>
/// Describes the current playback state of an audio resource without exposing
/// backend-specific playback types.
/// </summary>
public enum AudioPlaybackState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2
}
