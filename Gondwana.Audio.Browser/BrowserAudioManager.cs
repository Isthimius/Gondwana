using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>
/// Manages audio playback in browser/WASM environments using the HTML5 Audio API.
/// </summary>
/// <remarks>
/// <para>
/// Use this manager instead of <c>AudioResourceManager</c> when running on a
/// <c>net8.0-browser</c> target. It routes all audio operations to the
/// <c>gondwana-audio</c> JavaScript module rather than the NAudio-based desktop
/// pipeline (which is unavailable in WebAssembly).
/// </para>
/// <para>
/// Access the singleton instance through the engine extension method:
/// </para>
/// <code>
/// var mgr = Engine.Instance.GetBrowserAudioManager();
/// </code>
/// <para>
/// Before using this manager, ensure the JavaScript module is imported in
/// <c>Program.Browser.cs</c>:
/// </para>
/// <code>
/// await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
/// </code>
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserAudioManager
{
    private static readonly Lazy<BrowserAudioManager> _instance =
        new(() => new BrowserAudioManager());

    private readonly ConcurrentDictionary<string, BrowserAudioPlayer> _players = new();

    private BrowserAudioManager() { }

    /// <summary>Gets the singleton <see cref="BrowserAudioManager"/> instance.</summary>
    public static BrowserAudioManager Instance => _instance.Value;

    /// <summary>
    /// Loads an audio file and returns a <see cref="BrowserAudioPlayer"/> that controls it.
    /// </summary>
    /// <remarks>
    /// If a player with the same <paramref name="key"/> already exists it is stopped and
    /// replaced. The <paramref name="src"/> should be a URL relative to the application
    /// root (e.g. <c>"assets/theme.mp3"</c>).
    /// </remarks>
    /// <param name="key">Unique identifier for the track.</param>
    /// <param name="src">URL of the audio file.</param>
    /// <param name="volume">Initial volume in the range [0.0, 1.0]. Defaults to 1.0.</param>
    /// <param name="loop">Whether the track should loop. Defaults to <see langword="false"/>.</param>
    /// <returns>A <see cref="BrowserAudioPlayer"/> bound to the loaded track.</returns>
    public BrowserAudioPlayer Load(string key, string src, float volume = 1.0f, bool loop = false)
    {
        if (_players.TryGetValue(key, out var existing))
            existing.Stop();

        volume = Math.Clamp(volume, 0f, 1f);
        BrowserAudioInterop.Load(key, src, loop, volume);

        var player = new BrowserAudioPlayer(key, volume, loop);
        _players[key] = player;
        return player;
    }

    /// <summary>
    /// Unloads a track by key, stopping playback and releasing browser resources.
    /// </summary>
    /// <param name="key">The track identifier to unload.</param>
    public void Unload(string key)
    {
        BrowserAudioInterop.Unload(key);
        _players.TryRemove(key, out _);
    }

    /// <summary>
    /// Unloads all tracks, stopping playback and releasing all browser resources.
    /// </summary>
    public void UnloadAll()
    {
        foreach (var key in _players.Keys)
            BrowserAudioInterop.Unload(key);

        _players.Clear();
    }

    /// <summary>
    /// Attempts to retrieve a loaded player by key.
    /// </summary>
    /// <param name="key">The track identifier.</param>
    /// <param name="player">
    /// When this method returns <see langword="true"/>, contains the
    /// <see cref="BrowserAudioPlayer"/> for the track; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a player with the specified key is loaded;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGet(string key, out BrowserAudioPlayer? player)
        => _players.TryGetValue(key, out player);

    /// <summary>
    /// Returns the <see cref="BrowserAudioPlayer"/> for the given key, or
    /// <see langword="null"/> if no such player is loaded.
    /// </summary>
    public BrowserAudioPlayer? Get(string key)
        => _players.TryGetValue(key, out var p) ? p : null;

    /// <summary>
    /// Returns <see langword="true"/> if a player with the given key is loaded.
    /// </summary>
    public bool Contains(string key) => _players.ContainsKey(key);
}
