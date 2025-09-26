namespace Gondwana.Video;

public interface IVideoPlayer : IDisposable
{
    // Control
    void Open(Uri source);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void SetRate(double rate);
    bool Loop { get; set; }

    // Info
    bool IsPlaying { get; }
    TimeSpan Duration { get; }
    TimeSpan Position { get; }
    (int width, int height) NaturalSize { get; }
    bool HasAudio { get; }

    // Events
    event EventHandler Started;
    event EventHandler Paused;
    event EventHandler Stopped;
    event EventHandler Ended;
    event EventHandler<VideoStateChangedEventArgs> StateChanged;    // generic hook
    event EventHandler<VideoFrameReadyEventArgs> FrameReady;        // decoded frame callback
}
