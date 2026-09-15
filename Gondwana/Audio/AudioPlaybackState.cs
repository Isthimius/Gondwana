namespace Gondwana.Audio;

/// <summary>
/// Describes the current playback state of an audio resource without exposing
/// backend-specific playback types.
/// </summary>
public enum AudioPlaybackState
{
    /// <summary>
    /// Playback is stopped. No audio is currently playing and the position is at the start (or has been reset).
    /// </summary>
    Stopped = 0,

    /// <summary>
    /// Playback is in progress.
    /// </summary>
    Playing = 1,

    /// <summary>
    /// Playback is paused and can be resumed from the current position.
    /// </summary>
    Paused = 2
}
