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

    /// <summary>
    /// Occurs when a loaded <see cref="AudioResource"/> is disposed and removed from the manager.
    /// The event payload contains the resource key and the <see cref="AudioResource"/> instance.
    /// </summary>
    public event EventHandler<(string Key, AudioResource Resource)>? SoundDisposed;

    private AudioResourceManager() { }

    /// <summary>
    /// Gets the singleton <see cref="AudioResourceManager"/> instance.
    /// </summary>
    public static AudioResourceManager Instance => _instance.Value;

    /// <summary>Gets the currently configured audio backend, or null if none has been configured.</summary>
    public IAudioBackend? Backend => _backend;

    /// <summary>Gets whether an audio backend has been configured.</summary>
    public bool IsBackendConfigured => _backend is not null;

    /// <summary>
    /// Configures the backend used for subsequently loaded audio resources.
    /// A backend cannot be replaced while resources are loaded.
    /// </summary>
    /// <param name="backend">The <see cref="IAudioBackend"/> implementation to use for audio playback.</param>
    /// <exception cref="InvalidOperationException">Thrown if attempting to replace the configured backend while audio resources are still loaded.</exception>
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

    /// <summary>
    /// Compatibility overload that loads an audio resource from a file path.
    /// Retains the pre-backend-refactor CLR signature for compiled callers.
    /// </summary>
    /// <param name="key">Unique key for the audio resource.</param>
    /// <param name="filePath">Path to the audio file to load.</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <returns>The loaded <see cref="AudioResource"/>.</returns>
    public AudioResource LoadFromFile(string key, string filePath, float volume, float pan)
        => LoadFromFile(key, filePath, volume, pan, 1.0f);

    /// <summary>
    /// Loads an audio resource from a file on disk.
    /// </summary>
    /// <param name="key">Unique key for the audio resource.</param>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <param name="playbackSpeed">Initial playback speed (clamped to allowed range).</param>
    /// <returns>The loaded <see cref="AudioResource"/>.</returns>
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

    /// <summary>
    /// Compatibility overload that loads an audio resource from a stream.
    /// Retains the pre-backend-refactor CLR signature for compiled callers.
    /// </summary>
    /// <param name="key">Unique key for the audio resource.</param>
    /// <param name="input">Input <see cref="Stream"/> containing audio data.</param>
    /// <param name="fileExt">File extension or hint for the audio data (e.g. ".wav").</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <returns>The loaded <see cref="AudioResource"/>.</returns>
    public AudioResource LoadFromStream(string key, Stream input, string fileExt, float volume, float pan)
        => LoadFromStream(key, input, fileExt, volume, pan, 1.0f);

    /// <summary>
    /// Loads an audio resource from the provided <see cref="Stream"/>.
    /// </summary>
    /// <param name="key">Unique key for the audio resource.</param>
    /// <param name="input">Input <see cref="Stream"/> containing audio data.</param>
    /// <param name="fileExt">File extension or hint for the audio data (e.g. ".ogg").</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <param name="playbackSpeed">Initial playback speed (clamped to allowed range).</param>
    /// <returns>The loaded <see cref="AudioResource"/>.</returns>
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
    /// <param name="key">Unique key for the audio resource.</param>
    /// <param name="uri">The URI referencing the audio resource.</param>
    /// <param name="volume">Initial volume (0.0 to 1.0).</param>
    /// <param name="pan">Initial stereo pan (-1.0 to 1.0).</param>
    /// <param name="playbackSpeed">Initial playback speed (clamped to allowed range).</param>
    /// <returns>The created <see cref="AudioResource"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is null or whitespace.</exception>
    public AudioResource LoadFromUri(
        string key,
        string uri,
        float volume = 1.0f,
        float pan = 0.0f,
        float playbackSpeed = 1.0f)
    {
        lock (_backendLock)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentException("Audio URI cannot be empty.", nameof(uri));

            ValidateSettings(key, volume, pan, playbackSpeed);
            var speed = ClampPlaybackSpeed(playbackSpeed);
            var playback = RequireBackend().CreateFromUri(key, uri, Math.Clamp(volume, 0f, 1f), Math.Clamp(pan, -1f, 1f), speed);
            AudioResource? resource = null;
            try
            {
                resource = new AudioResource(key, playback, volume, pan, speed);
                resource.SetSourceUri(uri);
                RegisterLoadedSound(key, resource);
                return resource;
            }
            catch
            {
                DisposeFailedLoad(resource, playback);
                throw;
            }
        }
    }

    /// <summary>
    /// Compatibility overload that loads audio resources from an <see cref="AssetsFile"/>.
    /// Retains the pre-backend-refactor CLR signature for compiled callers.
    /// </summary>
    /// <param name="resourceFile">The <see cref="AssetsFile"/> containing audio entries.</param>
    /// <param name="defaultVolume">Default volume to apply to loaded resources.</param>
    /// <param name="defaultPan">Default pan to apply to loaded resources.</param>
    /// <returns>A list of loaded <see cref="AudioResource"/> instances.</returns>
    public List<AudioResource> LoadFromEngineAssetsFile(AssetsFile resourceFile, float defaultVolume, float defaultPan)
        => LoadFromEngineAssetsFile(resourceFile, defaultVolume, defaultPan, 1.0f);

    /// <summary>
    /// Loads all audio entries from the provided <see cref="AssetsFile"/> and returns the loaded resources.
    /// </summary>
    /// <param name="resourceFile">The <see cref="AssetsFile"/> to read audio entries from.</param>
    /// <param name="defaultVolume">Default volume to apply when an entry does not specify one.</param>
    /// <param name="defaultPan">Default pan to apply when an entry does not specify one.</param>
    /// <param name="defaultPlaybackSpeed">Default playback speed for loaded resources.</param>
    /// <returns>A list of loaded <see cref="AudioResource"/> instances.</returns>
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

            lock (_backendLock)
            {
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
                        var sound = LoadFromBytes(
                            entry.AssetName,
                            ms.ToArray(),
                            entry.AssetName,
                            defaultVolume,
                            defaultPan,
                            defaultPlaybackSpeed);
                        sound.SetAssetIdentifier(new AssetsFileIdentifier(resourceFile, AssetTypes.Audio, entry.AssetName));
                        loadedSounds.Add(sound);
                    }

                    Engine.Logger.LogInformation("Loaded sound: {Key}", entry.AssetName);
                }
                catch (Exception ex)
                {
                    Engine.Logger.LogError(ex, "Error loading sound from asset file for key: {Key}", entry.AssetName);
                    throw;
                }
            }
        }

        return loadedSounds;
    }

    /// <summary>
    /// Compatibility overload that creates a clone of an existing audio resource.
    /// Retains the pre-backend-refactor CLR signature; the clone inherits its source's playback speed.
    /// </summary>
    /// <param name="key">Key of the source resource to clone.</param>
    /// <param name="newKey">Optional key for the clone. If null, a generated key is used.</param>
    /// <param name="volume">Optional volume override for the clone.</param>
    /// <param name="pan">Optional pan override for the clone.</param>
    /// <returns>The cloned <see cref="AudioResource"/> or null if the source was not found or could not be cloned.</returns>
    public AudioResource? Clone(string key, string? newKey, float? volume, float? pan)
        => Clone(key, newKey, volume, pan, null);

    /// <summary>
    /// Creates a clone of an existing <see cref="AudioResource"/>. The clone may inherit or override
    /// volume, pan, and playback speed from the source resource.
    /// </summary>
    /// <param name="key">Key of the resource to clone.</param>
    /// <param name="newKey">Optional new key for the clone. If null a generated key is used.</param>
    /// <param name="volume">Optional volume override for the clone.</param>
    /// <param name="pan">Optional pan override for the clone.</param>
    /// <param name="playbackSpeed">Optional playback speed override for the clone.</param>
    /// <returns>The cloned <see cref="AudioResource"/> or null if cloning was not possible.</returns>
    public AudioResource? Clone(
        string key,
        string? newKey = null,
        float? volume = null,
        float? pan = null,
        float? playbackSpeed = null)
    {
        lock (_backendLock)
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
                using var stream = new MemoryStream(original.OriginalBytes, writable: false);
                clone = LoadFromStream(
                    newKey,
                    stream,
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

            clone.CopySourceFrom(original);
            clone.IsLooping = original.IsLooping;
            return clone;
        }
    }

    private AudioResource LoadFromBytes(
        string key,
        byte[] bytes,
        string fileHint,
        float volume,
        float pan,
        float playbackSpeed)
    {
        lock (_backendLock)
        {
            var ext = Path.GetExtension(fileHint);
            if (string.IsNullOrWhiteSpace(ext))
            {
                throw new InvalidOperationException(
                    $"Audio asset '{key}' has no file extension. Ensure audio AssetsFile entries retain their extension.");
            }

            ValidateSettings(key, volume, pan, playbackSpeed);
            var speed = ClampPlaybackSpeed(playbackSpeed);
            var playback = RequireBackend().CreateFromBytes(
                key,
                bytes,
                fileHint,
                Math.Clamp(volume, 0f, 1f),
                Math.Clamp(pan, -1f, 1f),
                speed);

            AudioResource? sound = null;
            try
            {
                sound = new AudioResource(key, playback, volume, pan, speed, fileHint, bytes);
                RegisterLoadedSound(key, sound);
                return sound;
            }
            catch
            {
                DisposeFailedLoad(sound, playback);
                throw;
            }
        }
    }

    private static void DisposeFailedLoad(AudioResource? resource, IAudioPlaybackHandle playback)
    {
        try
        {
            if (resource is not null)
                resource.Dispose();
            else
                playback.Dispose();
        }
        catch (Exception ex)
        {
            // Preserve the original load failure even if a backend also fails to dispose.
            Engine.Logger.LogError(ex, "Failed to release an audio handle after loading failed.");
        }
    }

    private void ReplaceExisting(string key)
    {
        if (_soundResources.TryGetValue(key, out var existing))
            existing.Dispose();
    }

    private static void ValidateSettings(string key, float volume, float pan, float speed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (float.IsNaN(volume) || float.IsNaN(pan) || float.IsNaN(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), "Audio settings must not be NaN.");
    }

    private void RegisterLoadedSound(string key, AudioResource sound)
    {
        ReplaceExisting(key);
        _soundResources[key] = sound;
        sound.Disposed += (_, _) =>
        {
            if (((ICollection<KeyValuePair<string, AudioResource>>)_soundResources).Remove(new(key, sound)))
            {
                SoundDisposed?.Invoke(this, (key, sound));
            }
        };
    }

    private IAudioBackend RequireBackend() => _backend
        ?? throw new InvalidOperationException(
            "No Gondwana audio backend is configured. Install and initialize a backend such as Gondwana.Audio.NAudio or Gondwana.Audio.Browser before loading audio.");

    private static float ClampPlaybackSpeed(float speed) =>
        Math.Clamp(speed, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed);

    /// <summary>
    /// Unloads and disposes the audio resource with the specified key, if it exists.
    /// </summary>
    /// <param name="key">Key of the audio resource to unload.</param>
    public void Unload(string key)
    {
        lock (_backendLock)
        {
            if (_soundResources.TryGetValue(key, out var resource))
                resource.Dispose();
        }
    }

    /// <summary>
    /// Disposes and clears all loaded audio resources.
    /// </summary>
    public void Clear()
    {
        lock (_backendLock)
        {
            foreach (var resource in _soundResources.Values.ToArray())
                resource.Dispose();

            _soundResources.Clear();
        }
    }

    /// <summary>
    /// Attempts to retrieve a loaded audio resource by key.
    /// </summary>
    /// <param name="key">Key of the audio resource to retrieve.</param>
    /// <param name="resource">When this method returns, contains the <see cref="AudioResource"/> associated with the key, if found; otherwise null.</param>
    /// <returns>True if the resource was found; otherwise false.</returns>
    public bool TryGet(string key, out AudioResource? resource) => _soundResources.TryGetValue(key, out resource);

    /// <summary>
    /// Gets the <see cref="AudioResource"/> with the specified key, or null if not found.
    /// </summary>
    /// <param name="key">Key of the audio resource to retrieve.</param>
    /// <returns>The <see cref="AudioResource"/> if found; otherwise null.</returns>
    public AudioResource? Get(string key) => _soundResources.TryGetValue(key, out var resource) ? resource : null;

    /// <summary>
    /// Determines whether an audio resource with the specified key is loaded.
    /// </summary>
    /// <param name="key">Key to check for existence.</param>
    /// <returns>True if the key exists; otherwise false.</returns>
    public bool Contains(string key) => _soundResources.ContainsKey(key);

    /// <summary>
    /// Returns all loaded audio resource keys.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{String}"/> of all resource keys.</returns>
    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

    /// <summary>
    /// Returns a snapshot dictionary of all loaded audio resources.
    /// </summary>
    /// <returns>A <see cref="Dictionary{String,AudioResource}"/> containing the loaded resources.</returns>
    public Dictionary<string, AudioResource> GetAll() => new(_soundResources);

    /// <summary>
    /// Disposes the manager and all loaded audio resources. This method is idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }
}
