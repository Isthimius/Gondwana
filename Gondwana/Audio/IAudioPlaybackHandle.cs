namespace Gondwana.Audio;

/// <summary>
/// Backend-provided playback handle used by <see cref="AudioResource"/>.
/// Implements <see cref="IDisposable"/> to allow releasing any native or temporary resources
/// associated with playback (for example temporary files or native audio handles).
/// </summary>
public interface IAudioPlaybackHandle : IDisposable
{
    /// <summary>
    /// Raised when playback has reached the end (and is not looping) or otherwise completes.
    /// </summary>
    event EventHandler? PlaybackCompleted;

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    AudioPlaybackState State { get; }

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    TimeSpan CurrentTime { get; }

    /// <summary>
    /// Gets the total duration of the loaded audio resource.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets or sets whether playback should loop when reaching the end.
    /// </summary>
    bool IsLooping { get; set; }

    /// <summary>
    /// Gets or sets the playback volume. Expected range is implementation-defined (commonly 0.0 to 1.0).
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Gets or sets the stereo pan. Expected range is implementation-defined (commonly -1.0 (left) to 1.0 (right)).
    /// </summary>
    float Pan { get; set; }

    /// <summary>
    /// Gets or sets the playback speed multiplier (1.0 = normal speed).
    /// </summary>
    float PlaybackSpeed { get; set; }

    /// <summary>
    /// If the backend created a temporary file for playback (for example when streaming or converting),
    /// returns the file path. Otherwise <c>null</c>.
    /// </summary>
    string? TemporaryFilePath { get; }

    /// <summary>
    /// Start playback. If <paramref name="fromStart"/> is <c>true</c>, playback will begin from the
    /// start of the resource; otherwise playback begins from the current position.
    /// </summary>
    /// <param name="fromStart">Whether playback should start from the beginning.</param>
    void Play(bool fromStart = true);

    /// <summary>
    /// Pause playback. Calling <see cref="Resume"/> should continue from the paused position.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume playback after a pause.
    /// </summary>
    void Resume();

    /// <summary>
    /// Seek to the specified position within the resource.
    /// </summary>
    /// <param name="position">The position to seek to.</param>
    void Seek(TimeSpan position);

    /// <summary>
    /// Stop playback and reset the current position to the start.
    /// </summary>
    void Stop();
}
