using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>Low-level JavaScript bindings for the <c>gondwana-audio</c> module.</summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserAudioInterop
{
    private const string Module = "gondwana-audio";

    [JSImport("load", Module)]
    internal static partial void Load(string key, string src, bool loop, float volume, float pan, float playbackSpeed);

    [JSImport("play", Module)]
    internal static partial void Play(string key, bool fromStart);

    [JSImport("pause", Module)]
    internal static partial void Pause(string key);

    [JSImport("stop", Module)]
    internal static partial void Stop(string key);

    [JSImport("setVolume", Module)]
    internal static partial void SetVolume(string key, float volume);

    [JSImport("setLoop", Module)]
    internal static partial void SetLoop(string key, bool loop);

    [JSImport("setPan", Module)]
    internal static partial void SetPan(string key, float pan);

    [JSImport("setPlaybackSpeed", Module)]
    internal static partial void SetPlaybackSpeed(string key, float playbackSpeed);

    [JSImport("setCurrentTime", Module)]
    internal static partial void SetCurrentTime(string key, double seconds);

    [JSImport("getCurrentTime", Module)]
    internal static partial double GetCurrentTime(string key);

    [JSImport("getDuration", Module)]
    internal static partial double GetDuration(string key);

    [JSImport("getState", Module)]
    internal static partial int GetState(string key);

    [JSImport("unload", Module)]
    internal static partial void Unload(string key);
}
