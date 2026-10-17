using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>Prepares scope-aware true moves and executes recoverable multi-file transactions.</summary>
public sealed partial class AssetRelocationPlan
{
    private readonly DefinitionReferenceDiscovery _scan;
    private readonly Dictionary<string, byte[]> _looseWrites = new(DefinitionReferenceDiscovery.Paths);
    private readonly Dictionary<string, DefinitionReferenceDiscovery.Archive> _archiveWrites = new(DefinitionReferenceDiscovery.Paths);
    private readonly HashSet<string> _removals = new(DefinitionReferenceDiscovery.Paths);
    private readonly List<string> _preview = [];
    private readonly List<ReferenceRelocation> _mappings;
    private readonly Dictionary<string, (AssetLocation Location, byte[] Bytes)> _final = [];

    /// <summary>Gets the declared directory or single-archive scope.</summary>
    public string Scope => _scan.Scope;
    /// <summary>Gets the exact resolved inbound references affected by this move.</summary>
    public IReadOnlyList<DefinitionReference> InboundReferences { get; private set; } = [];
    /// <summary>Gets the prepared human-readable review, including scope limitations.</summary>
    public IReadOnlyList<string> Preview => _preview.AsReadOnly();
    /// <summary>Gets every file written or retired; hosts must protect open documents at these paths.</summary>
    public IReadOnlyList<string> AffectedFiles => _looseWrites.Keys.Concat(_archiveWrites.Keys).Concat(_removals).Distinct(DefinitionReferenceDiscovery.Paths).ToArray();
    /// <summary>Gets loose files and whole archives removed after successful writes.</summary>
    public IReadOnlyList<string> RetiredFiles => _removals.ToArray();
    /// <summary>Gets exact typed old-to-new mappings. Logical identities are unchanged.</summary>
    public IReadOnlyList<ReferenceRelocation> Mappings => _mappings.AsReadOnly();

    private AssetRelocationPlan(DefinitionReferenceDiscovery scan, IEnumerable<ReferenceRelocation> mappings)
    {
        _scan = scan;
        _mappings = mappings.ToList();
    }

    /// <summary>Prepares a batch of true physical moves without modifying files or editor state.</summary>
    /// <param name="scope">Directory/project scope or single GAF.</param>
    /// <param name="mappings">Exact typed source/destination pairs.</param>
    /// <param name="credentials">Per-archive credential provider.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>A validated plan, with no silent overwrites.</returns>
    public static AssetRelocationPlan Create(string scope, IEnumerable<ReferenceRelocation> mappings,
        Func<string, string?>? credentials = null, CancellationToken cancellation = default)
    {
        var plan = new AssetRelocationPlan(DefinitionReferenceDiscovery.Inventory(scope, credentials, cancellation), mappings);
        plan.Prepare(credentials, cancellation);
        return plan;
    }

    /// <summary>Prepares a whole-GAF move, including every typed entry and external inbound references.</summary>
    /// <param name="source">Existing archive path.</param>
    /// <param name="destination">New archive path; must not exist.</param>
    /// <param name="scope">Explicit discovery scope.</param>
    /// <param name="credentials">Per-archive credential provider.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>A whole-archive move plan preserving encryption and binary contents.</returns>
    public static AssetRelocationPlan MoveArchive(string source, string destination, string scope,
        Func<string, string?>? credentials = null, CancellationToken cancellation = default)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        var scan = DefinitionReferenceDiscovery.Inventory(scope, credentials, cancellation);
        scan.Include(source, archive: true);
        EnsureNewFile(destination);
        var original = scan.ReadArchive(source);
        var mappings = original.Entries.Keys.Select(key => new ReferenceRelocation(
            new(key.AssetType, source, key.AssetName), new(key.AssetType, destination, key.AssetName)));
        var plan = new AssetRelocationPlan(scan, mappings);
        plan._archiveWrites.Add(destination, new(original.Password, original.Encrypt, new()));
        scan.Files.TryAdd(destination, null);
        plan.Prepare(credentials, cancellation);
        plan._archiveWrites.Remove(source);
        plan._removals.Add(source);
        plan._preview.Add($"Retire whole archive: {source}");
        return plan;
    }

    private static void EnsureNewFile(string path)
    {
        DefinitionReferenceDiscovery.CheckPath(path);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"Destination exists: {path}.");
    }

    private void Prepare(Func<string, string?>? credentials, CancellationToken cancellation)
    {
        SafeFilePath.ValidateDestinations(_mappings.Select(m => (m.Destination.Path, m.Destination.AssetEntryName is not null)));
        var sourceKeys = new HashSet<string>();
        var destinationKeys = new HashSet<string>();
        foreach (var mapping in _mappings)
        {
            cancellation.ThrowIfCancellationRequested();
            var source = mapping.Source;
            var destination = mapping.Destination;
            if (!sourceKeys.Add(DefinitionReferenceDiscovery.Key(source)) || !destinationKeys.Add(DefinitionReferenceDiscovery.Key(destination)))
                throw new InvalidDataException("Duplicate source or destination identity.");
            if (StringComparer.OrdinalIgnoreCase.Equals(source.Path, destination.Path) &&
                StringComparer.OrdinalIgnoreCase.Equals(source.AssetEntryName, destination.AssetEntryName))
                throw new InvalidDataException("Same-identity and case-only moves are not supported.");
            if (source.AssetEntryName is null && AssetArchiveFormat.IsArchive(source.Path))
                throw new InvalidDataException("Archive files require MoveArchive (CLI --archive); ordinary loose relocation cannot preserve their references.");
            if (destination.AssetEntryName is null && AssetArchiveFormat.IsArchive(destination.Path))
                throw new InvalidDataException("An ordinary loose destination cannot be an archive file; use MoveArchive or a typed packed entry.");
            if (source.AssetEntryName is not null) SafeFilePath.ValidateRelative(source.AssetEntryName);
            var content = _scan.Resolve(source, source.AssetType, null);
            _scan.Contents[DefinitionReferenceDiscovery.Key(source)] = content;
            _scan.Included.Add(DefinitionReferenceDiscovery.Key(source));
            if (source.AssetEntryName is not null) _scan.Include(source.Path, archive: true);
            if (destination.AssetEntryName is null)
            {
                EnsureNewFile(destination.Path);
                if (DefinitionReferenceDiscovery.Native(destination.AssetType) && AssetImportPlan.InferType(destination.Path) != destination.AssetType)
                    throw new InvalidDataException("Loose native destination must retain its format extension.");
                _scan.Files.TryAdd(destination.Path, null);
            }
            else
            {
                SafeFilePath.ValidateRelative(destination.AssetEntryName);
                DefinitionReferenceDiscovery.CheckPath(destination.Path);
                if (!_archiveWrites.ContainsKey(destination.Path))
                {
                    if (File.Exists(destination.Path)) _scan.Include(destination.Path, archive: true);
                    else
                    {
                        EnsureNewFile(destination.Path);
                        _scan.Files.TryAdd(destination.Path, null);
                        var password = credentials?.Invoke(destination.Path);
                        _archiveWrites.Add(destination.Path, new(password, password is not null, new()));
                    }
                }
                var existing = _archiveWrites.GetValueOrDefault(destination.Path) ?? _scan.Archives[destination.Path];
                if (existing.Entries.ContainsKey(new() { AssetType = destination.AssetType, AssetName = destination.AssetEntryName }))
                    throw new InvalidDataException($"Destination entry exists: {destination}.");
            }
            _preview.Add($"Move {source} → {destination}");
        }
        if (sourceKeys.Overlaps(destinationKeys)) throw new InvalidDataException("Source/destination identity collisions are not supported.");
        _scan.Analyze();
        InboundReferences = _scan.Found.Where(r => sourceKeys.Contains(DefinitionReferenceDiscovery.Key(r.Target))).ToArray();
        _preview.Insert(0, $"Scope: {Scope}; explicitly selected source documents and source/destination archives are also inspected. Other outside-scope resources and arbitrary game code are not rewritten.");
        foreach (var reference in InboundReferences) _preview.Add($"Inbound {reference.ReferencePath} → {reference.Target} (selector '{reference.Selector}')");
        foreach (var key in _scan.Included)
        {
            cancellation.ThrowIfCancellationRequested();
            var content = _scan.Contents[key];
            var mapping = _mappings.FirstOrDefault(m => DefinitionReferenceDiscovery.Same(m.Source, content.Location));
            var target = mapping?.Destination ?? content.Location;
            bool changed = mapping is not null || InboundReferences.Any(r => DefinitionReferenceDiscovery.Same(r.Document, content.Location));
            byte[] bytes = content.Bytes;
            if (changed)
            {
                bytes = content.Document?.Relocate(content.Location, target, _mappings, requireAllMappings: false) ?? bytes;
                Write(target, bytes);
                if (content.Document is not null) _preview.Add($"Rebase document {content.Location} → {target}; members: {string.Join(", ", content.Document.Members().Select(m => m.Identity))}");
                foreach (var reference in _scan.Found.Where(r => DefinitionReferenceDiscovery.Same(r.Document, content.Location)))
                {
                    var dependency = _mappings.FirstOrDefault(m => DefinitionReferenceDiscovery.Same(m.Source, reference.Target))?.Destination ?? reference.Target;
                    _preview.Add($"Outbound {reference.ReferencePath}: {reference.Target} → {dependency}; relative to {target.Path}");
                }
                if (mapping is not null) Retire(content.Location);
            }
            if (!_final.TryAdd(DefinitionReferenceDiscovery.Key(target), (target, bytes))) throw new InvalidDataException($"Duplicate final identity: {target}.");
        }
        // Validate the complete prepared graph against its final endpoints, before staging any writes.
        foreach (var content in _final.Values)
            SemanticPackPlan.Discover(content.Location, content.Bytes, (location, type, selector) =>
            {
                if (location is not null && _final.TryGetValue(DefinitionReferenceDiscovery.Key(location), out var target)) return target;
                if (location is not null && sourceKeys.Contains(DefinitionReferenceDiscovery.Key(location))) throw new InvalidDataException($"Reference still targets retired resource: {location}.");
                var external = _scan.Resolve(location, type, selector);
                return (external.Location, external.Bytes);
            }, _ => { }, cancellation);
        _preview.Add("Conflicts: none. Originals are retired only after destination and reference writes succeed. Save/close affected editors before committing.");
        foreach (var path in AffectedFiles) _preview.Add($"Affected file: {path}");
        _scan.Recheck();
    }

    private DefinitionReferenceDiscovery.Archive WritableArchive(string path)
    {
        if (!_archiveWrites.TryGetValue(path, out var archive))
        {
            var original = _scan.ReadArchive(path);
            _archiveWrites.Add(path, archive = new(original.Password, original.Encrypt, new(original.Entries)));
        }
        return archive;
    }

    private void Write(AssetLocation location, byte[] bytes)
    {
        if (location.AssetEntryName is null) _looseWrites.Add(location.Path, bytes);
        else WritableArchive(location.Path).Entries[new() { AssetType = location.AssetType, AssetName = location.AssetEntryName }] = bytes;
    }

    private void Retire(AssetLocation location)
    {
        if (location.AssetEntryName is null) _removals.Add(location.Path);
        else WritableArchive(location.Path).Entries.Remove(new() { AssetType = location.AssetType, AssetName = location.AssetEntryName });
    }
}
