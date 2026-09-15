using Gondwana.Audio;

namespace Gondwana.Audio.NAudio;

/// <summary>Windows desktop audio backend implemented with NAudio.</summary>
public sealed class NAudioAudioBackend : IAudioBackend
{
    private NAudioAudioBackend() { }

    public static NAudioAudioBackend Instance { get; } = new();

    public string Name => "NAudio";

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

    public IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed)
        => throw new NotSupportedException(
            "Gondwana.Audio.NAudio loads files, streams, and engine assets. URI playback is provided by URI-capable backends such as Gondwana.Audio.Browser.");
}
