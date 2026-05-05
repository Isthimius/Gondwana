using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>
/// Low-level JavaScript interop bindings for the <c>gondwana-audio</c> JS module.
/// </summary>
/// <remarks>
/// <para>
/// The JS module must be imported before any method on this class is called.
/// The recommended place to do this is in <c>Program.Browser.cs</c> before the
/// Avalonia app is started:
/// </para>
/// <code>
/// await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
/// </code>
/// <para>
/// The <c>gondwana-audio.js</c> file ships as a NuGet content file and is
/// automatically placed in the consuming project's <c>wwwroot/</c> directory.
/// </para>
/// </remarks>
[SupportedOSPlatform("browser")]
internal static partial class BrowserAudioInterop
{
    private const string Module = "gondwana-audio";

    /// <summary>Loads a new audio track.</summary>
    [JSImport("load", Module)]
    internal static partial void Load(string key, string src, bool loop, float volume);

    /// <summary>Starts or resumes playback of a loaded track.</summary>
    [JSImport("play", Module)]
    internal static partial void Play(string key, bool fromStart);

    /// <summary>Pauses a loaded track without resetting its position.</summary>
    [JSImport("pause", Module)]
    internal static partial void Pause(string key);

    /// <summary>Stops a loaded track and resets it to the beginning.</summary>
    [JSImport("stop", Module)]
    internal static partial void Stop(string key);

    /// <summary>Sets the volume of a loaded track.</summary>
    [JSImport("setVolume", Module)]
    internal static partial void SetVolume(string key, float volume);

    /// <summary>Sets the looping behaviour of a loaded track.</summary>
    [JSImport("setLoop", Module)]
    internal static partial void SetLoop(string key, bool loop);

    /// <summary>Unloads a track and releases its browser resources.</summary>
    [JSImport("unload", Module)]
    internal static partial void Unload(string key);
}
