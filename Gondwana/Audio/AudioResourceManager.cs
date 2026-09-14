using System.Collections.Concurrent;
using Gondwana.Assets;
using Microsoft.Extensions.Logging;

namespace Gondwana.Audio;

/// <summary>
/// Manages the lifecycle of audio resources independently of the playback backend.
/// </summary>
public sealed class AudioResourceManager : IDisposable
{
    private static readonly Lazy<AudioResourceManager> _instance = new(() => new AudioResourceManager());
    private readonly ConcurrentDictionary<string, AudioResource> _soundResources = new(StringComparer.Ordinal);
    private readonly object _backendLock = new();
    private IAudioBackend? _backend;
    private bool _disposed;

    public event EventHandler<(string Key, AudioResource Resource)>? SoundDisposed;

    private AudioResourceManager() { }

    public static AudioResourceManager Instance => _instance.Value;

    /// <summary>Gets the currently configured audio backend, or null if none has been configured.</summary>
    public IAudioBackend? Backend => _backend;

    /// <summary>Gets whether an audio backend has been configured.</summary>
    public bool IsBackendConfigured => _backend is not null;

    /// <summary>
    /// Configures the backend used for subsequently loaded audio resources.
    /// A backend cannot be replaced while resources are loaded.
    /// </summary>
    public void ConfigureBackend(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        lock (_backendLock)
        {
            if (ReferenceEquals(_backend, backend))
                return;

            if (_backend is not null && _soundResources.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Cannot replace audio backend '{_backend.Name}' with '{backend.Name}' while audio resources are loaded. Clear the audio resource manager first.");
            }

            _backend = backend;
            Engine.Logger.LogInformation("Configured Gondwana audio backend: {AudioBackend}", backend.Name);
        }
    }

    public AudioResource LoadFromFile(
        string key,
        string filePath,
        float volume = 1.0f,
        float pan = 0.0f,
        float playbackSpeed = 1.0f)
    {
        var bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(key, bytes, filePath, volume, pan, playbackSpeed);
    }

    public AudioResource LoadFromStream(
        string key,
        Stream input,
        string fileExt,
        float volume = 1.0f,
        float pan = 0.0f,
        float playbackSpeed = 1.0f)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return LoadFromBytes(key, ms.ToArray(), fileExt, volume, pan, playbackSpeed);
    }

    /// <summary>
    /// Loads an audio source by URI. This is primarily intended for URI-capable backends such as browser audio.
    /// </summary>
    public AudioResource LoadFromUri(
        string key,
        string uri,
        float volume = 1.0f,
        float pan = 0.0f,
        float playbackSpeed = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("Audio URI cannot be empty.", nameof(uri));

        ReplaceExisting(key);
        var speed = ClampPlaybackSpeed(playbackSpeed);
        var playback = RequireBackend().CreateFromUri(key, uri, Math.Clamp(volume, 0f, 1f), Math.Clamp(pan, -1f, 1f), speed);
        var resource = new AudioResource(key, playback, volume, pan, speed);
        resource.SetSourceUri(uri);
        RegisterLoadedSound(key, resource);
        return resource;
    }

    public List<AudioResource> LoadFromEngineAssetsFile(
        AssetsFile resourceFile,
        float defaultVolume = 1.0f,
        float defaultPan = 0.0f,
        float defaultPlaybackSpeed = 1.0f)
    {
        List<AudioResource> loadedSounds = [];

        foreach (var entry in resourceFile.GetAllEntries())
        {
            if (entry.AssetType != AssetTypes.Audio)
                continue;

            if (_soundResources.ContainsKey(entry.AssetName))
            {
                Engine.Logger.LogDebug("AudioResource '{Key}' already loaded. Skipping.", entry.AssetName);
                continue;
            }

            var stream = resourceFile.Get(entry.AssetType, entry.AssetName);
            if (stream is null)
            {
                Engine.Logger.LogWarning("Failed to retrieve stream for audio resource: {Key}", entry.AssetName);
                continue;
            }

            try
            {
                using (stream)
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    loadedSounds.Add(LoadFromBytes(
                        entry.AssetName,
                        ms.ToArray(),
                        entry.AssetName,
                        defaultVolume,
                        defaultPan,
                        defaultPlaybackSpeed));
                }

                Engine.Logger.LogInformation("Loaded sound: {Key}", entry.AssetName);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error loading sound from asset file for key: {Key}", entry.AssetName);
                throw;
            }
        }

        return loadedSounds;
    }

    public AudioResource? Clone(
        string key,
        string? newKey = null,
        float? volume = null,
        float? pan = null,
        float? playbackSpeed = null)
    {
        if (!_soundResources.TryGetValue(key, out var original))
        {
            Engine.Logger.LogWarning("Attempted to clone non-existent AudioResource with key: {Key}", key);
            return null;
        }

        newKey ??= $"{key}_clone_{Guid.NewGuid()}";
        if (_soundResources.ContainsKey(newKey))
        {
            Engine.Logger.LogWarning("AudioResource with key '{Key}' already exists. Cannot clone.", newKey);
            return null;
        }

        AudioResource clone;
        if (original.OriginalBytes is not null && !string.IsNullOrEmpty(original.SourceExtension))
        {
            clone = LoadFromStream(
                newKey,
                new MemoryStream(original.OriginalBytes),
                original.SourceExtension,
                volume ?? original.Volume,
                pan ?? original.Pan,
                playbackSpeed ?? original.PlaybackSpeed);
        }
        else if (!string.IsNullOrWhiteSpace(original.SourceUri))
        {
            clone = LoadFromUri(
                newKey,
                original.SourceUri,
                volume ?? original.Volume,
                pan ?? original.Pan,
                playbackSpeed ?? original.PlaybackSpeed);
        }
        else
        {
            Engine.Logger.LogWarning("Cannot clone AudioResource '{Key}' because its source cannot be recreated.", key);
            return null;
        }

        clone.IsLooping = original.IsLooping;
        return clone;
    }

    private AudioResource LoadFromBytes(
        string key,
        byte[] bytes,
        string fileHint,
        float volume,
        float pan,
        float playbackSpeed)
    {
        var ext = Path.GetExtension(fileHint);
        if (string.IsNullOrWhiteSpace(ext))
        {
            throw new InvalidOperationException(
                $"Audio asset '{key}' has no file extension. Ensure audio AssetsFile entries retain their extension.");
        }

        ReplaceExisting(key);
        var speed = ClampPlaybackSpeed(playbackSpeed);
        var playback = RequireBackend().CreateFromBytes(
            key,
            bytes,
            fileHint,
            Math.Clamp(volume, 0f, 1f),
            Math.Clamp(pan, -1f, 1f),
            speed);

        var sound = new AudioResource(key, playback, volume, pan, speed, fileHint, bytes);
        RegisterLoadedSound(key, sound);
        return sound;
    }

    private void ReplaceExisting(string key)
    {
        if (_soundResources.TryRemove(key, out var existing))
            existing.Dispose();
    }

    private void RegisterLoadedSound(string key, AudioResource sound)
    {
        _soundResources[key] = sound;
        sound.Disposed += (_, _) =>
        {
            if (_soundResources.TryGetValue(key, out var current) && ReferenceEquals(current, sound))
            {
                _soundResources.TryRemove(key, out _);
                SoundDisposed?.Invoke(this, (key, sound));
            }
        };
    }

    private IAudioBackend RequireBackend() => _backend
        ?? throw new InvalidOperationException(
            "No Gondwana audio backend is configured. Install and initialize a backend such as Gondwana.Audio.NAudio or Gondwana.Audio.Browser before loading audio.");

    private static float ClampPlaybackSpeed(float speed) =>
        Math.Clamp(speed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);

    public void Unload(string key)
    {
        if (_soundResources.TryRemove(key, out var resource))
            resource.Dispose();
    }

    public void Clear()
    {
        foreach (var resource in _soundResources.Values.ToArray())
            resource.Dispose();

        _soundResources.Clear();
    }

    public bool TryGet(string key, out AudioResource? resource) => _soundResources.TryGetValue(key, out resource);

    public AudioResource? Get(string key) => _soundResources.TryGetValue(key, out var resource) ? resource : null;

    public bool Contains(string key) => _soundResources.ContainsKey(key);

    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

    public Dictionary<string, AudioResource> GetAll() => new(_soundResources);

    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }
}
