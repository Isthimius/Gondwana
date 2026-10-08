using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>
/// Compatibility wrapper around the common <see cref="AudioResource"/> browser implementation.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserAudioPlayer
{
    private readonly AudioResource _resource;

    internal BrowserAudioPlayer(AudioResource resource)
    {
        _resource = resource;
    }

    /// <summary>
    /// Gets the key.
    /// </summary>
    public string Key => _resource.Key;
    /// <summary>
    /// Gets whether playback is active.
    /// </summary>
    public bool IsPlaying => _resource.IsPlaying;
    /// <summary>
    /// Gets whether playback is paused.
    /// </summary>
    public bool IsPaused => _resource.IsPaused;
    /// <summary>
    /// Gets the state.
    /// </summary>
    public AudioPlaybackState State => _resource.State;
    /// <summary>
    /// Gets or sets the current playback position.
    /// </summary>
    public TimeSpan CurrentTime
    {
        get => _resource.CurrentTime;
        set => _resource.CurrentTime = value;
    }

    /// <summary>
    /// Gets the media duration.
    /// </summary>
    public TimeSpan Duration => _resource.Duration;

    /// <summary>
    /// Gets or sets whether playback repeats when it reaches the end.
    /// </summary>
    public bool IsLooping
    {
        get => _resource.IsLooping;
        set => _resource.IsLooping = value;
    }

    /// <summary>
    /// Gets or sets the playback volume.
    /// </summary>
    public float Volume
    {
        get => _resource.Volume;
        set => _resource.Volume = value;
    }

    /// <summary>
    /// Gets or sets the stereo pan.
    /// </summary>
    public float Pan
    {
        get => _resource.Pan;
        set => _resource.Pan = value;
    }

    /// <summary>
    /// Gets or sets the playback speed multiplier.
    /// </summary>
    public float PlaybackSpeed
    {
        get => _resource.PlaybackSpeed;
        set => _resource.PlaybackSpeed = value;
    }

    /// <summary>
    /// Starts playback.
    /// </summary>
    /// <param name="fromStart">Whether to restart playback from the beginning.</param>
    public void Play(bool fromStart = true) => _resource.Play(fromStart);
    /// <summary>
    /// Pauses playback at the current position.
    /// </summary>
    public void Pause() => _resource.Pause();
    /// <summary>
    /// Resumes paused playback.
    /// </summary>
    public void Resume() => _resource.Resume();
    /// <summary>
    /// Moves playback to the requested time.
    /// </summary>
    /// <param name="position">The requested playback position.</param>
    public void Seek(TimeSpan position) => _resource.Seek(position);
    /// <summary>
    /// Stops playback.
    /// </summary>
    public void Stop() => _resource.Stop();
}
