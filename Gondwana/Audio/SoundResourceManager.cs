using System.Collections.Concurrent;
using Gondwana.Resource;
using Microsoft.Extensions.Logging;

namespace Gondwana.Audio;

public class SoundResourceManager : IDisposable
{
    private static readonly Lazy<SoundResourceManager> _instance = new(() => new SoundResourceManager());
    private readonly ConcurrentDictionary<string, SoundResource> _soundResources = new();
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

    public SoundResource LoadFromFile(string key, string filePath, float volume = 1.0f, float pan = 0.0f, bool isTemp = false)
    {
        if (_soundResources.TryGetValue(key, out var existing))
            return existing;

        var bytes = File.ReadAllBytes(filePath);
        return LoadFromBytes(key, bytes, filePath, volume, pan);
    }

    public SoundResource LoadFromStream(string key, Stream input, string fileExt, float volume = 1.0f, float pan = 0.0f)
    {
        if (_soundResources.TryGetValue(key, out var existing))
        {
            existing.Dispose(); // replace existing
        }

        using var ms = new MemoryStream();
        input.CopyTo(ms);
        var bytes = ms.ToArray();

        return LoadFromBytes(key, bytes, fileExt, volume, pan);
    }

    public void LoadFromEngineResourceFile(EngineResourceFile resourceFile, float defaultVolume = 1.0f, float defaultPan = 0.0f)
    {
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
                LoadFromBytes(entry.ResourceName, bytes, entry.ResourceName, defaultVolume, defaultPan);
                Engine.Logger.LogInformation("Loaded sound: {Key}", entry.ResourceName);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error loading sound from resource file for key: {Key}", entry.ResourceName);
                throw;
            }
        }
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

        if (original.OriginalBytes == null || string.IsNullOrEmpty(original.OriginalExtension))
        {
            Engine.Logger.LogWarning("Cannot clone SoundResource '{Key}' – missing original bytes or extension.", key);
            return null;
        }

        return LoadFromStream(
            newKey,
            new MemoryStream(original.OriginalBytes),
            original.OriginalExtension,
            volume ?? original.Volume,
            pan ?? original.Pan
        );
    }

    private void RegisterLoadedSound(string key, SoundResource sound)
    {
        sound.Disposed += (_, _) =>
        {
            if (_soundResources.TryRemove(key, out var removed))
                SoundDisposed?.Invoke(this, (key, removed));
        };
    }

    /// <summary>
    /// Unloads a sound resource by its key, disposing of it and removing it from the manager.
    /// </summary>
    /// <param name="key">Unique identifier for SoundResource.</param>
    public void Unload(string key)
    {
        if (_soundResources.TryRemove(key, out var resource))
            resource.Dispose();
    }

    /// <summary>
    /// Clears all sound resources, disposing of each one.
    /// </summary>
    public void Clear()
    {
        foreach (var resource in _soundResources.Values)
            resource.Dispose();

        _soundResources.Clear();
    }

    public bool TryGet(string key, out SoundResource? resource) => _soundResources.TryGetValue(key, out resource);

    public SoundResource? Get(string key) => _soundResources.TryGetValue(key, out var res) ? res : null;

    public bool Contains(string key) => _soundResources.ContainsKey(key);

    public IEnumerable<string> GetAllKeys() => _soundResources.Keys;

    public Dictionary<string, SoundResource> GetAll() => _soundResources.ToDictionary();

    private static string SaveStreamToTempFile(Stream input, string extension)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        input.Position = 0; // ensure we're at the beginning
        using var fs = File.Create(tempPath);
        input.CopyTo(fs);
        return tempPath;
    }

    private SoundResource LoadFromBytes(string key, byte[] bytes, string fileHint, float volume, float pan)
    {
        var newStream = new MemoryStream(bytes);
        var (reader, fileRequired) = PlatformAudioFactory.CreateReader(newStream, fileHint);

        string? filePath = null;
        if (fileRequired)
        {
            filePath = SaveStreamToTempFile(new MemoryStream(bytes), Path.GetExtension(fileHint));
        }

        var sound = new SoundResource(
            key,
            reader,
            volume,
            pan,
            filePath,
            fileRequired,
            bytes,
            Path.GetExtension(fileHint)
        );

        _soundResources[key] = sound;
        RegisterLoadedSound(key, sound);
        return sound;
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
