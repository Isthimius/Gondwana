namespace Gondwana.Audio.NAudio;

/// <summary>Windows desktop audio backend implemented with NAudio.</summary>
public sealed class NAudioAudioBackend : IAudioBackend
{
    private NAudioAudioBackend() { }

    /// <summary>
    /// Gets the singleton instance of the <see cref="NAudioAudioBackend"/>.
    /// </summary>
    public static NAudioAudioBackend Instance { get; } = new();

    /// <summary>
    /// Gets the display name of this audio backend.
    /// </summary>
    public string Name => "NAudio";

    /// <summary>
    /// Creates an <see cref="IAudioPlaybackHandle"/> from the provided raw audio bytes.
    /// The data is opened via the <see cref="NAudioReaderRegistry"/> which may create a temporary file.
    /// The returned playback handle is responsible for disposing the underlying reader when finished.
    /// </summary>
    /// <param name="key">Logical key for the audio resource (used for logging).</param>
    /// <param name="data">Raw audio data to load.</param>
    /// <param name="fileNameOrExtension">A hint containing the original file name or extension to aid format detection.</param>
    /// <param name="volume">Initial playback volume in the range [0,1].</param>
    /// <param name="pan">Initial stereo pan in the range [-1,1].</param>
    /// <param name="playbackSpeed">Initial playback speed multiplier (clamped to supported range).</param>
    /// <returns>An <see cref="IAudioPlaybackHandle"/> instance that controls playback for the loaded audio resource.</returns>
    public IAudioPlaybackHandle CreateFromBytes(
        string key,
        byte[] data,
        string fileNameOrExtension,
        float volume,
        float pan,
        float playbackSpeed)
    {
        var (reader, tempPath) = NAudioReaderRegistry.Open(data, fileNameOrExtension);
        try
        {
            return new NAudioPlaybackHandle(key, reader, tempPath, volume, pan, playbackSpeed);
        }
        catch
        {
            reader.Dispose();
            NAudioReaderRegistry.TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Creates an <see cref="IAudioPlaybackHandle"/> from a URI.
    /// </summary>
    /// <param name="key">Logical key for the audio resource (used for logging).</param>
    /// <param name="uri">The URI to load audio from.</param>
    /// <param name="volume">Initial playback volume in the range [0,1].</param>
    /// <param name="pan">Initial stereo pan in the range [-1,1].</param>
    /// <param name="playbackSpeed">Initial playback speed multiplier.</param>
    /// <returns>An <see cref="IAudioPlaybackHandle"/> for the requested resource.</returns>
    /// <exception cref="NotSupportedException">Always thrown. URI-based playback is not supported by this backend.</exception>
    public IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed)
        => throw new NotSupportedException(
            "Gondwana.Audio.NAudio loads files, streams, and engine assets. URI playback is provided by URI-capable backends such as Gondwana.Audio.Browser.");
}
