using System.Runtime.Versioning;
using Gondwana.Audio;

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
        => throw new NotSupportedException(
            "Gondwana.Audio.Browser currently loads URI-addressable browser assets. Use AudioResourceManager.LoadFromUri().");

    public IAudioPlaybackHandle CreateFromUri(
        string key,
        string uri,
        float volume,
        float pan,
        float playbackSpeed)
        => new BrowserAudioPlaybackHandle(key, uri, volume, pan, playbackSpeed);
}
