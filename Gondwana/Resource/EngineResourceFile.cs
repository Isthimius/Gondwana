using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Gondwana.Resource;

public sealed class EngineResourceFile : IDisposable
{
    private ZipFile? _zipFile;
    private readonly Dictionary<EngineResourceFileEntry, Func<Stream>> _zipEntries = new();
    private bool _isLoaded = false;

    [JsonConstructor]
    private EngineResourceFile() { }

    public static EngineResourceFile LoadOrCreate(string path, string? password = null, bool encrypt = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be null or empty.", nameof(path));

        EngineResourceFile resourceFile = new()
        {
            FilePath = path,
            Password = password,
            IsEncrypted = encrypt
        };

        if (File.Exists(resourceFile.FilePath))
            resourceFile.LoadZip();

        return resourceFile;
    }

    [JsonInclude]
    public string FilePath { get; private set; } = string.Empty;

    [JsonInclude]
    public string? Password { get; private set; } = null;

    [JsonInclude]
    public bool IsEncrypted { get; private set; } = false;

    private void EnsureLoaded()
    {
        if (!_isLoaded)
            LoadZip();
    }

    private void LoadZip()
    {
        if (_isLoaded)
            return;

        Engine.Logger.LogDebug("Loading resource file: {FilePath}", FilePath);

        _zipFile?.Close();
        _zipFile = new ZipFile(File.OpenRead(FilePath));

        if (!string.IsNullOrEmpty(Password))
            _zipFile.Password = Password;

        _zipEntries.Clear();

        foreach (ZipEntry entry in _zipFile)
        {
            if (!entry.IsFile)
                continue;

            var key = EngineResourceFileEntry.FromString(entry.Name);
            if (key == null)
            {
                Engine.Logger.LogWarning("Invalid entry in resource file: {EntryName}", entry.Name);
                continue;
            }

            _zipEntries[key] = () =>
            {
                var zipEntry = _zipFile.GetEntry(key.ToString());
                return zipEntry != null ? _zipFile!.GetInputStream(zipEntry) : Stream.Null;
            };
        }

        _isLoaded = true;
    }

    public void Add(EngineResourceFileTypes type, string name, Func<Stream> streamFactory)
    {
        var key = new EngineResourceFileEntry
        {
            ResourceType = type,
            ResourceName = name
        };

        _zipEntries[key] = streamFactory;
    }

    public void Remove(EngineResourceFileTypes type, string name)
    {
        var key = new EngineResourceFileEntry
        {
            ResourceType = type,
            ResourceName = name
        };

        _zipEntries.Remove(key);
    }

    public void AddFromFile(EngineResourceFileTypes type, string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        Add(type, name, () => File.OpenRead(filePath));
    }

    public Stream? this[EngineResourceFileTypes type, string name] => Get(type, name);

    public Stream? Get(EngineResourceFileTypes type, string name)
    {
        EnsureLoaded();

        var key = new EngineResourceFileEntry
        {
            ResourceType = type,
            ResourceName = name
        };

        return _zipEntries.TryGetValue(key, out var getStream) ? getStream() : null;
    }

    public IEnumerable<EngineResourceFileEntry> GetAllEntries()
    {
        EnsureLoaded();
        return _zipEntries.Keys;
    }

    public void Save()
    {
        using var fs = File.Create(FilePath);
        using var zipStream = new ZipOutputStream(fs)
        {
            IsStreamOwner = true
        };

        if (!string.IsNullOrEmpty(Password))
        {
            zipStream.Password = Password;
            // zipStream.Encryption = EncryptionAlgorithm.WinZipAes256; // optional
        }

        foreach (var (key, getStream) in _zipEntries)
        {
            var entry = new ZipEntry(key.ToString())
            {
                DateTime = DateTime.Now
            };

            zipStream.PutNextEntry(entry);

            using var inputStream = getStream();
            inputStream.CopyTo(zipStream);
            zipStream.CloseEntry();
        }

        _zipEntries.Clear();
    }

    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;
    }
}
