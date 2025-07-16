using System.Collections.Concurrent;
using Gondwana.Resource;
using Microsoft.Extensions.Logging;

namespace Gondwana.Audio;

public sealed class SoundResourceManager : IDisposable
{
    private static readonly Lazy<SoundResourceManager> _instance = new(() => new SoundResourceManager());
    private readonly ConcurrentDictionary<string, (SoundResource soundResource, string? tempPath)> _soundResources = new();
    private bool _disposed = false;

    /// <summary>
    /// Event that is raised when a sound resource is disposed.
    /// </summary>
    public event EventHandler<(string Key, SoundResource Resource)>? SoundDisposed;

    private SoundResourceManager() { }

    /// <summary>
    /// Singleton instance of the SoundResourceManager.
    /// </summary>
    public static SoundResourceManager Instance => _instance.Value;

    public SoundResource LoadFromFile(string key, string filePath, float volume = 1.0f, float pan = 0.0f)
    {
        if (_soundResources.TryGetValue(key, out var existing))
            return existing.Item1;

        var bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(key, bytes, filePath, volume, pan);
    }

    public SoundResource LoadFromStream(string key, Stream input, string fileExt, float volume = 1.0f, float pan = 0.0f)
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

    public List<SoundResource> LoadFromEngineResourceFile(EngineResourceFile resourceFile, float defaultVolume = 1.0f, float defaultPan = 0.0f)
    {
        List<SoundResource> loadedSounds = new();

        foreach (var entry in resourceFile.GetAllEntries())
        {
            if (entry.ResourceType != EngineResourceFileTypes.Audio)
                continue;

            if (_soundResources.ContainsKey(entry.ResourceName))
            {
                Engine.Logger.LogDebug("SoundResource '{Key}' already loaded. Skipping.", entry.ResourceName);
                continue;
            }

            var stream = resourceFile.Get(entry.ResourceType, entry.ResourceName);
            if (stream == null)
            {
                Engine.Logger.LogWarning("Failed to retrieve stream for audio resource: {Key}", entry.ResourceName);
                continue;
            }

            try
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                Engine.Logger.LogInformation("Loaded sound: {Key}", entry.ResourceName);
                loadedSounds.Add(LoadFromBytes(entry.ResourceName, bytes, entry.ResourceName, defaultVolume, defaultPan));
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error loading sound from resource file for key: {Key}", entry.ResourceName);
                throw;
            }
        }

        return loadedSounds;
    }

    public SoundResource? Clone(string key, string? newKey = null, float? volume = null, float? pan = null)
    {
        if (!_soundResources.TryGetValue(key, out var original))
        {
            Engine.Logger.LogWarning("Attempted to clone non-existent SoundResource with key: {Key}", key);
            return null;
        }

        newKey ??= $"{key}_clone_{Guid.NewGuid()}";

        if (_soundResources.ContainsKey(newKey))
        {
            Engine.Logger.LogWarning("SoundResource with key '{Key}' already exists. Cannot clone.", newKey);
            return null;
        }

        if (original.soundResource.OriginalBytes == null)
        {
            Engine.Logger.LogWarning("Cannot clone SoundResource '{Key}' – missing original bytes.", key);
            return null;
        }

        if (string.IsNullOrEmpty(original.soundResource.Extension))
        {
            Engine.Logger.LogWarning("Cannot clone SoundResource '{Key}' – missing original extension.", key);
            return null;
        }

        return LoadFromStream(
            newKey,
            new MemoryStream(original.soundResource.OriginalBytes),
            original.soundResource.Extension,
            volume ?? original.soundResource.Volume,
            pan ?? original.soundResource.Pan
        );
    }

    private SoundResource LoadFromBytes(string key, byte[] bytes, string fileHint, float volume, float pan)
    {
        var newStream = new MemoryStream(bytes);
        var (reader, fileRequired) = PlatformAudioFactory.CreateReader(newStream, fileHint);

        string? filePath = fileHint;
        string? tempFilePath = null;

        if (fileRequired)
        {
            tempFilePath = SaveStreamToTempFile(new MemoryStream(bytes), Path.GetExtension(fileHint));
        }

        var sound = new SoundResource(
            key,
            reader,
            volume,
            pan,
            filePath,
            bytes,
            tempFilePath
        );

        _soundResources[key] = (sound, fileRequired ? filePath : "");
        RegisterLoadedSound(key, sound);
        return sound;
    }

    private void RegisterLoadedSound(string key, SoundResource sound)
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
    /// <param name="key">Unique identifier for SoundResource.</param>
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

    public bool TryGet(string key, out SoundResource? resource)
    {
        if (_soundResources.TryGetValue(key, out var entry))
        {
            resource = entry.soundResource;
            return true;
        }

        resource = null;
        return false;
    }

    public SoundResource? Get(string key) => _soundResources.TryGetValue(key, out var entry) ? entry.soundResource : null;

    public bool Contains(string key) => _soundResources.ContainsKey(key);

    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

    public Dictionary<string, SoundResource> GetAll() =>
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
