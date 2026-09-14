namespace Gondwana.Audio;

/// <summary>
/// Backend-provided playback handle used by <see cref="AudioResource"/>.
/// </summary>
public interface IAudioPlaybackHandle : IDisposable
{
    event EventHandler? PlaybackCompleted;

    AudioPlaybackState State { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }
    bool IsLooping { get; set; }
    float Volume { get; set; }
    float Pan { get; set; }
    float PlaybackSpeed { get; set; }
    string? TemporaryFilePath { get; }

    void Play(bool fromStart = true);
    void Pause();
    void Resume();
    void Seek(TimeSpan position);
    void Stop();
}
