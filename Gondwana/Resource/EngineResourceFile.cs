using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Gondwana.Resource;

public sealed class EngineResourceFile : IDisposable
{
    private static List<EngineResourceFile> _allResourceFiles = new();

    /// <summary>
    /// Gets a read-only list of all instantiated <see cref="EngineResourceFile"/> instances."/>
    /// </summary>
    public static IReadOnlyList<EngineResourceFile> AllResourceFiles => _allResourceFiles.AsReadOnly();

    public static void ClearAll()
    {
        foreach (var resourceFile in _allResourceFiles.ToList())
        {
            resourceFile.Dispose();
        }
    }

    private ZipFile? _zipFile;
    private readonly Dictionary<EngineResourceFileEntry, Func<Stream>> _zipEntries = new();
    private bool _isLoaded = false;

    [JsonConstructor]
    private EngineResourceFile() { _allResourceFiles.Add(this); }

    public static EngineResourceFile LoadOrCreate(string path, string? password = null, bool encrypt = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be null or empty.", nameof(path));

        var resourceFile = new EngineResourceFile
        {
            FilePath = path,
            Password = password,
            UseEncryption = encrypt
        };

        if (File.Exists(path))
            resourceFile.LoadZip();

        return resourceFile;
    }

    [JsonInclude]
    public string FilePath { get; private set; } = string.Empty;

    [JsonInclude]
    public string? Password { get; private set; } = null;

    [JsonInclude]
    public bool UseEncryption { get; private set; } = false;

    private void EnsureLoaded()
    {
        if (!_isLoaded)
            LoadZip();
    }

    private void LoadZip()
    {
        if (_isLoaded)
            return;

        try
        {
            Engine.Logger.LogDebug("Loading resource file: {FilePath}", FilePath);

            _zipFile?.Close();
            _zipFile = new ZipFile(File.OpenRead(FilePath));

            if (!string.IsNullOrEmpty(Password))
                _zipFile.Password = Password;

            // Test decryption on first entry to validate password early
            var testEntry = _zipFile.Cast<ZipEntry>().FirstOrDefault(e => e.IsFile);
            if (testEntry != null)
            {
                using var testStream = _zipFile.GetInputStream(testEntry);
                testStream.ReadByte(); // force decryption
            }

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
        catch (ZipException zex)
        {
            Engine.Logger.LogError(zex, "Decryption failed — check password or file integrity.");
            throw;
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "Failed to load zip file.");
            throw;
        }
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

    public void Add(EngineResourceFileTypes type, string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        Add(type, name, () => File.OpenRead(filePath));
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
            zipStream.Password = Password;

        foreach (var (key, getStream) in _zipEntries)
        {
            var entry = new ZipEntry(key.ToString())
            {
                DateTime = DateTime.Now
            };

            if (UseEncryption && !string.IsNullOrEmpty(Password))
                entry.AESKeySize = 256;

            zipStream.PutNextEntry(entry);

            using var inputStream = getStream();
            inputStream.CopyTo(zipStream);
            zipStream.CloseEntry();
        }

        Engine.Logger.LogInformation("Resource file saved: {FilePath} (Encrypted: {Encrypted})", FilePath, UseEncryption);
        _zipEntries.Clear();
    }

    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;
        _allResourceFiles.Remove(this);
    }
}
