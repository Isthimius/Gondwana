using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Gondwana.Audio;

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

    public static BrowserAudioManager Instance => _instance.Value;

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

    public void Unload(string key)
    {
        AudioResourceManager.Instance.Unload(key);
        _players.TryRemove(key, out _);
    }

    public void UnloadAll()
    {
        foreach (var key in _players.Keys.ToArray())
            Unload(key);
    }

    public bool TryGet(string key, out BrowserAudioPlayer? player) => _players.TryGetValue(key, out player);

    public BrowserAudioPlayer? Get(string key) => _players.TryGetValue(key, out var player) ? player : null;

    public bool Contains(string key) => _players.ContainsKey(key);
}
