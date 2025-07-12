using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Gondwana.Resource;

public sealed class EngineResourceFile : IDisposable
{
    private ZipFile? _zipFile;
    private readonly List<(string Key, Func<Stream> GetStream)> _zipEntries = new();
    private bool _isLoaded = false;

    [JsonConstructor]
    private EngineResourceFile() { }

    public static EngineResourceFile LoadOrCreate(string path, string? password = null, bool encrypt = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be null or empty.", nameof(path));

        EngineResourceFile resourceFile = new();

        resourceFile.FilePath = path;
        resourceFile.Password = password;
        resourceFile.IsEncrypted = encrypt;

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
        if (_isLoaded)
            return;

        LoadZip();
    }

    private void LoadZip()
    {
        if (_isLoaded)
            return;

        Engine.Logger.LogDebug("loading resource file: {FilePath}", FilePath);

        _zipFile?.Close(); // ensure any previous zip is closed
        _zipFile = new ZipFile(File.OpenRead(FilePath));

        if (!string.IsNullOrEmpty(Password))
            _zipFile.Password = Password;

        _zipEntries.Clear();

        foreach (ZipEntry entry in _zipFile)
        {
            if (!entry.IsFile)
                continue;

            var key = entry.Name;

            _zipEntries.Add((key, () =>
            {
                var zipEntry = _zipFile.GetEntry(key);
                return zipEntry != null ? _zipFile!.GetInputStream(zipEntry) : Stream.Null;
            }
            ));
        }

        _isLoaded = true;
    }

    public void Add(EngineResourceFileTypes type, string name, Func<Stream> streamFactory)
    {
        var key = $"{type}_{name.ToLower()}";
        _zipEntries.Add((key, streamFactory));
    }

    public void Remove(EngineResourceFileTypes type, string name)
    {
        var key = $"{type}_{name.ToLower()}";
        _zipEntries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
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

        var key = $"{type}_{name.ToLower()}";
        var match = _zipEntries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        return match != default ? match.GetStream() : null;
    }


    /// <summary>
    /// Saves the current state of the resource file to disk.
    /// This will overwrite the existing file.
    /// </summary>
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
            // zipStream.Encryption = EncryptionAlgorithm.WinZipAes256; // optional: needs testing
        }

        foreach (var (key, getStream) in _zipEntries)
        {
            var entry = new ZipEntry(key)
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
        if (_zipFile is not null)
        {
            _zipFile.Close();
            _zipFile = null;
        }

        _isLoaded = false;
    }
}
