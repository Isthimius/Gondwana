namespace Gondwana.Audio;

/// <summary>
/// Creates backend-specific playback handles for the engine's common audio API.
/// </summary>
public interface IAudioBackend
{
    string Name { get; }

    IAudioPlaybackHandle CreateFromBytes(
        string key,
        byte[] data,
        string fileNameOrExtension,
        float volume,
        float pan,
        float playbackSpeed);

    IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed);
}
