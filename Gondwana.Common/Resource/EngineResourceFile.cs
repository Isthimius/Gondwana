using System.Runtime.Serialization;
using ICSharpCode.SharpZipLib.Zip;

namespace Gondwana.Common.Resource;

[DataContract(IsReference = true)]
public class EngineResourceFile : IDisposable
{
    [DataMember]
    public string FilePath { get; private set; }

    [DataMember]
    public string Password { get; private set; }

    [DataMember]
    public bool IsEncrypted { get; private set; }

    private ZipFile _zipFile;
    private List<(string key, Func<Stream> getStream)> _pendingEntries = new();

    public EngineResourceFile(string path, string password = null, bool encrypt = false)
    {
        FilePath = path;
        Password = password;
        IsEncrypted = encrypt;

        if (File.Exists(FilePath))
            LoadZip();
    }

    private void LoadZip()
    {
        _zipFile = new ZipFile(File.OpenRead(FilePath));
        if (!string.IsNullOrEmpty(Password))
            _zipFile.Password = Password;
    }

    public void Add(EngineResourceFileTypes type, string name, Func<Stream> streamFactory)
    {
        var key = type + "_" + name;
        _pendingEntries.Add((key, streamFactory));
    }

    public void AddFromFile(EngineResourceFileTypes type, string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        Add(type, name, () => File.OpenRead(filePath));
    }

    public Stream this[EngineResourceFileTypes type, string name] => Get(type, name);

    public Stream? Get(EngineResourceFileTypes type, string name)
    {
        var key = type + "_" + name;
        if (_zipFile == null)
            LoadZip();

        var entry = _zipFile.GetEntry(key);
        if (entry == null)
            return null;

        return _zipFile.GetInputStream(entry);
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
            if (IsEncrypted)
                zipStream.Encryption = EncryptionAlgorithm.WinZipAes256;
        }

        foreach (var (key, getStream) in _pendingEntries)
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

        _pendingEntries.Clear();
    }

    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
    }
}
