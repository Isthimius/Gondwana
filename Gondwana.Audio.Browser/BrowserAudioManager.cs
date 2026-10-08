using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>
/// Compatibility facade for browser audio. New code may use <see cref="AudioResourceManager"/>
/// directly after calling <c>Engine.UseBrowserAudio()</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserAudioManager
{
    private static readonly Lazy<BrowserAudioManager> _instance = new(() => new BrowserAudioManager());
    private readonly ConcurrentDictionary<string, BrowserAudioPlayer> _players = new();

    private BrowserAudioManager()
    {
        AudioResourceManager.Instance.ConfigureBackend(BrowserAudioBackend.Instance);
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static BrowserAudioManager Instance => _instance.Value;

    /// <summary>Retains the original CLR signature for compiled browser clients.</summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="src">The URI of the audio source.</param>
    /// <param name="volume">The playback volume.</param>
    /// <param name="loop">Whether playback repeats at the end.</param>
    /// <returns>The resulting browser audio player.</returns>
    public BrowserAudioPlayer Load(string key, string src, float volume, bool loop)
        => Load(key, src, volume, loop, 0.0f, 1.0f);

    /// <summary>
    /// Loads URI-backed audio and registers a browser player under the supplied key.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="src">The URI of the audio source.</param>
    /// <param name="volume">The playback volume.</param>
    /// <param name="loop">Whether playback repeats at the end.</param>
    /// <param name="pan">The stereo pan, from -1 (left) to 1 (right).</param>
    /// <param name="playbackSpeed">The playback speed multiplier.</param>
    /// <returns>The resulting browser audio player.</returns>
    public BrowserAudioPlayer Load(
        string key,
        string src,
        float volume = 1.0f,
        bool loop = false,
        float pan = 0.0f,
        float playbackSpeed = 1.0f)
    {
        AudioResourceManager.Instance.ConfigureBackend(BrowserAudioBackend.Instance);
        var resource = AudioResourceManager.Instance.LoadFromUri(key, src, volume, pan, playbackSpeed);
        resource.IsLooping = loop;

        var player = new BrowserAudioPlayer(resource);
        _players[key] = player;
        resource.Disposed += (_, _) =>
            ((ICollection<KeyValuePair<string, BrowserAudioPlayer>>)_players)
                .Remove(new(key, player));
        return player;
    }

    /// <summary>
    /// Unloads the keyed audio resource and removes its browser player.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    public void Unload(string key)
    {
        AudioResourceManager.Instance.Unload(key);
        _players.TryRemove(key, out _);
    }

    /// <summary>
    /// Unloads all audio resources tracked by this browser manager.
    /// </summary>
    public void UnloadAll()
    {
        foreach (var key in _players.Keys.ToArray())
            Unload(key);
    }

    /// <summary>
    /// Attempts to retrieve the browser player registered under the specified key.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="player">The registered player when found; otherwise, null.</param>
    /// <returns><see langword="true"/> if the player was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string key, out BrowserAudioPlayer? player) => _players.TryGetValue(key, out player);

    /// <summary>
    /// Gets the browser player registered under the specified key.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <returns>The registered browser player, or <see langword="null"/> if the key is absent.</returns>
    public BrowserAudioPlayer? Get(string key) => _players.TryGetValue(key, out var player) ? player : null;

    /// <summary>
    /// Determines whether a browser player is registered under the specified key.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <returns><see langword="true"/> if a player is registered for the key; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string key) => _players.ContainsKey(key);
}
