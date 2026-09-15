using System.Runtime.Versioning;
using Gondwana.Audio;
using Gondwana.Audio.Browser;

namespace Gondwana;

/// <summary>Engine registration helpers for browser/WASM audio.</summary>
[SupportedOSPlatform("browser")]
public static class BrowserAudioEngineExtensions
{
    /// <summary>Configures <see cref="AudioResourceManager"/> to use the browser audio backend.</summary>
    public static Engine UseBrowserAudio(this Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AudioResourceManager.Instance.ConfigureBackend(BrowserAudioBackend.Instance);
        return engine;
    }

    /// <summary>
    /// Returns the compatibility <see cref="BrowserAudioManager"/> facade and configures the browser backend.
    /// New code may use <c>Engine.Managers.AudioResources</c> directly after <see cref="UseBrowserAudio"/>.
    /// </summary>
    public static BrowserAudioManager GetBrowserAudioManager(this Engine engine)
    {
        engine.UseBrowserAudio();
        return BrowserAudioManager.Instance;
    }
}
