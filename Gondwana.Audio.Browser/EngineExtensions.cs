using System.Runtime.Versioning;
using Gondwana.Audio.Browser;

namespace Gondwana;

/// <summary>
/// Provides extension methods for the <see cref="Engine"/> class to support
/// browser/WASM audio functionality via the HTML5 Audio API.
/// </summary>
[SupportedOSPlatform("browser")]
public static class BrowserAudioEngineExtensions
{
    /// <summary>
    /// Returns the <see cref="BrowserAudioManager"/> singleton for use on
    /// browser/WASM targets.
    /// </summary>
    /// <param name="engine">The <see cref="Engine"/> instance.</param>
    /// <returns>The singleton <see cref="BrowserAudioManager"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be called when <see cref="OperatingSystem.IsBrowser()"/>
    /// returns <see langword="true"/>. On desktop targets, use
    /// <c>Engine.Managers.AudioResources</c> (the NAudio-based pipeline) instead.
    /// </para>
    /// <para>
    /// Before calling this method, ensure the JavaScript module is imported in
    /// <c>Program.Browser.cs</c>:
    /// </para>
    /// <code>
    /// await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
    /// </code>
    /// </remarks>
    public static BrowserAudioManager GetBrowserAudioManager(this Engine engine)
        => BrowserAudioManager.Instance;
}
