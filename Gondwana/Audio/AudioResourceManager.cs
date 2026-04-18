using System.Collections.Concurrent;
using Gondwana.Assets;
using Microsoft.Extensions.Logging;

namespace Gondwana.Audio;

/// <summary>
/// Manages the lifecycle of audio resources, providing loading, retrieval, cloning, and disposal functionality.
/// </summary>
/// <remarks>This class implements the singleton pattern to ensure a single instance manages all audio resources
/// throughout the application. It supports loading audio from files, streams, and asset files, and tracks all
/// loaded resources in a thread-safe manner.</remarks>
public sealed class AudioResourceManager : IDisposable
{
    private static readonly Lazy<AudioResourceManager> _instance = new(() => new AudioResourceManager());
    private readonly ConcurrentDictionary<string, (AudioResource soundResource, string? tempPath)> _soundResources = new();
    private bool _disposed = false;

    /// <summary>
    /// Event that is raised when a sound resource is disposed.
    /// </summary>
    public event EventHandler<(string Key, AudioResource Resource)>? SoundDisposed;

    private AudioResourceManager()
    { }

    /// <summary>
    /// Singleton instance of the AudioResourceManager.
    /// </summary>
    public static AudioResourceManager Instance => _instance.Value;

    /// <summary>
    /// Loads an audio resource from a file on disk.
    /// </summary>
    /// <remarks>If a resource with the same key already exists, it will be disposed and replaced with the new resource.
    /// The audio file format is determined by the file extension.</remarks>
    /// <param name="key">A unique identifier for the audio resource.</param>
    /// <param name="filePath">The path to the audio file on disk.</param>
    /// <param name="volume">The initial volume level for the audio resource, ranging from 0.0 (silent) to 1.0 (full volume). Defaults to 1.0.</param>
    /// <param name="pan">The initial stereo pan position, ranging from -1.0 (full left) to 1.0 (full right). Defaults to 0.0 (center).</param>
    /// <returns>The loaded <see cref="AudioResource"/> instance.</returns>
    public AudioResource LoadFromFile(string key, string filePath, float volume = 1.0f, float pan = 0.0f)
    {
        if (_soundResources.TryGetValue(key, out var existing))
        {
            existing.soundResource.Dispose(); // replace existing
        }

        var bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(key, bytes, filePath, volume, pan);
    }

    /// <summary>
    /// Loads an audio resource from a stream.
    /// </summary>
    /// <remarks>If a resource with the same key already exists, it will be disposed and replaced with the new resource.
    /// The audio format is determined by the specified file extension.</remarks>
    /// <param name="key">A unique identifier for the audio resource.</param>
    /// <param name="input">The stream containing the audio data.</param>
    /// <param name="fileExt">The file extension indicating the audio format (e.g., ".wav", ".mp3").</param>
    /// <param name="volume">The initial volume level for the audio resource, ranging from 0.0 (silent) to 1.0 (full volume). Defaults to 1.0.</param>
    /// <param name="pan">The initial stereo pan position, ranging from -1.0 (full left) to 1.0 (full right). Defaults to 0.0 (center).</param>
    /// <returns>The loaded <see cref="AudioResource"/> instance.</returns>
    public AudioResource LoadFromStream(string key, Stream input, string fileExt, float volume = 1.0f, float pan = 0.0f)
    {
        if (_soundResources.TryGetValue(key, out var existing))
        {
            existing.soundResource.Dispose(); // replace existing
        }

        using var ms = new MemoryStream();
        input.CopyTo(ms);
        var bytes = ms.ToArray();

        return LoadFromBytes(key, bytes, fileExt, volume, pan);
    }

    /// <summary>
    /// Loads all audio resources from an <see cref="AssetsFile"/>.
    /// </summary>
    /// <remarks>This method iterates through all audio entries in the asset file and loads them into the manager.
    /// Resources that are already loaded will be skipped. Failed loads are logged but do not prevent other resources
    /// from being loaded.</remarks>
    /// <param name="resourceFile">The <see cref="AssetsFile"/> containing audio resources.</param>
    /// <param name="defaultVolume">The default volume level for all loaded audio resources, ranging from 0.0 to 1.0. Defaults to 1.0.</param>
    /// <param name="defaultPan">The default stereo pan position for all loaded audio resources, ranging from -1.0 to 1.0. Defaults to 0.0.</param>
    /// <returns>A list of successfully loaded <see cref="AudioResource"/> instances.</returns>
    public List<AudioResource> LoadFromEngineAssetsFile(AssetsFile resourceFile, float defaultVolume = 1.0f, float defaultPan = 0.0f)
    {
        List<AudioResource> loadedSounds = new();

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
            if (stream == null)
            {
                Engine.Logger.LogWarning("Failed to retrieve stream for audio resource: {Key}", entry.AssetName);
                continue;
            }

            try
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                Engine.Logger.LogInformation("Loaded sound: {Key}", entry.AssetName);
                loadedSounds.Add(LoadFromBytes(entry.AssetName, bytes, entry.AssetName, defaultVolume, defaultPan));
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error loading sound from asset file for key: {Key}", entry.AssetName);
                throw;
            }
        }

        return loadedSounds;
    }

    /// <summary>
    /// Creates a copy of an existing audio resource with a new key and optionally different settings.
    /// </summary>
    /// <remarks>Cloning requires the original resource to have its raw byte data available. If the new key
    /// already exists or the original resource cannot be found, the method returns <see langword="null"/>.</remarks>
    /// <param name="key">The key of the existing audio resource to clone.</param>
    /// <param name="newKey">The key for the cloned resource. If <see langword="null"/>, a unique key will be generated automatically.</param>
    /// <param name="volume">The volume level for the cloned resource. If <see langword="null"/>, uses the original resource's volume.</param>
    /// <param name="pan">The stereo pan position for the cloned resource. If <see langword="null"/>, uses the original resource's pan.</param>
    /// <returns>The cloned <see cref="AudioResource"/> if successful; otherwise, <see langword="null"/>.</returns>
    public AudioResource? Clone(string key, string? newKey = null, float? volume = null, float? pan = null)
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

        if (original.soundResource.OriginalBytes == null)
        {
            Engine.Logger.LogWarning("Cannot clone AudioResource '{Key}' – missing original bytes.", key);
            return null;
        }

        if (string.IsNullOrEmpty(original.soundResource.SourceExtension))
        {
            Engine.Logger.LogWarning("Cannot clone AudioResource '{Key}' – missing original extension.", key);
            return null;
        }

        return LoadFromStream(
            newKey,
            new MemoryStream(original.soundResource.OriginalBytes),
            original.soundResource.SourceExtension,
            volume ?? original.soundResource.Volume,
            pan ?? original.soundResource.Pan
        );
    }

    private AudioResource LoadFromBytes(string key, byte[] bytes, string fileHint, float volume, float pan)
    {
        string ext = Path.GetExtension(fileHint);

        if (string.IsNullOrWhiteSpace(ext))
        {
            throw new InvalidOperationException(
                $"Audio asset '{key}' has no file extension. " +
                "Ensure audio AssetsFile entries retain their extension."
            );
        }

        var (readerFactory, requiresFile) = PlatformAudioFactory.GetReaderFactory(ext);

        Stream streamForReader;
        string? tempFilePath = null;

        if (requiresFile)
        {
            // TODO: how does this play with the WinForms implementation of PlatformAudioFactory?
            tempFilePath = SaveStreamToTempFile(new MemoryStream(bytes), ext);
            streamForReader = File.OpenRead(tempFilePath);
        }
        else
        {
            streamForReader = new MemoryStream(bytes);
        }

        var reader = readerFactory(streamForReader);

        var sound = new AudioResource(
            key,
            reader,
            volume,
            pan,
            fileHint,
            bytes,
            tempFilePath
        );

        _soundResources[key] = (sound, requiresFile ? tempFilePath : null);
        RegisterLoadedSound(key, sound);
        return sound;
    }

    private void RegisterLoadedSound(string key, AudioResource sound)
    {
        sound.Disposed += (_, _) =>
        {
            if (_soundResources.TryRemove(key, out var removed))
                SoundDisposed?.Invoke(this, (key, removed.soundResource));
        };
    }

    /// <summary>
    /// Unloads a sound resource by its key, disposing of it and removing it from the manager.
    /// </summary>
    /// <param name="key">Unique identifier for AudioResource.</param>
    public void Unload(string key)
    {
        if (_soundResources.TryRemove(key, out var resource))
            resource.soundResource.Dispose();
    }

    /// <summary>
    /// Clears all sound resources, disposing of each one.
    /// </summary>
    public void Clear()
    {
        foreach (var resource in _soundResources.Values)
            resource.soundResource.Dispose();

        _soundResources.Clear();
    }

    /// <summary>
    /// Attempts to retrieve an audio resource by its key.
    /// </summary>
    /// <param name="key">The unique identifier of the audio resource to retrieve.</param>
    /// <param name="resource">When this method returns, contains the <see cref="AudioResource"/> associated with the specified key,
    /// if the key is found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the audio resource was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string key, out AudioResource? resource)
    {
        if (_soundResources.TryGetValue(key, out var entry))
        {
            resource = entry.soundResource;
            return true;
        }

        resource = null;
        return false;
    }

    /// <summary>
    /// Retrieves an audio resource by its key.
    /// </summary>
    /// <param name="key">The unique identifier of the audio resource to retrieve.</param>
    /// <returns>The <see cref="AudioResource"/> associated with the specified key if found; otherwise, <see langword="null"/>.</returns>
    public AudioResource? Get(string key) => _soundResources.TryGetValue(key, out var entry) ? entry.soundResource : null;

    /// <summary>
    /// Determines whether the manager contains an audio resource with the specified key.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <returns><see langword="true"/> if the manager contains an audio resource with the specified key; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string key) => _soundResources.ContainsKey(key);

    /// <summary>
    /// Gets all keys of audio resources currently managed by this instance.
    /// </summary>
    /// <returns>An enumerable collection of all resource keys.</returns>
    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

    /// <summary>
    /// Gets all audio resources currently managed by this instance as a dictionary.
    /// </summary>
    /// <returns>A dictionary containing all audio resources, keyed by their unique identifiers.</returns>
    public Dictionary<string, AudioResource> GetAll() =>
    _soundResources.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.soundResource
    );

    private static string SaveStreamToTempFile(Stream input, string extension)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        input.Position = 0; // ensure we're at the beginning
        using var fs = File.Create(tempPath);
        input.CopyTo(fs);
        return tempPath;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AudioResourceManager"/>.
    /// </summary>
    /// <remarks>This method clears all managed audio resources, disposing of each one. After disposal,
    /// the manager should not be used further.</remarks>
    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
    }
}