using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

internal sealed partial class SemanticPackPlan
{
    private bool _importDependencies;
    private bool _validateReusedTargets;
    private HashSet<string>? _importPreview;
    private readonly Queue<Entry> _importQueue = new();
    private readonly Queue<Entry> _reuseQueue = new();
    private readonly HashSet<Entry> _reuseChecked = [];
    private readonly List<(AssetLocation Source, Entry Entry)> _importAliases = [];

    /// <summary>Prepares additions using the existing typed dependency analysis and Phase 1 relocation.</summary>
    /// <param name="archive">The current destination archive.</param>
    /// <param name="snapshot">Its current in-memory entries.</param>
    /// <param name="sources">Reviewed loose inputs.</param>
    /// <param name="dependencies">Whether to copy the known dependency closure.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>Prepared additions and review text.</returns>
    internal static (IReadOnlyDictionary<AssetsFileEntry, byte[]> Additions, IReadOnlyList<string> Preview) PrepareImport(
        AssetsFile archive, Dictionary<AssetsFileEntry, byte[]> snapshot, AssetImportSource[] sources, bool dependencies, CancellationToken cancellation)
    {
        var root = sources.Length == 0 ? Path.GetDirectoryName(Path.GetFullPath(archive.SourcePath!))! : Path.GetDirectoryName(Path.GetFullPath(sources[0].FilePath))!;
        var plan = new SemanticPackPlan(root, Path.GetFullPath(archive.SourcePath!), archive.Password, archive.UseEncryption, true, cancellation)
        { _resolveLooseFromInventory = false, _importDependencies = dependencies, _importPreview = [] };
        foreach (var pair in snapshot)
        {
            var location = new AssetLocation(pair.Key.AssetType, plan._output, pair.Key.AssetName);
            plan._entries.Add(pair.Key, ReadEntry(location, location, pair.Value));
        }
        foreach (var source in sources)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(source.AssetType)) throw new InvalidDataException("Unknown asset type.");
            var inferred = AssetImportPlan.InferType(source.FilePath);
            if (PackDefinitionDocument.IsNative(inferred) && inferred != source.AssetType)
                throw new InvalidDataException($"Native file '{source.FilePath}' requires type {inferred}.");
            var location = new AssetLocation(source.AssetType, Path.GetFullPath(source.FilePath));
            if (plan._entries.Values.Any(entry => Same(entry.Location, location)) ||
                plan._importAliases.Any(alias => Same(alias.Source, location)))
                throw new InvalidDataException($"Source selected more than once: {location}.");
            var entry = ReadEntry(location, new(source.AssetType, plan._output, source.EntryName), File.ReadAllBytes(location.Path));
            plan.AddImport(entry);
        }
        while (plan._importQueue.Count > 0 || plan._reuseQueue.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            bool incoming = plan._importQueue.TryDequeue(out var entry);
            entry ??= plan._reuseQueue.Dequeue();
            entry.Document?.Validate(entry.Location.ToString());
            // Reuse validates existing references without importing or changing their targets.
            plan._importDependencies = incoming && dependencies;
            plan._validateReusedTargets = !incoming;
            plan.Analyze(entry);
        }
        plan._importDependencies = dependencies;
        var additions = new Dictionary<AssetsFileEntry, byte[]>();
        foreach (var entry in plan._incoming.OrderBy(entry => entry.Destination.AssetType).ThenBy(entry => entry.Destination.AssetEntryName, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var mapping in entry.Mappings)
                plan._importPreview.Add($"Rewrite {entry.Destination.AssetEntryName}: {mapping.Source} → {mapping.Destination}");
            if (entry.Document is not null)
                plan._importPreview.Add($"Document {entry.Destination.AssetEntryName}: {string.Join(", ", entry.Document.Members().Select(member => member.Identity))}; rebase dependencies for destination");
            var bytes = entry.Document?.Relocate(entry.Location, entry.Destination, entry.Mappings) ?? entry.Bytes;
            additions.Add(new() { AssetType = entry.Destination.AssetType, AssetName = entry.Destination.AssetEntryName! }, bytes);
        }
        plan._importPreview.Add("Conflicts: none. Replacements: none (existing entries are never replaced).");
        return (additions, Array.AsReadOnly(plan._importPreview.ToArray()));
    }

    private void TrackReuse(Entry entry)
    {
        if (_importPreview is null || _incoming.Contains(entry)) return;
        bool external = _external.Contains(entry);
        _importPreview.Add($"{(external ? "External" : "Reuse")}: {entry.Destination}");
        if (!external && entry.Document is not null && _reuseChecked.Add(entry)) _reuseQueue.Enqueue(entry);
    }

    private Entry AddImport(Entry entry)
    {
        SafeFilePath.ValidateRelative(entry.Destination.AssetEntryName!);
        var key = new AssetsFileEntry { AssetType = entry.Destination.AssetType, AssetName = entry.Destination.AssetEntryName! };
        if (_entries.TryGetValue(key, out var existing))
        {
            if (entry.Document is null && existing.Document is null && existing.Bytes.AsSpan().SequenceEqual(entry.Bytes))
            {
                _importPreview!.Add($"Reuse identical bytes: {existing.Destination}");
                _importAliases.Add((entry.Location, existing));
                return existing;
            }
            throw new InvalidDataException($"Destination conflict: {entry.Destination}. Existing content will not be replaced. Choose another entry name or explicitly reference the existing packed definition.");
        }
        _entries.Add(key, entry);
        _incoming.Add(entry);
        _importQueue.Enqueue(entry);
        _importPreview!.Add($"Add: {entry.Destination.AssetType} / {entry.Destination.AssetEntryName}");
        return entry;
    }

    private Entry? FindImportAlias(AssetLocation location)
    {
        foreach (var alias in _importAliases)
            if (Same(alias.Source, location))
                return alias.Entry;
        return null;
    }

    private Entry ImportDependency(Entry source)
    {
        string name;
        if (source.Location.AssetEntryName is not null)
            name = "dependencies/" + Path.GetFileNameWithoutExtension(source.Location.Path) + "/" + source.Location.AssetEntryName;
        else
        {
            name = Path.GetRelativePath(_source, source.Location.Path).Replace('\\', '/');
            if (name.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(name))
                name = "dependencies/" + source.Location.AssetType + "/" + Path.GetFileName(source.Location.Path);
        }
        return AddImport(ReadEntry(source.Location, new(source.Location.AssetType, _output, name), source.Bytes));
    }
}
