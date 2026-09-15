namespace Gondwana.Audio;

/// <summary>
/// Creates backend-specific playback handles for the engine's common audio API.
/// </summary>
public interface IAudioBackend
{
    /// <summary>
    /// Gets the human-readable name of the backend implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates a playback handle from raw audio bytes.
    /// The implementation is responsible for interpreting the provided data according to the file hint.
    /// </summary>
    /// <param name="key">A unique key for the audio resource.</param>
    /// <param name="data">Raw audio data bytes.</param>
    /// <param name="fileNameOrExtension">A file name or extension used as a hint for format detection (for example ".wav").</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <param name="playbackSpeed">Initial playback speed.</param>
    /// <returns>An <see cref="IAudioPlaybackHandle"/> representing the created playback instance.</returns>
    IAudioPlaybackHandle CreateFromBytes(
        string key,
        byte[] data,
        string fileNameOrExtension,
        float volume,
        float pan,
        float playbackSpeed);

    /// <summary>
    /// Creates a playback handle that plays audio from the specified URI.
    /// This is intended for backends that can stream or reference remote/local URIs directly (for example browser audio backends).
    /// </summary>
    /// <param name="key">A unique key for the audio resource.</param>
    /// <param name="uri">The URI that identifies the audio resource.</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <param name="playbackSpeed">Initial playback speed.</param>
    /// <returns>An <see cref="IAudioPlaybackHandle"/> representing the created playback instance.</returns>
    IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed);
}
