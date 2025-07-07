using System.Collections.Concurrent;

namespace Gondwana.Audio;

public class SoundResourceManager : IDisposable
{
    private static readonly Lazy<SoundResourceManager> _instance = new(() => new SoundResourceManager());
    private readonly ConcurrentDictionary<string, SoundResource> _soundResources = new();
    private bool _disposed = false;

    public event EventHandler<(string Key, SoundResource Resource)>? SoundDisposed;

    private SoundResourceManager() { }

    public static SoundResourceManager Instance => _instance.Value;

    public SoundResource LoadFromFile(string key, string filePath, float volume = 1.0f, float pan = 0.0f, bool isTemp = false)
    {
        if (_soundResources.TryGetValue(key, out var existing))
            return existing;

        using var fileStream = File.OpenRead(filePath);
        using var ms = new MemoryStream();
        fileStream.CopyTo(ms);
        var bytes = ms.ToArray();

        var newStream = new MemoryStream(bytes);
        var (reader, fileRequired) = PlatformAudioFactory.CreateReader(newStream, filePath);

        var sound = new SoundResource(key, reader, volume, pan, filePath, isTemp, bytes, Path.GetExtension(filePath));
        _soundResources[key] = sound;

        RegisterLoadedSound(key, sound);
        return sound;
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

        var newStream = new MemoryStream(bytes);
        var (reader, fileRequired) = PlatformAudioFactory.CreateReader(newStream, fileExt);

        string? filePath = null;
        if (fileRequired)
        {
            filePath = SaveStreamToTempFile(new MemoryStream(bytes), fileExt);
        }

        var sound = new SoundResource(key, reader, volume, pan, filePath, fileRequired, bytes, fileExt);
        _soundResources[key] = sound;

        RegisterLoadedSound(key, sound);
        return sound;
    }

    private string SaveStreamToTempFile(Stream input, string extension)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        input.Position = 0; // ensure we're at the beginning
        using var fs = File.Create(tempPath);
        input.CopyTo(fs);
        return tempPath;
    }

    public SoundResource Clone(string key, string? newKey = null, float? volume = null, float? pan = null)
    {
        if (!_soundResources.TryGetValue(key, out var original))
            throw new KeyNotFoundException($"SoundResource with key '{key}' not found.");

        newKey ??= $"{key}_clone_{Guid.NewGuid()}";

        if (_soundResources.ContainsKey(newKey))
            throw new ArgumentException($"SoundResource with key '{newKey}' already exists.");

        if (original.OriginalBytes == null || string.IsNullOrEmpty(original.OriginalExtension))
            throw new InvalidOperationException($"SoundResource '{key}' cannot be cloned – missing raw data.");

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

    public void Unload(string key)
    {
        if (_soundResources.TryRemove(key, out var resource))
            resource.Dispose();
    }

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

    public IEnumerable<SoundResource> GetAll() => _soundResources.Values;

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
    }
}
