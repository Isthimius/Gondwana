using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.Assets;

/// <summary>
/// Represents an asset file used by the engine, providing functionality to load, manage, and save assets.
/// </summary>
/// <remarks>
/// The <see cref="AssetsFile"/> class allows for the creation, loading, and management of
/// asset files used by the engine. It supports encryption, asset retrieval by type and name, and saving assets
/// to a zip file. Instances of this class are tracked globally and can be accessed via the static
/// <see cref="AllAssetsFiles"/> property.
/// </remarks>
[JsonObject(IsReference = true)]
public sealed class AssetsFile : IDisposable
{
    private static readonly List<AssetsFile> _allAssetsFiles = new();

    /// <summary>
    /// Gets a read-only list of all instantiated <see cref="AssetsFile"/> instances.
    /// </summary>
    public static IReadOnlyList<AssetsFile> AllAssetsFiles => _allAssetsFiles.AsReadOnly();

    /// <summary>
    /// Releases all assets held by the application and clears the internal collection of asset files.
    /// </summary>
    public static void ClearAll()
    {
        foreach (var assetFile in _allAssetsFiles.ToList())
            assetFile.Dispose();
    }

    private ZipFile? _zipFile;

    // In-memory source of truth.
    // Each entry stores its complete bytes independently of any live ZipFile handle.
    private readonly Dictionary<AssetsFileEntry, byte[]> _zipEntries = new();

    private bool _isLoaded;

    [JsonConstructor]
    private AssetsFile()
    {
        _allAssetsFiles.Add(this);
    }

    /// <summary>
    /// Loads an existing asset file from the specified path or creates a new one if the file does not exist.
    /// </summary>
    /// <param name="path">The file path of the asset file to load or create. Cannot be null, empty, or whitespace.</param>
    /// <param name="password">An optional password used to secure the asset file. Can be null if no password is required.</param>
    /// <param name="encrypt">A value indicating whether the asset file should use encryption. Defaults to <see langword="false"/>.</param>
    /// <returns>An <see cref="AssetsFile"/> instance representing the loaded or newly created asset file.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null, empty, or consists only of whitespace.</exception>
    public static AssetsFile LoadOrCreate(string path, string? password = null, bool encrypt = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path cannot be null or empty.", nameof(path));

        var assetFile = new AssetsFile
        {
            FilePath = path,
            Password = password,
            UseEncryption = encrypt
        };

        if (File.Exists(path))
            assetFile.LoadZip();
        else
            assetFile._isLoaded = true;

        return assetFile;
    }

    /// <summary>
    /// Gets the file path associated with the current instance.
    /// </summary>
    [JsonProperty]
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the password associated with the current instance.
    /// </summary>
    [JsonProperty]
    public string? Password { get; private set; }

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the current operation.
    /// </summary>
    [JsonProperty]
    public bool UseEncryption { get; private set; }

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
            Engine.Logger.LogInformation("Loading assets file: {FilePath}", FilePath);

            _zipFile?.Close();
            _zipFile = null;
            _zipEntries.Clear();

            if (!File.Exists(FilePath))
            {
                _isLoaded = true;
                return;
            }

            _zipFile = new ZipFile(File.OpenRead(FilePath));

            if (!string.IsNullOrEmpty(Password))
                _zipFile.Password = Password;

            // Force decryption on the first entry so a bad password fails early.
            var testEntry = _zipFile.Cast<ZipEntry>().FirstOrDefault(e => e.IsFile);
            if (testEntry != null)
            {
                using var testStream = _zipFile.GetInputStream(testEntry);
                testStream.ReadByte();
            }

            foreach (ZipEntry entry in _zipFile)
            {
                if (!entry.IsFile)
                    continue;

                var key = AssetsFileEntry.FromString(entry.Name);
                if (key == null)
                {
                    Engine.Logger.LogWarning("Invalid entry in assets file: {EntryName}", entry.Name);
                    continue;
                }

                using var entryStream = _zipFile.GetInputStream(entry);
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);

                _zipEntries[key] = ms.ToArray();
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
            Engine.Logger.LogError(ex, "Failed to load asset file.");
            throw;
        }
        finally
        {
            // Once contents are buffered, no need to keep the file handle open.
            _zipFile?.Close();
            _zipFile = null;
        }
    }

    /// <summary>
    /// Adds an asset file entry to the collection with the specified type, name, and stream factory.
    /// </summary>
    /// <remarks>
    /// If an entry with the same type and name already exists, it will be replaced with the new stream data.
    /// The provided factory is invoked immediately and its contents are buffered in memory.
    /// </remarks>
    /// <param name="type">The type of the asset file to add.</param>
    /// <param name="name">The name of the asset file to add. Cannot be null or empty.</param>
    /// <param name="streamFactory">A factory method that provides a <see cref="Stream"/> for the asset file.</param>
    public void Add(AssetTypes type, string name, Func<Stream> streamFactory)
    {
        if (streamFactory == null)
            throw new ArgumentNullException(nameof(streamFactory));

        EnsureLoaded();

        using var stream = streamFactory();
        Add(type, name, stream);
    }

    /// <summary>
    /// Adds an asset file to the engine with the specified type and file path.
    /// </summary>
    /// <remarks>
    /// The asset is stored using its full file name, including extension, unless an explicit name is provided.
    /// </remarks>
    /// <param name="type">The type of the asset file to add.</param>
    /// <param name="filePath">The full path to the asset file. Must not be null or empty.</param>
    /// <param name="name">The name to use for the asset file. If null, the file name will be used.</param>
    public void Add(AssetTypes type, string filePath, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var nameToUse = name ?? Path.GetFileName(filePath);
        using var stream = File.OpenRead(filePath);
        Add(type, nameToUse, stream);
    }

    /// <summary>
    /// Adds an asset file entry to the collection with the specified type, name, and source stream.
    /// </summary>
    /// <remarks>
    /// The provided <paramref name="stream"/> is read immediately and buffered in memory.
    /// Subsequent access returns a new read-only stream over the buffered bytes.
    /// </remarks>
    /// <param name="type">The type of the asset file to add.</param>
    /// <param name="name">The name of the asset file to add. Cannot be null or empty.</param>
    /// <param name="stream">The source stream containing the asset data. Must be readable.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    public void Add(AssetTypes type, string name, Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Asset name cannot be null or empty.", nameof(name));

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        EnsureLoaded();

        var key = new AssetsFileEntry
        {
            AssetType = type,
            AssetName = name
        };

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _zipEntries[key] = ms.ToArray();
    }

    /// <summary>
    /// Removes the specified asset file entry from the collection.
    /// </summary>
    /// <param name="type">The type of the asset file to remove.</param>
    /// <param name="name">The name of the asset file to remove. Cannot be null or empty.</param>
    public void Remove(AssetTypes type, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Asset name cannot be null or empty.", nameof(name));

        EnsureLoaded();

        var key = new AssetsFileEntry
        {
            AssetType = type,
            AssetName = name
        };

        _zipEntries.Remove(key);
    }

    /// <summary>
    /// Gets the stream associated with the specified asset type and name.
    /// </summary>
    /// <param name="type">The type of the asset file to retrieve.</param>
    /// <param name="name">The name of the asset within the specified type.</param>
    /// <returns>A readable stream if found; otherwise, <see langword="null"/>.</returns>
    public Stream? this[AssetTypes type, string name] => Get(type, name);

    /// <summary>
    /// Retrieves a stream for the specified asset type and name.
    /// </summary>
    /// <remarks>
    /// Attempts exact match first, then falls back to base-name matching without extension.
    /// </remarks>
    /// <param name="type">The type of the asset to retrieve.</param>
    /// <param name="name">The name of the asset to retrieve. May include or omit the file extension.</param>
    /// <returns>
    /// A <see cref="Stream"/> containing the asset data if a match is found; otherwise, <see langword="null"/>.
    /// </returns>
    public Stream? Get(AssetTypes type, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        EnsureLoaded();

        var exactKey = new AssetsFileEntry
        {
            AssetType = type,
            AssetName = name
        };

        if (_zipEntries.TryGetValue(exactKey, out var exactData))
            return new MemoryStream(exactData, writable: false);

        var requestedBaseName = Path.GetFileNameWithoutExtension(name);

        var match = _zipEntries.Keys.FirstOrDefault(k =>
            k.AssetType == type &&
            string.Equals(
                Path.GetFileNameWithoutExtension(k.AssetName),
                requestedBaseName,
                StringComparison.OrdinalIgnoreCase));

        return match != null && _zipEntries.TryGetValue(match, out var fallbackData)
            ? new MemoryStream(fallbackData, writable: false)
            : null;
    }

    /// <summary>
    /// Retrieves all entries from the asset file.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all entries in the asset file.</returns>
    public IEnumerable<AssetsFileEntry> GetAllEntries()
    {
        EnsureLoaded();
        return _zipEntries.Keys.ToList();
    }

    /// <summary>
    /// Saves the current set of entries to a zip file at the specified file path.
    /// </summary>
    public void Save()
    {
        EnsureLoaded();

        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;

        using (var fs = File.Create(FilePath))
        using (var zipStream = new ZipOutputStream(fs) { IsStreamOwner = true })
        {
            if (!string.IsNullOrEmpty(Password))
                zipStream.Password = Password;

            foreach (var (key, data) in _zipEntries)
            {
                var entry = new ZipEntry(key.ToString())
                {
                    DateTime = DateTime.Now
                };

                if (UseEncryption && !string.IsNullOrEmpty(Password))
                    entry.AESKeySize = 256;

                zipStream.PutNextEntry(entry);

                using var inputStream = new MemoryStream(data, writable: false);
                inputStream.CopyTo(zipStream);
                zipStream.CloseEntry();
            }

            zipStream.Finish();
        }

        Engine.Logger.LogInformation(
            "Assets file saved: {FilePath} (Encrypted: {Encrypted})",
            FilePath,
            UseEncryption);

        // Keep the in-memory copy as the source of truth.
        _isLoaded = true;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AssetsFile"/> instance.
    /// </summary>
    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;
        _zipEntries.Clear();
        _allAssetsFiles.Remove(this);
    }
}