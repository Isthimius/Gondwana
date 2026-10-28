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

/// <summary>An inspection failure; a reference is present when an explicit endpoint was available.</summary>
public sealed record ReferenceScanProblem(string Source, string Message, DefinitionReference? Reference = null,
    ReferenceProblemKind Kind = ReferenceProblemKind.Invalid);

/// <summary>Actionable categories for diagnostic inspection failures.</summary>
public enum ReferenceProblemKind
{
    /// <summary>Malformed content, incorrect types, or unsafe entries.</summary>
    Invalid,
    /// <summary>A file, typed entry, or selected member was not found.</summary>
    Missing,
    /// <summary>More than one definition matches the selector.</summary>
    Ambiguous,
    /// <summary>File or directory access failed.</summary>
    Inaccessible,
    /// <summary>An archive requires a valid credential.</summary>
    Authentication
}

internal sealed class ReferenceInspectionException(ReferenceProblemKind kind, string message, Exception? inner = null)
    : IOException(message, inner)
{
    internal ReferenceProblemKind Kind { get; } = kind;
}

/// <summary>Discovers validated physical references in an explicit directory or single archive scope.</summary>
public sealed class DefinitionReferenceDiscovery
{
    /// <summary>Filesystem path identity for this platform.</summary>
    internal static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs", "node_modules" };
    private Func<string, string?> _credentials;
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
    public IReadOnlyList<string> InspectedFiles => _inspectedFiles ?? Files.Keys.ToArray();
    private string[]? _inspectedFiles;
    /// <summary>Gets failures encountered by a diagnostic scan. Strict scans still throw.</summary>
    public IReadOnlyList<ReferenceScanProblem> Problems => _problems.AsReadOnly();
    private readonly List<ReferenceScanProblem> _problems = [];
    private readonly List<(ResourceAddress Source, string Uri)> _externalReferences = [];
    /// <summary>Gets remote references as authored, without treating their URIs as local files.</summary>
    public IReadOnlyList<(ResourceAddress Source, string Uri)> ExternalReferences => _externalReferences.AsReadOnly();
    /// <summary>Gets whether cancellation prevented completion.</summary>
    public bool IsCancelled { get; private set; }
    /// <summary>Gets whether the entire declared scope was successfully inspected.</summary>
    public bool IsComplete => !IsCancelled && _problems.Count == 0;
    /// <summary>Tests whether a typed endpoint was included, rather than merely validated externally.</summary>
    public bool Includes(AssetLocation location) => Included.Contains(Key(location));
    /// <summary>Tests the physical scope boundary, including media not enumerated as definitions.</summary>
    public bool IsWithinScope(AssetLocation location) => Paths.Equals(location.Path, Scope) ||
        location.Path.StartsWith(Path.EndsInDirectorySeparator(Scope) ? Scope : Scope + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static ReferenceProblemKind Classify(Exception error) => error switch
    {
        ReferenceInspectionException inspection => inspection.Kind,
        FileNotFoundException or DirectoryNotFoundException => ReferenceProblemKind.Missing,
        UnauthorizedAccessException => ReferenceProblemKind.Inaccessible,
        _ when error.InnerException is not null => Classify(error.InnerException),
        IOException when error is not InvalidDataException => ReferenceProblemKind.Inaccessible,
        _ => ReferenceProblemKind.Invalid
    };

    private DefinitionReferenceDiscovery(string scope, Func<string, string?>? credentials, CancellationToken cancellation, bool diagnostic = false)
    {
        Scope = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scope));
        _credentials = credentials ?? (_ => null);
        _cancellation = cancellation;
        var paths = new List<string>();
        try
        {
            foreach (var path in Enumerate(Scope, cancellation))
            {
                cancellation.ThrowIfCancellationRequested();
                paths.Add(path);
                try { Include(path, archive: File.Exists(Scope)); }
                catch (Exception ex) when (diagnostic && ex is not OperationCanceledException)
                { _problems.Add(new(path, ex.Message, Kind: Classify(ex))); }
            }
        }
        catch (OperationCanceledException) when (diagnostic) { IsCancelled = true; }
        catch (Exception ex) when (diagnostic) { _problems.Add(new(Scope, ex.Message, Kind: Classify(ex))); }
        _scopeFiles = paths.ToArray();
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

    /// <summary>Uses the same inventory and semantic handlers for read-only exploration, retaining failures and partial results.</summary>
    /// <remarks>Failures can prevent discovery of later references in the affected document. Never use this partial inventory for mutation planning.</remarks>
    public static DefinitionReferenceDiscovery Inspect(string scope, Func<string, string?>? credentials = null, CancellationToken cancellation = default)
    {
        var result = new DefinitionReferenceDiscovery(scope, credentials, cancellation, diagnostic: true);
        foreach (var content in result.Included.Select(key => result.Contents[key]).ToArray())
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                content.Document = SemanticPackPlan.Discover(content.Location, content.Bytes, (location, type, selector) =>
                {
                    var target = result.Resolve(location, type, selector);
                    return (target.Location, target.Bytes);
                }, result.Found.Add, cancellation, (reference, error) =>
                    result._problems.Add(new(content.Location.ToString(), error.Message, reference, Classify(error))),
                    (member, uri, field) => result._externalReferences.Add((new(content.Location, member, field), uri)));
            }
            catch (OperationCanceledException) { result.IsCancelled = true; break; }
            catch (Exception ex) { result._problems.Add(new(content.Location.ToString(), ex.Message, Kind: Classify(ex))); }
        }
        // Exploration needs identities and diagnostics, not retained media bytes or passwords.
        result._inspectedFiles = result.Files.Keys.ToArray();
        result.Files.Clear();
        result.Archives.Clear();
        result.Contents.Clear();
        result._credentials = _ => null;
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
    internal static bool Native(AssetTypes type) => PackDefinitionDocument.IsNative(type);
    /// <summary>Rejects unsafe names, links, parent-file collisions, and case collisions.</summary>
    /// <param name="path">Absolute physical path.</param>
    internal static void CheckPath(string path)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Path.GetDirectoryName(path) is { } root)
            SafeFilePath.Resolve(root, Path.GetFileName(path));
        else if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new DirectoryInfo(path).LinkTarget is not null)
            throw new IOException($"Refusing symbolic link or junction root: {path}.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (current != path && File.Exists(current)) throw new IOException($"Parent is a file: {current}.");
            var parent = Path.GetDirectoryName(current);
            if (!OperatingSystem.IsWindows() && parent is not null && Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent).Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(p), Path.GetFileName(current)) && Path.GetFileName(p) != Path.GetFileName(current)))
                throw new IOException($"Case-only path collision: {current}.");
        }
    }

    private static IEnumerable<string> Enumerate(string scope, CancellationToken cancellation = default)
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
                cancellation.ThrowIfCancellationRequested();
                if (Excluded.Contains(Path.GetFileName(path))) continue;
                CheckPath(path); // Reject links rather than silently producing an incomplete inventory.
                if (Directory.Exists(path)) pending.Push(path);
                else if (AssetArchiveFormat.IsArchive(path) || Native(AssetImportPlan.InferType(path))) yield return path;
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
        if (archive || AssetArchiveFormat.IsArchive(path))
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
            using var zip = new ZipFile(new MemoryStream(bytes));
            bool encrypt = zip.Cast<ZipEntry>().Any(entry => entry.AESKeySize > 0);
            try { AssetsFile.Validate(path, password); }
            catch (Exception ex) when (zip.Cast<ZipEntry>().Any(entry => entry.IsCrypted))
            { throw new ReferenceInspectionException(ReferenceProblemKind.Authentication, $"Authentication required or failed for '{path}'. Open the archive with its correct password and rescan.", ex); }
            using var archive = AssetsFile.LoadOrCreate(path, password, encrypt, register: false);
            result = new(password, encrypt, AssetImportPlan.Snapshot(archive));
            Archives.Add(path, result);
            foreach (var pair in result.Entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                SafeFilePath.ValidateRelative(pair.Key.AssetName);
                var location = new AssetLocation(pair.Key.AssetType, path, pair.Key.AssetName);
                Contents.Add(Key(location), new(location, pair.Value));
            }
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException($"Archive changed while scanning: {path}.");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new InvalidDataException($"Cannot inspect archive '{path}'; verify its contents and credential. {ex.Message}", ex); }
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
            if (matches.Length != 1) throw new ReferenceInspectionException(matches.Length == 0 ? ReferenceProblemKind.Missing : ReferenceProblemKind.Ambiguous,
                $"Logical {type} '{selector}' has {matches.Length} matching documents in scope; author an explicit physical source.");
            return matches[0];
        }
        if (Contents.TryGetValue(Key(location), out var found)) return found;
        if (location.AssetEntryName is not null)
        {
            ReadArchive(location.Path);
            return Contents.GetValueOrDefault(Key(location)) ?? throw new ReferenceInspectionException(ReferenceProblemKind.Missing, $"Missing typed entry {location}.");
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
