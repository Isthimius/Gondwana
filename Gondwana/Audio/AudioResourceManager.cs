using System.Collections.Concurrent;
using Gondwana.Assets;
using Microsoft.Extensions.Logging;

namespace Gondwana.Audio;

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

    public AudioResource LoadFromFile(string key, string filePath, float volume = 1.0f, float pan = 0.0f)
    {
        if (_soundResources.TryGetValue(key, out var existing))
        {
            existing.soundResource.Dispose(); // replace existing
        }

        var bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(key, bytes, filePath, volume, pan);
    }

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

    public List<AudioResource> LoadFromEngineResourceFile(AssetsFile resourceFile, float defaultVolume = 1.0f, float defaultPan = 0.0f)
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

    public AudioResource? Get(string key) => _soundResources.TryGetValue(key, out var entry) ? entry.soundResource : null;

    public bool Contains(string key) => _soundResources.ContainsKey(key);

    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

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

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
    }
}