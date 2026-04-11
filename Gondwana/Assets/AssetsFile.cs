using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.Assets;

/// <summary>
/// Represents a asset file used by the engine, providing functionality to load, manage, and save assets.
/// </summary>
/// <remarks>The <see cref="AssetsFile"/> class allows for the creation, loading, and management of
/// asset files used by the engine. It supports encryption, asset retrieval by type and name, and saving assets
/// to a zip file. Instances of this class are tracked globally and can be accessed via the static
/// <see cref="AllAssetsFiles"/> property.</remarks>
[JsonObject(IsReference = true)]
public sealed class AssetsFile : IDisposable
{
    private static List<AssetsFile> _allAssetsFiles = new();

    /// <summary>
    /// Gets a read-only list of all instantiated <see cref="AssetsFile"/> instances."/>
    /// </summary>
    public static IReadOnlyList<AssetsFile> AllAssetsFiles => _allAssetsFiles.AsReadOnly();

    /// <summary>
    /// Releases all assets held by the application and clears the internal collection of asset files.
    /// </summary>
    /// <remarks>This method disposes of all asset files currently tracked by the application.  After
    /// calling this method, the internal collection of asset files will be empty. Ensure that no further operations
    /// are performed on the disposed assets.</remarks>
    public static void ClearAll()
    {
        foreach (var assetFile in _allAssetsFiles.ToList())
        {
            assetFile.Dispose();
        }
    }

    private ZipFile? _zipFile;
    private readonly Dictionary<AssetsFileEntry, Func<Stream>> _zipEntries = new();
    private bool _isLoaded = false;

    [JsonConstructor]
    private AssetsFile()
    { _allAssetsFiles.Add(this); }

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
    public string? Password { get; private set; } = null;

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the current operation.
    /// </summary>
    [JsonProperty]
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
            Engine.Logger.LogInformation("Loading assets file: {FilePath}", FilePath);

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

                var key = AssetsFileEntry.FromString(entry.Name);
                if (key == null)
                {
                    Engine.Logger.LogWarning("Invalid entry in assets file: {EntryName}", entry.Name);
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
            Engine.Logger.LogError(ex, "Failed to load asset file.");
            throw;
        }
    }

    /// <summary>
    /// Adds a asset file entry to the collection with the specified type, name, and stream factory.
    /// </summary>
    /// <remarks>If an entry with the same type and name already exists, it will be replaced with the new
    /// stream factory.</remarks>
    /// <param name="type">The type of the asset file to add.</param>
    /// <param name="name">The name of the asset file to add. Cannot be null or empty.</param>
    /// <param name="streamFactory">A factory method that provides a <see cref="Stream"/> for the asset file.  The factory is invoked when the
    /// asset is accessed.</param>
    public void Add(AssetTypes type, string name, Func<Stream> streamFactory)
    {
        var key = new AssetsFileEntry
        {
            AssetType = type,
            AssetName = name
        };

        _zipEntries[key] = streamFactory;
    }

    /// <summary>
    /// Adds an asset file to the engine with the specified type and file path.
    /// </summary>
    /// <remarks>
    /// This method associates the asset file with the specified type and prepares it for use by the engine.
    /// The asset is stored using its full file name, including extension (e.g., "player.png").
    /// 
    /// <para>
    /// Retaining the file extension allows the engine to preserve format information (e.g., for decoding)
    /// and improves transparency when inspecting the underlying asset archive.
    /// </para>
    /// 
    /// <para>
    /// The asset can later be retrieved using either the full file name (e.g., "player.png") or the base
    /// name without extension (e.g., "player"). When using the base name, a fallback lookup will attempt to
    /// match any asset of the same type with a corresponding file name.
    /// </para>
    /// 
    /// <para>
    /// <b>Important:</b> If multiple assets of the same type share the same base name but differ by extension
    /// (e.g., "player.png" and "player.webp"), retrieving by base name is ambiguous and may return an
    /// unintended result. In such cases, the full file name should be used.
    /// </para>
    /// </remarks>
    /// <param name="type">The type of the asset file to add.</param>
    /// <param name="filePath">The full path to the asset file. Must not be null or empty.</param>
    public void Add(AssetTypes type, string filePath)
    {
        var name = Path.GetFileName(filePath);
        Add(type, name, () => File.OpenRead(filePath));
    }

    /// <summary>
    /// Adds an asset file entry to the collection with the specified type, name, and source stream.
    /// </summary>
    /// <remarks>
    /// The provided <paramref name="stream"/> is read immediately and its contents are buffered in memory.
    /// Subsequent access to the asset will return new read-only streams over the buffered data.
    /// 
    /// <para>
    /// This approach ensures that the original stream does not need to remain open after this method completes,
    /// and avoids issues related to stream lifetime or disposal.
    /// </para>
    /// 
    /// <para>
    /// <b>Important:</b> The entire contents of the stream are loaded into memory. For large assets, this may
    /// have a noticeable memory impact. In such cases, consider using the <see cref="Add(AssetTypes, string, Func{Stream})"/>
    /// overload instead to defer stream creation.
    /// </para>
    /// 
    /// <para>
    /// If an entry with the same type and name already exists, it will be replaced with the new data.
    /// </para>
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

        // Copy stream into memory buffer
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        Add(type, name, () => new MemoryStream(data, writable: false));
    }

    /// <summary>
    /// Removes the specified asset file entry from the collection.
    /// </summary>
    /// <remarks>This method removes the asset file entry identified by the specified type and name from
    /// the internal collection. If the entry does not exist, no action is taken.</remarks>
    /// <param name="type">The type of the asset file to remove.</param>
    /// <param name="name">The name of the asset file to remove. Cannot be null or empty.</param>
    public void Remove(AssetTypes type, string name)
    {
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
    /// <remarks>Use this indexer to access assets by specifying their type and name.  If the asset does
    /// not exist, the indexer returns <see langword="null"/>.</remarks>
    /// <param name="type">The type of the asset file to retrieve.</param>
    /// <param name="name">The name of the asset within the specified type.</param>
    /// <returns></returns>
    public Stream? this[AssetTypes type, string name] => Get(type, name);

    /// <summary>
    /// Retrieves a stream for the specified asset type and name.
    /// </summary>
    /// <remarks>
    /// This method attempts to resolve the asset in two steps:
    /// <list type="number">
    /// <item>
    /// Performs an exact match using the provided <paramref name="name"/> (e.g., "player.png").
    /// </item>
    /// <item>
    /// If no exact match is found, performs a fallback lookup by comparing the base file name
    /// (without extension), allowing queries such as "player" to match entries like "player.png".
    /// </item>
    /// </list>
    /// 
    /// <para>
    /// <b>Important:</b> If multiple assets of the same <paramref name="type"/> share the same base name
    /// but differ by extension (e.g., "player.png" and "player.webp"), the fallback behavior will return
    /// the first match encountered. This is non-deterministic and depends on internal collection ordering.
    /// </para>
    /// 
    /// <para>
    /// To avoid ambiguous results, it is recommended to:
    /// <list type="bullet">
    /// <item>Ensure unique base names per asset type, or</item>
    /// <item>Use the full file name (including extension) when retrieving assets.</item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// If no matching asset is found, the method returns <see langword="null"/>.
    /// </para>
    /// </remarks>
    /// <param name="type">The type of the asset to retrieve.</param>
    /// <param name="name">The name of the asset to retrieve. May include or omit the file extension.</param>
    /// <returns>
    /// A <see cref="Stream"/> containing the asset data if a match is found; otherwise, <see langword="null"/>.
    /// </returns>
    public Stream? Get(AssetTypes type, string name)
    {
        EnsureLoaded();

        // 1. Exact match first: "player.png"
        var exactKey = new AssetsFileEntry
        {
            AssetType = type,
            AssetName = name
        };

        if (_zipEntries.TryGetValue(exactKey, out var getExactStream))
            return getExactStream();

        // 2. Fallback: if caller asked for "player", try matching "player.*"
        var requestedBaseName = Path.GetFileNameWithoutExtension(name);

        var match = _zipEntries.Keys.FirstOrDefault(k =>
            k.AssetType == type &&
            string.Equals(
                Path.GetFileNameWithoutExtension(k.AssetName),
                requestedBaseName,
                StringComparison.OrdinalIgnoreCase));

        return match != null && _zipEntries.TryGetValue(match, out var getFallbackStream)
            ? getFallbackStream()
            : null;
    }

    /// <summary>
    /// Retrieves all entries from the asset file.
    /// </summary>
    /// <remarks>This method ensures that the asset file is loaded before returning the entries.</remarks>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all entries in the asset file.</returns>
    public IEnumerable<AssetsFileEntry> GetAllEntries()
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

        Engine.Logger.LogInformation("Assets file saved: {FilePath} (Encrypted: {Encrypted})", FilePath, UseEncryption);
        _zipEntries.Clear();
        _isLoaded = false;
        LoadZip();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AssetsFile"/> instance.
    /// </summary>
    /// <remarks>This method closes the underlying zip file, clears the loaded state, and removes this
    /// instance from the global collection of asset files. After calling this method, the instance should
    /// not be used further.</remarks>
    public void Dispose()
    {
        _zipFile?.Close();
        _zipFile = null;
        _isLoaded = false;
        _allAssetsFiles.Remove(this);
    }
}