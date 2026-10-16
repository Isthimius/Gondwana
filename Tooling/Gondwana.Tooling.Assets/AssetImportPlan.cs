using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>A reviewed loose input and its exact destination identity.</summary>
/// <param name="FilePath">The source file, never modified.</param>
/// <param name="AssetType">The selected asset type.</param>
/// <param name="EntryName">The destination entry name.</param>
public sealed record AssetImportSource(string FilePath, AssetTypes AssetType, string EntryName);

/// <summary>Prepares semantic additions against a borrowed archive's current in-memory state.</summary>
public sealed class AssetImportPlan
{
    private readonly AssetsFile _destination;
    private readonly Dictionary<AssetsFileEntry, byte[]> _before;
    private readonly IReadOnlyDictionary<AssetsFileEntry, byte[]> _additions;
    private readonly string _path;

    private AssetImportPlan(AssetsFile destination, Dictionary<AssetsFileEntry, byte[]> before,
        IReadOnlyDictionary<AssetsFileEntry, byte[]> additions, IReadOnlyList<string> preview)
    {
        _destination = destination;
        _path = destination.SourcePath!;
        _before = before;
        _additions = additions;
        Preview = preview;
    }

    /// <summary>Gets additions, reused entries, rewritten references, and remaining external dependencies.</summary>
    public IReadOnlyList<string> Preview { get; }

    /// <summary>Gets the number of new physical entries; existing entries are never replaced.</summary>
    public int AdditionCount => _additions.Count;

    /// <summary>Gets whether this plan belongs to the supplied borrowed archive instance.</summary>
    /// <param name="archive">The editor's current archive.</param>
    /// <returns>Whether the exact instance owns this plan.</returns>
    public bool Targets(AssetsFile archive) => ReferenceEquals(_destination, archive);

    /// <summary>Infers standard asset types using the CLI defaults without reading configuration files.</summary>
    /// <param name="path">The source filename.</param>
    /// <returns>The inferred type, or Misc for an unknown extension.</returns>
    public static AssetTypes InferType(string path) => AssetTypeMap.BuildDefault().Infer(path, AssetTypes.Misc);

    /// <summary>Prepares complete documents and optionally their explicit dependency closure without mutation or saving.</summary>
    /// <param name="destination">The borrowed current archive, including unsaved edits.</param>
    /// <param name="sources">Reviewed source types and entry names.</param>
    /// <param name="importDependencies">Whether resolvable physical dependencies should also be copied.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>An immutable prepared plan, or an exception describing a conflict or unresolved dependency.</returns>
    public static AssetImportPlan Create(AssetsFile destination, IEnumerable<AssetImportSource> sources,
        bool importDependencies, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sources);
        cancellation.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(destination.SourcePath)) throw new InvalidOperationException("The destination archive requires a physical path before importing definitions.");
        var before = Snapshot(destination);
        var (additions, preview) = SemanticPackPlan.PrepareImport(destination, before, sources.ToArray(), importDependencies, cancellation);
        return new(destination, before, additions, preview);
    }

    /// <summary>Applies all prepared additions or rolls them back; never saves or disposes the borrowed archive.</summary>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>The number of added entries.</returns>
    public int Apply(CancellationToken cancellation = default) => ApplyCore(null, cancellation);

    /// <summary>Executes the transaction with an optional per-entry writer for fault-injection tests.</summary>
    /// <param name="cancellation">Cancellation token.</param>
    /// <param name="write">Optional writer; null uses the archive's ordinary byte add operation.</param>
    /// <returns>The number of added entries.</returns>
    internal int ApplyCore(Action<AssetsFileEntry, byte[]>? write, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var current = Snapshot(_destination);
        if (_path != _destination.SourcePath || current.Count != _before.Count ||
            _before.Any(pair => !current.TryGetValue(pair.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(pair.Value)))
            throw new InvalidOperationException("The archive changed after preview. Prepare a new import plan.");
        var attempted = new List<AssetsFileEntry>();
        try
        {
            foreach (var pair in _additions)
            {
                cancellation.ThrowIfCancellationRequested();
                attempted.Add(pair.Key);
                if (write is not null) write(pair.Key, pair.Value);
                else
                {
                    using var stream = new MemoryStream(pair.Value, writable: false);
                    _destination.Add(pair.Key.AssetType, pair.Key.AssetName, stream);
                }
            }
            cancellation.ThrowIfCancellationRequested();
        }
        catch
        {
            foreach (var key in attempted) _destination.Remove(key.AssetType, key.AssetName);
            throw;
        }
        return _additions.Count;
    }

    /// <summary>Copies current in-memory entry bytes without saving or assuming disk contents match.</summary>
    /// <param name="archive">The borrowed archive.</param>
    /// <returns>An independent typed snapshot.</returns>
    internal static Dictionary<AssetsFileEntry, byte[]> Snapshot(AssetsFile archive)
    {
        var result = new Dictionary<AssetsFileEntry, byte[]>();
        foreach (var entry in archive.GetAllEntries())
        {
            using var stream = archive.Get(entry.AssetType, entry.AssetName)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            result.Add(new() { AssetType = entry.AssetType, AssetName = entry.AssetName }, buffer.ToArray());
        }
        return result;
    }
}
