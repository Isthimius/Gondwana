using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Gondwana.Resource;

/// <summary>
/// Represents a resource file used by the engine, providing functionality to load, manage, and save resources.
/// </summary>
/// <remarks>The <see cref="EngineResourceFile"/> class allows for the creation, loading, and management of
/// resource files used by the engine. It supports encryption, resource retrieval by type and name, and saving resources
/// to a zip file. Instances of this class are tracked globally and can be accessed via the <see
/// cref="AllResourceFiles"/> property.</remarks>
public sealed class EngineResourceFile : IDisposable
{
    private static List<EngineResourceFile> _allResourceFiles = new();

    /// <summary>
    /// Gets a read-only list of all instantiated <see cref="EngineResourceFile"/> instances."/>
    /// </summary>
    public static IReadOnlyList<EngineResourceFile> AllResourceFiles => _allResourceFiles.AsReadOnly();

    /// <summary>
    /// Releases all resources held by the application and clears the internal collection of resource files.
    /// </summary>
    /// <remarks>This method disposes of all resource files currently tracked by the application.  After
    /// calling this method, the internal collection of resource files will be empty. Ensure that no further operations
    /// are performed on the disposed resources.</remarks>
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

    /// <summary>
    /// Loads an existing resource file from the specified path or creates a new one if the file does not exist.
    /// </summary>
    /// <param name="path">The file path of the resource file to load or create. Cannot be null, empty, or whitespace.</param>
    /// <param name="password">An optional password used to secure the resource file. Can be null if no password is required.</param>
    /// <param name="encrypt">A value indicating whether the resource file should use encryption. Defaults to <see langword="false"/>.</param>
    /// <returns>An <see cref="EngineResourceFile"/> instance representing the loaded or newly created resource file.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null, empty, or consists only of whitespace.</exception>
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

    /// <summary>
    /// Gets the file path associated with the current instance.
    /// </summary>
    [JsonInclude]
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the password associated with the current instance.
    /// </summary>
    [JsonInclude]
    public string? Password { get; private set; } = null;

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the current operation.
    /// </summary>
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

    /// <summary>
    /// Adds a resource file entry to the collection with the specified type, name, and stream factory.
    /// </summary>
    /// <remarks>If an entry with the same type and name already exists, it will be replaced with the new
    /// stream factory.</remarks>
    /// <param name="type">The type of the resource file to add.</param>
    /// <param name="name">The name of the resource file to add. Cannot be null or empty.</param>
    /// <param name="streamFactory">A factory method that provides a <see cref="Stream"/> for the resource file.  The factory is invoked when the
    /// resource is accessed.</param>
    public void Add(EngineResourceFileTypes type, string name, Func<Stream> streamFactory)
    {
        var key = new EngineResourceFileEntry
        {
            ResourceType = type,
            ResourceName = name
        };

        _zipEntries[key] = streamFactory;
    }

    /// <summary>
    /// Adds a resource file to the engine with the specified type and file path.
    /// </summary>
    /// <remarks>This method associates the resource file with the specified type and prepares it for use by
    /// the engine. The file is identified by its name, derived from the file path without the extension.</remarks>
    /// <param name="type">The type of the resource file to add.</param>
    /// <param name="filePath">The full path to the resource file. Must not be null or empty.</param>
    public void Add(EngineResourceFileTypes type, string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        Add(type, name, () => File.OpenRead(filePath));
    }

    /// <summary>
    /// Removes the specified resource file entry from the collection.
    /// </summary>
    /// <remarks>This method removes the resource file entry identified by the specified type and name from
    /// the internal collection. If the entry does not exist, no action is taken.</remarks>
    /// <param name="type">The type of the resource file to remove.</param>
    /// <param name="name">The name of the resource file to remove. Cannot be null or empty.</param>
    public void Remove(EngineResourceFileTypes type, string name)
    {
        var key = new EngineResourceFileEntry
        {
            ResourceType = type,
            ResourceName = name
        };

        _zipEntries.Remove(key);
    }

    /// <summary>
    /// Gets the stream associated with the specified resource type and name.
    /// </summary>
    /// <remarks>Use this indexer to access resources by specifying their type and name.  If the resource does
    /// not exist, the indexer returns <see langword="null"/>.</remarks>
    /// <param name="type">The type of the resource file to retrieve.</param>
    /// <param name="name">The name of the resource within the specified type.</param>
    /// <returns></returns>
    public Stream? this[EngineResourceFileTypes type, string name] => Get(type, name);

    /// <summary>
    /// Retrieves a stream for the specified resource type and name.
    /// </summary>
    /// <remarks>The method returns a stream that allows access to the resource data. Ensure that the resource
    /// type and name provided match an existing entry. If no matching resource is found, the method returns <see
    /// langword="null"/>.</remarks>
    /// <param name="type">The type of the resource to retrieve.</param>
    /// <param name="name">The name of the resource to retrieve.</param>
    /// <returns>A <see cref="Stream"/> containing the resource data if the resource is found; otherwise, <see langword="null"/>.</returns>
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

    /// <summary>
    /// Retrieves all entries from the resource file.
    /// </summary>
    /// <remarks>This method ensures that the resource file is loaded before returning the entries.</remarks>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all entries in the resource file.</returns>
    public IEnumerable<EngineResourceFileEntry> GetAllEntries()
    {
        EnsureLoaded();
        return _zipEntries.Keys;
    }

    /// <summary>
    /// Saves the current set of entries to a zip file at the specified file path.
    /// </summary>
    /// <remarks>This method creates a zip archive containing all entries currently stored in the collection.
    /// If a password is provided, the zip file will be encrypted using AES-256 encryption.  The method clears the
    /// current entries after saving and reloads the zip file to ensure consistency.</remarks>
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
        _isLoaded = false;
        LoadZip();
    }

    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;
        _allResourceFiles.Remove(this);
    }
}
