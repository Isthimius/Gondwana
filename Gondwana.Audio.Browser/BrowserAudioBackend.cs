using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>Browser/WASM audio backend implemented with HTML media and Web Audio APIs.</summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserAudioBackend : IAudioBackend
{
    private BrowserAudioBackend() { }

    public static BrowserAudioBackend Instance { get; } = new();

    public string Name => "Browser Audio";

    public IAudioPlaybackHandle CreateFromBytes(
        string key,
        byte[] data,
        string fileNameOrExtension,
        float volume,
        float pan,
        float playbackSpeed)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new BrowserAudioPlaybackHandle(
            key,
            data,
            ResolveMimeType(fileNameOrExtension),
            volume,
            pan,
            playbackSpeed);
    }

    public IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed)
        => new BrowserAudioPlaybackHandle(key, uri, volume, pan, playbackSpeed);

    private static string ResolveMimeType(string fileNameOrExtension)
    {
        var extension = Path.GetExtension(fileNameOrExtension);
        if (string.IsNullOrWhiteSpace(extension) && fileNameOrExtension.StartsWith('.'))
            extension = fileNameOrExtension;

        return extension.ToLowerInvariant() switch
        {
            ".aac" => "audio/aac",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/ogg",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }
}
