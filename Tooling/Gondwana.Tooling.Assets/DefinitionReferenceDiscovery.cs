using Gondwana.Assets;
using ICSharpCode.SharpZipLib.Zip;

namespace Gondwana.Tooling.Assets;

/// <summary>A resolved Gondwana dependency, separate from the referring document's identity.</summary>
/// <param name="Document">Containing physical document.</param>
/// <param name="Member">Containing logical member.</param>
/// <param name="Target">Exact typed dependency location.</param>
/// <param name="Selector">Logical member selected within the target, when applicable.</param>
/// <param name="ReferencePath">Format-specific diagnostic path.</param>
public sealed record DefinitionReference(AssetLocation Document, string Member, AssetLocation Target, string? Selector, string ReferencePath);

/// <summary>Discovers validated physical references in an explicit directory or single archive scope.</summary>
public sealed class DefinitionReferenceDiscovery
{
    /// <summary>Filesystem path identity for this platform.</summary>
    internal static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs", "node_modules" };
    private readonly Func<string, string?> _credentials;
    private readonly CancellationToken _cancellation;
    /// <summary>Original file bytes; null marks a verified absent destination.</summary>
    internal readonly Dictionary<string, byte[]?> Files = new(Paths);
    /// <summary>Independent archive snapshots and individual credentials.</summary>
    internal readonly Dictionary<string, Archive> Archives = new(Paths);
    /// <summary>Typed resources, including dependencies validated outside discovery scope.</summary>
    internal readonly Dictionary<string, Content> Contents = new(StringComparer.Ordinal);
    /// <summary>References owned by explicitly included documents.</summary>
    internal readonly List<DefinitionReference> Found = [];
    /// <summary>Resources authorized for discovery, separate from external validation.</summary>
    internal readonly HashSet<string> Included = [];
    private readonly string[] _scopeFiles;

    /// <summary>Archive configuration and typed bytes.</summary>
    /// <param name="Password">This archive's password.</param>
    /// <param name="Encrypt">Whether AES encryption is enabled.</param>
    /// <param name="Entries">Independent entry contents.</param>
    internal sealed record Archive(string? Password, bool Encrypt, Dictionary<AssetsFileEntry, byte[]> Entries);
    /// <summary>A typed physical snapshot.</summary>
    /// <param name="Location">Exact physical location.</param>
    /// <param name="Bytes">Original bytes.</param>
    internal sealed record Content(AssetLocation Location, byte[] Bytes)
    {
        /// <summary>Gets or sets the validated authoring document.</summary>
        internal PackDefinitionDocument? Document { get; set; }
    }

    /// <summary>Gets the actual directory or single-GAF scope searched.</summary>
    public string Scope { get; }
    /// <summary>Gets all discovered references, including every logical member.</summary>
    public IReadOnlyList<DefinitionReference> References => Found.AsReadOnly();
    /// <summary>Gets the files inspected, including dependencies checked outside scope.</summary>
    public IReadOnlyList<string> InspectedFiles => Files.Keys.ToArray();

    private DefinitionReferenceDiscovery(string scope, Func<string, string?>? credentials, CancellationToken cancellation)
    {
        Scope = Path.GetFullPath(scope);
        _credentials = credentials ?? (_ => null);
        _cancellation = cancellation;
        _scopeFiles = Enumerate(Scope).ToArray();
        foreach (var path in _scopeFiles) Include(path, archive: File.Exists(Scope));
    }

    /// <summary>Scans all relevant resources or fails clearly; inaccessible resources are never skipped.</summary>
    /// <param name="scope">An existing directory or single .gaf file.</param>
    /// <param name="credentials">Per-archive password provider; paths are absolute. Null means no password.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>A complete, validated inventory for the declared scope.</returns>
    public static DefinitionReferenceDiscovery Scan(string scope, Func<string, string?>? credentials = null, CancellationToken cancellation = default)
    {
        var result = new DefinitionReferenceDiscovery(scope, credentials, cancellation);
        result.Analyze();
        return result;
    }

    /// <summary>Inventories scope before a move adds its explicitly selected endpoints and performs validation.</summary>
    /// <param name="scope">Directory or single archive.</param>
    /// <param name="credentials">Per-archive credential provider.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>An unvalidated inventory owned by the planner.</returns>
    internal static DefinitionReferenceDiscovery Inventory(string scope, Func<string, string?>? credentials, CancellationToken cancellation)
        => new(scope, credentials, cancellation);

    /// <summary>Finds inbound references using exact typed physical identity.</summary>
    /// <param name="target">The physical resource to find.</param>
    /// <returns>References in the declared scope, never a project-wide claim.</returns>
    public IReadOnlyList<DefinitionReference> FindInbound(AssetLocation target) => Found.Where(r => Same(r.Target, target)).ToArray();

    /// <summary>Computes exact typed physical identity using platform and GAF comparison rules.</summary>
    /// <param name="location">Physical endpoint.</param>
    /// <returns>Normalized dictionary key.</returns>
    internal static string Key(AssetLocation location) => $"{(int)location.AssetType}|{(OperatingSystem.IsWindows() ? location.Path.ToUpperInvariant() : location.Path)}|{location.AssetEntryName?.ToUpperInvariant()}";
    /// <summary>Compares exact typed endpoints.</summary>
    /// <param name="a">First endpoint.</param>
    /// <param name="b">Second endpoint.</param>
    /// <returns>Whether the endpoints identify the same resource.</returns>
    internal static bool Same(AssetLocation a, AssetLocation b) => Key(a) == Key(b);
    /// <summary>Recognizes the five native definition types.</summary>
    /// <param name="type">Asset type.</param>
    /// <returns>Whether the type contains a native document.</returns>
    internal static bool Native(AssetTypes type) => type is AssetTypes.TilesheetDefinition or AssetTypes.AnimationDefinition or AssetTypes.SceneDefinition or AssetTypes.SpriteDefinition or AssetTypes.AudioDefinition;
    /// <summary>Rejects unsafe names, links, parent-file collisions, and case collisions.</summary>
    /// <param name="path">Absolute physical path.</param>
    internal static void CheckPath(string path)
    {
        SafeFilePath.Resolve(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (current != path && File.Exists(current)) throw new IOException($"Parent is a file: {current}.");
            var parent = Path.GetDirectoryName(current);
            if (!OperatingSystem.IsWindows() && parent is not null && Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent).Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(p), Path.GetFileName(current)) && Path.GetFileName(p) != Path.GetFileName(current)))
                throw new IOException($"Case-only path collision: {current}.");
        }
    }

    private static IEnumerable<string> Enumerate(string scope)
    {
        CheckPath(scope);
        if (File.Exists(scope))
        {
            yield return scope;
            yield break;
        }
        if (!Directory.Exists(scope)) throw new DirectoryNotFoundException(scope);
        var pending = new Stack<string>();
        pending.Push(scope);
        while (pending.TryPop(out var directory))
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                if (Excluded.Contains(Path.GetFileName(path))) continue;
                CheckPath(path); // Reject links rather than silently producing an incomplete inventory.
                if (Directory.Exists(path)) pending.Push(path);
                else if (Path.GetExtension(path).Equals(".gaf", StringComparison.OrdinalIgnoreCase) || Native(AssetImportPlan.InferType(path))) yield return path;
            }
    }

    /// <summary>Snapshots a dependency without following links.</summary>
    /// <param name="path">Absolute physical path.</param>
    /// <returns>Original bytes.</returns>
    internal byte[] Read(string path)
    {
        _cancellation.ThrowIfCancellationRequested();
        CheckPath(path);
        if (!Files.TryGetValue(path, out var bytes)) Files.Add(path, bytes = File.ReadAllBytes(path));
        return bytes ?? throw new FileNotFoundException("Missing dependency", path);
    }

    /// <summary>Adds an explicitly authorized loose file or complete archive to discovery.</summary>
    /// <param name="path">Absolute physical path.</param>
    /// <param name="archive">Whether the endpoint explicitly identifies an archive, regardless of extension.</param>
    internal void Include(string path, bool archive = false)
    {
        if (archive || Path.GetExtension(path).Equals(".gaf", StringComparison.OrdinalIgnoreCase))
        {
            ReadArchive(path);
            foreach (var content in Contents.Values.Where(c => Paths.Equals(c.Location.Path, path))) Included.Add(Key(content.Location));
        }
        else
        {
            var location = new AssetLocation(AssetImportPlan.InferType(path), path);
            Contents.TryAdd(Key(location), new(location, Read(path)));
            Included.Add(Key(location));
        }
    }

    /// <summary>Opens an archive with its own credential and snapshots all entries.</summary>
    /// <param name="path">Absolute archive path.</param>
    /// <returns>Validated independent snapshot.</returns>
    internal Archive ReadArchive(string path)
    {
        if (Archives.TryGetValue(path, out var result)) return result;
        var bytes = Read(path);
        var password = _credentials(path);
        try
        {
            AssetsFile.Validate(path, password);
            using var zip = new ZipFile(new MemoryStream(bytes));
            bool encrypt = zip.Cast<ZipEntry>().Any(entry => entry.AESKeySize > 0);
            using var archive = AssetsFile.LoadOrCreate(path, password, encrypt, register: false);
            result = new(password, encrypt, AssetImportPlan.Snapshot(archive));
            Archives.Add(path, result);
            foreach (var pair in result.Entries)
            {
                SafeFilePath.ValidateRelative(pair.Key.AssetName);
                var location = new AssetLocation(pair.Key.AssetType, path, pair.Key.AssetName);
                Contents.Add(Key(location), new(location, pair.Value));
            }
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException($"Archive changed while scanning: {path}.");
            return result;
        }
        catch (Exception ex) { throw new InvalidDataException($"Cannot inspect archive '{path}'; supply its own correct credential. {ex.Message}", ex); }
    }

    /// <summary>Resolves an exact physical reference or an unambiguous logical member.</summary>
    /// <param name="location">Explicit location, or null for logical lookup.</param>
    /// <param name="type">Required type.</param>
    /// <param name="selector">Required member identity when applicable.</param>
    /// <returns>The matching resource.</returns>
    internal Content Resolve(AssetLocation? location, AssetTypes type, string? selector)
    {
        _cancellation.ThrowIfCancellationRequested();
        if (location is null)
        {
            var comparer = type == AssetTypes.AnimationDefinition ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var matches = Included.Select(key => Contents[key]).Where(c => c.Location.AssetType == type &&
                PackDefinitionDocument.Read(type, c.Bytes, c.Location.AssetEntryName ?? c.Location.Path)?.Members().Any(m => comparer.Equals(m.Identity, selector)) == true).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"Logical {type} '{selector}' has {matches.Length} matching documents; author an explicit physical source.");
            return matches[0];
        }
        if (Contents.TryGetValue(Key(location), out var found)) return found;
        if (location.AssetEntryName is not null)
        {
            ReadArchive(location.Path);
            return Contents.GetValueOrDefault(Key(location)) ?? throw new InvalidDataException($"Missing typed entry {location}.");
        }
        var inferred = AssetImportPlan.InferType(location.Path);
        if (Native(inferred) && inferred != type) throw new InvalidDataException($"Incompatible dependency type: {location}.");
        return new(location, Read(location.Path));
    }

    /// <summary>Validates and enumerates all included documents using existing format handlers.</summary>
    internal void Analyze()
    {
        Found.Clear();
        foreach (var content in Included.Select(key => Contents[key]).ToArray())
            content.Document = SemanticPackPlan.Discover(content.Location, content.Bytes, (location, type, selector) =>
            {
                var target = Resolve(location, type, selector);
                return (target.Location, target.Bytes);
            }, Found.Add, _cancellation);
    }

    /// <summary>Rejects changed scope inventory, changed bytes, or newly occupied destinations.</summary>
    internal void Recheck()
    {
        if (!_scopeFiles.Order(StringComparer.Ordinal).SequenceEqual(Enumerate(Scope).Order(StringComparer.Ordinal)))
            throw new IOException("Search scope changed after planning; scan again.");
        foreach (var pair in Files)
        {
            CheckPath(pair.Key);
            if (pair.Value is null ? File.Exists(pair.Key) || Directory.Exists(pair.Key) : !File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).AsSpan().SequenceEqual(pair.Value))
                throw new IOException($"Resource changed after planning: {pair.Key}.");
        }
    }
}
