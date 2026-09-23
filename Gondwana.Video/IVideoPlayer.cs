namespace Gondwana.Video;

/// <summary>
/// Defines the contract for a video player that can play, pause, stop, and seek video content.
/// </summary>
public interface IVideoPlayer : IDisposable
{
    // Control
    
    /// <summary>
    /// Opens a video from the specified source URI.
    /// </summary>
    /// <param name="source">The URI of the video source to open.</param>
    void Open(Uri source);

    /// <summary>Opens a readable stream. Ownership transfers on success unless leaveOpen is true.
    /// Keep a borrowed stream alive and unused until the next Open or Dispose. Seek/loop require
    /// a seekable stream. Backends without stream support throw without taking ownership.</summary>
    void Open(Stream source, bool leaveOpen = false) => throw new NotSupportedException("This video backend does not support streams.");

    /// <summary>Gets an atomic snapshot of metadata availability and values for the current source.</summary>
    VideoMetadata Metadata => VideoMetadata.Unavailable;

    /// <summary>True only when metadata for the current source is available.</summary>
    bool IsMetadataReady => Metadata.Status == VideoMetadataStatus.Ready;

    /// <summary>
    /// Starts or resumes playback of the video.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses the video playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// Stops the video playback and resets the position.
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to the specified position in the video.
    /// </summary>
    /// <param name="position">The position to seek to in the video timeline.</param>
    void Seek(TimeSpan position);

    /// <summary>
    /// Sets the playback rate of the video.
    /// </summary>
    /// <param name="rate">The playback rate multiplier (e.g., 1.0 for normal speed, 2.0 for double speed).</param>
    void SetRate(double rate);

    /// <summary>
    /// Gets or sets a value indicating whether the video should loop when it reaches the end.
    /// </summary>
    bool Loop { get; set; }

    // Info
    
    /// <summary>
    /// Gets a value indicating whether the video is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Gets the total duration of the video.
    /// </summary>
    TimeSpan Duration { get; }
    
    /// <summary>
    /// Gets the current playback position in the video.
    /// </summary>
    TimeSpan Position { get; }
    
    /// <summary>
    /// Gets the natural size (width and height) of the video in pixels.
    /// </summary>
    (int width, int height) NaturalSize { get; }
    
    /// <summary>
    /// Gets whether the video has an audio track. Consult IsMetadataReady before interpreting false.
    /// </summary>
    bool HasAudio { get; }

    // Events
    
    /// <summary>
    /// Occurs when the video playback starts.
    /// </summary>
    event EventHandler Started;

    /// <summary>
    /// Occurs when the video playback is paused.
    /// </summary>
    event EventHandler Paused;

    /// <summary>
    /// Occurs when the video playback is stopped.
    /// </summary>
    event EventHandler Stopped;

    /// <summary>
    /// Occurs when the video playback reaches the end.
    /// </summary>
    event EventHandler Ended;

    /// <summary>
    /// Occurs when the video state changes, including MediaOpening, MediaOpened, MetadataReady,
    /// and MetadataFailed. MediaOpening must precede frames for a replacement source, after
    /// callbacks for the prior source have stopped. Events may run on background threads.
    /// </summary>
    event EventHandler<VideoStateChangedEventArgs> StateChanged;    // generic hook

    /// <summary>
    /// Occurs when a decoded BGRX frame is ready. The pointer is valid only during the callback.
    /// Copy it before returning. Do not call engine/render operations or player controls here.
    /// </summary>
    event EventHandler<VideoFrameReadyEventArgs> FrameReady;        // decoded frame callback
}
