using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>Reviews exact physical deletions and refuses known surviving references unless explicitly authorized.</summary>
public sealed class AssetDeletionPlan
{
    private readonly DefinitionReferenceDiscovery _scan;
    private readonly List<AssetLocation> _targets = [];
    private readonly HashSet<string> _archives = new(DefinitionReferenceDiscovery.Paths);
    private readonly HashSet<string> _removals = new(DefinitionReferenceDiscovery.Paths);
    private readonly Dictionary<string, DefinitionReferenceDiscovery.Archive> _archiveWrites = new(DefinitionReferenceDiscovery.Paths);
    private readonly List<string> _preview = [];

    /// <summary>Gets the declared directory or single-archive scope.</summary>
    public string Scope => _scan.Scope;
    /// <summary>Gets exact resources removed, including every entry of selected whole archives.</summary>
    public IReadOnlyList<AssetLocation> Targets => _targets.AsReadOnly();
    /// <summary>Gets archives explicitly removed as files, including empty archives.</summary>
    public IReadOnlyList<string> WholeArchives => _archives.ToArray();
    /// <summary>Gets inspected files, including explicitly selected archives and validated external dependencies.</summary>
    public IReadOnlyList<string> InspectedFiles => _scan.InspectedFiles;
    /// <summary>Gets all discovered inbound references to selected targets.</summary>
    public IReadOnlyList<DefinitionReference> InboundReferences { get; private set; } = [];
    /// <summary>Gets inbound references whose owning documents are also being deleted.</summary>
    public IReadOnlyList<DefinitionReference> InternalReferences { get; private set; } = [];
    /// <summary>Gets surviving references that explicit force authorization will leave dangling.</summary>
    public IReadOnlyList<DefinitionReference> BlockingReferences { get; private set; } = [];
    /// <summary>Gets whether committing requires explicit permission to break known relationships.</summary>
    public bool ForceRequired => BlockingReferences.Count != 0;
    /// <summary>Gets files replaced or retired by the transaction.</summary>
    public IReadOnlyList<string> AffectedFiles => _archiveWrites.Keys.Concat(_removals).ToArray();
    /// <summary>Gets files removed entirely, as distinct from archives losing entries.</summary>
    public IReadOnlyList<string> RetiredFiles => _removals.ToArray();
    /// <summary>Gets the complete review including scope, internal relationships, and broken-reference consequences.</summary>
    public IReadOnlyList<string> Preview => _preview.AsReadOnly();

    private AssetDeletionPlan(DefinitionReferenceDiscovery scan) => _scan = scan;

    /// <summary>Plans a batch without mutation. Force cannot bypass invalid identities or incomplete discovery.</summary>
    /// <param name="scope">Explicit directory tree or single archive to inspect.</param>
    /// <param name="targets">Loose files and exact typed entries. An archive file must instead be listed in wholeArchives.</param>
    /// <param name="wholeArchives">Optional archive files whose complete contents and container are removed.</param>
    /// <param name="credentials">Individual password provider, keyed by absolute archive path.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>A validated review; blocking references do not prevent preview creation.</returns>
    public static AssetDeletionPlan Create(string scope, IEnumerable<AssetLocation> targets,
        IEnumerable<string>? wholeArchives = null, Func<string, string?>? credentials = null, CancellationToken cancellation = default)
    {
        var plan = new AssetDeletionPlan(DefinitionReferenceDiscovery.Inventory(scope, credentials, cancellation));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in wholeArchives ?? [])
        {
            cancellation.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            if (!plan._archives.Add(fullPath)) throw new InvalidDataException($"Duplicate archive target: {fullPath}.");
            plan._scan.Include(fullPath, archive: true);
            plan._removals.Add(fullPath);
            foreach (var entry in plan._scan.Archives[fullPath].Entries.Keys)
            {
                var location = new AssetLocation(entry.AssetType, fullPath, entry.AssetName);
                plan._targets.Add(location);
                keys.Add(DefinitionReferenceDiscovery.Key(location));
            }
        }
        foreach (var target in targets)
        {
            cancellation.ThrowIfCancellationRequested();
            if (plan._archives.Contains(target.Path) || !keys.Add(DefinitionReferenceDiscovery.Key(target)))
                throw new InvalidDataException($"Duplicate or overlapping deletion target: {target}.");
            if (plan._targets.Any(t => DefinitionReferenceDiscovery.Paths.Equals(t.Path, target.Path) && (t.AssetEntryName is null || target.AssetEntryName is null)))
                throw new InvalidDataException($"Overlapping physical file and entry targets: {target.Path}.");
            if (target.AssetEntryName is null)
            {
                if (AssetArchiveFormat.IsArchive(target.Path)) throw new InvalidDataException("Select whole-archive deletion instead of treating a package as a loose resource.");
                var inferred = AssetImportPlan.InferType(target.Path);
                if (inferred != AssetTypes.Misc && inferred != target.AssetType)
                    throw new InvalidDataException($"Incompatible loose target type: {target} (expected {inferred}).");
                var content = plan._scan.Resolve(target, target.AssetType, null);
                plan._scan.Contents[DefinitionReferenceDiscovery.Key(target)] = content;
                plan._scan.Included.Add(DefinitionReferenceDiscovery.Key(target));
                plan._removals.Add(target.Path);
            }
            else
            {
                SafeFilePath.ValidateRelative(target.AssetEntryName);
                plan._scan.Include(target.Path, archive: true);
                plan._scan.Resolve(target, target.AssetType, null); // Exact type and entry must exist.
                if (!plan._archiveWrites.TryGetValue(target.Path, out var replacement))
                {
                    var original = plan._scan.Archives[target.Path];
                    plan._archiveWrites.Add(target.Path, replacement = new(original.Password, original.Encrypt, new(original.Entries)));
                }
                replacement.Entries.Remove(new() { AssetType = target.AssetType, AssetName = target.AssetEntryName });
            }
            plan._targets.Add(target);
        }
        if (plan._targets.Count == 0 && plan._archives.Count == 0) throw new InvalidDataException("Select at least one resource to delete.");
        plan._scan.Analyze();
        // A loose file has only one physical identity; a misleading type must not hide its references.
        foreach (var target in plan._targets.Where(t => t.AssetEntryName is null))
        {
            if (plan._scan.Archives.ContainsKey(target.Path))
                throw new InvalidDataException($"Referenced archive requires whole-archive deletion: {target.Path}.");
            if (plan._scan.Found.Any(r => r.Target.AssetEntryName is null && DefinitionReferenceDiscovery.Paths.Equals(r.Target.Path, target.Path) && r.Target.AssetType != target.AssetType))
                throw new InvalidDataException($"Loose target has references with a different type: {target}.");
        }
        plan.InboundReferences = plan._targets.SelectMany(plan._scan.FindInbound).Distinct().ToArray();
        plan.InternalReferences = plan.InboundReferences.Where(r => keys.Contains(DefinitionReferenceDiscovery.Key(r.Document))).ToArray();
        plan.BlockingReferences = plan.InboundReferences.Except(plan.InternalReferences).ToArray();
        plan._preview.Add($"Search scope: {plan.Scope}. Explicit target documents and complete target archives are also inspected. References outside this scope and arbitrary game code are unknown.");
        foreach (var path in plan.InspectedFiles) plan._preview.Add($"Inspected: {path}");
        foreach (var target in plan.Targets) plan._preview.Add($"Delete: {target}");
        foreach (var path in plan.WholeArchives) plan._preview.Add($"Delete entire archive: {path}");
        foreach (var reference in plan.InternalReferences) plan._preview.Add($"Internal (owner also deleted): {Describe(reference)}");
        foreach (var reference in plan.BlockingReferences) plan._preview.Add($"WILL BREAK: {Describe(reference)}");
        plan._preview.Add(plan.ForceRequired
            ? $"Blocked: {plan.BlockingReferences.Count} surviving references. Explicit force leaves precisely these references unchanged and dangling; dependents are not deleted."
            : "No surviving inbound references found within the inspected scope.");
        plan._preview.Add("Deletion commits to disk. Save/close editors first. Empty archives remain unless explicitly selected for whole-archive deletion.");
        plan._scan.Recheck();
        return plan;
    }

    private static string Describe(DefinitionReference reference)
        => $"{reference.Document} :: member '{reference.Member}' :: {reference.ReferencePath} -> {reference.Target} (selector '{reference.Selector}')";

    /// <summary>Commits only the reviewed targets, preserving surviving bytes and rolling back ordinary failures.</summary>
    /// <param name="force">Explicit authorization to leave the listed surviving references dangling.</param>
    /// <param name="cancellation">Cancellation token, honored through retirement.</param>
    public void Commit(bool force = false, CancellationToken cancellation = default) => CommitCore(force, null, cancellation);

    /// <summary>Runs the shared transaction with a test checkpoint; force never skips snapshot validation.</summary>
    /// <param name="force">Permission to break reviewed relationships.</param>
    /// <param name="checkpoint">Optional fault/cancellation observer.</param>
    /// <param name="cancellation">Cancellation token.</param>
    internal void CommitCore(bool force, Action<string, int>? checkpoint, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (ForceRequired && !force) throw new InvalidOperationException("Deletion blocked by surviving references. Review Preview and explicitly authorize force to break them.");
        // Discovery validated every original relationship. Only reviewed targets are removed; surviving bytes
        // are preserved exactly and the shared transaction rechecks every inspected dependency. Therefore
        // force permits only the reviewed dangling edges, never unrelated pre-existing or concurrent corruption.
        new AssetFileTransaction(_scan, new(DefinitionReferenceDiscovery.Paths), _archiveWrites, _removals, [], _preview).CommitCore(checkpoint, cancellation);
    }
}
