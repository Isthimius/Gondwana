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

    public string Key => _resource.Key;
    public bool IsPlaying => _resource.IsPlaying;
    public bool IsPaused => _resource.IsPaused;
    public AudioPlaybackState State => _resource.State;
    public TimeSpan CurrentTime
    {
        get => _resource.CurrentTime;
        set => _resource.CurrentTime = value;
    }

    public TimeSpan Duration => _resource.Duration;

    public bool IsLooping
    {
        get => _resource.IsLooping;
        set => _resource.IsLooping = value;
    }

    public float Volume
    {
        get => _resource.Volume;
        set => _resource.Volume = value;
    }

    public float Pan
    {
        get => _resource.Pan;
        set => _resource.Pan = value;
    }

    public float PlaybackSpeed
    {
        get => _resource.PlaybackSpeed;
        set => _resource.PlaybackSpeed = value;
    }

    public void Play(bool fromStart = true) => _resource.Play(fromStart);
    public void Pause() => _resource.Pause();
    public void Resume() => _resource.Resume();
    public void Seek(TimeSpan position) => _resource.Seek(position);
    public void Stop() => _resource.Stop();
}
