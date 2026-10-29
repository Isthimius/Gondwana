using Gondwana.Assets;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Assets;

/// <summary>Document-local entry deltas over the current authoritative AssetsFile; never owns the borrowed archive.</summary>
public sealed class AssetEditSession : IDisposable
{
    /// <summary>Default per-document retained binary payload quota (64 MiB), including undo and redo.</summary>
    public const long DefaultByteLimit = 64L * 1024 * 1024;
    private AssetsFile? _archive;
    // Weak registrations let retained payloads follow adoption without keeping pruned bytes alive.
    private readonly List<WeakReference<Payload>> _payloads = [];
    internal int RetainedReferenceCount => _payloads.Sum(w => w.TryGetTarget(out var p) ? p.ReferenceCount : 0);
    /// <summary>The archive currently adopted by the document.</summary>
    public AssetsFile Archive => _archive ?? throw new ObjectDisposedException(nameof(AssetEditSession));
    /// <summary>Shared history, bounded by both command count and payload bytes.</summary>
    public EditHistory History { get; }

    /// <summary>Creates a stable document accessor; commands never capture an archive instance or a control.</summary>
    public AssetEditSession(AssetsFile archive, bool initiallySaved, long byteLimit = DefaultByteLimit)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        History = new(initiallySaved);
        History.ByteLimit = byteLimit;
    }

    /// <summary>Adopts a successfully persisted archive. External refreshes invalidate obsolete local deltas.</summary>
    public void AcceptSavedArchive(AssetsFile archive, bool externalRefresh = false)
    {
        History.RequireIdle();
        ArgumentNullException.ThrowIfNull(archive);
        var entries = archive.GetAllEntries().ToHashSet();
        var previousPath = Archive.SourcePath!;
        if (!externalRefresh && !DefinitionReferenceDiscovery.Paths.Equals(previousPath, archive.SourcePath))
        {
            var updates = new List<(Payload Payload, ReferenceRelocation[] References)>();
            _payloads.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var weak in _payloads)
                if (weak.TryGetTarget(out var payload)) updates.Add((payload, payload.PrepareAdoption(previousPath, archive.SourcePath!, entries)));
            foreach (var update in updates) update.Payload.AcceptAdoption(update.References);
        }
        if (externalRefresh) _payloads.Clear();
        _archive = archive;
        if (externalRefresh) History.ResetSaved();
        else History.MarkSaved();
    }

    /// <summary>Records the exact approved additions as one step; redo never reopens sources or discovers dependencies.</summary>
    /// <param name="plan">Reviewed plan targeting the current archive.</param>
    /// <param name="confirmNonUndoable">Explicit approval for an oversized edit that clears earlier history; null rejects it before mutation.</param>
    /// <param name="cancellation">Cancellation during the initial import only.</param>
    /// <returns>Number of additions, excluding reused entries.</returns>
    public int Import(AssetImportPlan plan, Func<long, bool>? confirmNonUndoable = null, CancellationToken cancellation = default) =>
        ImportCore(plan, confirmNonUndoable, null, cancellation);

    internal int ImportCore(AssetImportPlan plan, Func<long, bool>? confirmNonUndoable,
        Action<AssetsFileEntry, byte[]>? write, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        History.RequireIdle();
        cancellation.ThrowIfCancellationRequested();
        if (!plan.Targets(Archive)) throw new InvalidOperationException("The import plan belongs to another archive.");
        plan.ValidateCurrent();
        if (plan.AdditionCount == 0) return 0;
        var command = new EntryCommand(this, $"Import {plan.AdditionCount} asset(s)",
            plan.Additions.Select(p => new Delta(p.Key.AssetType, p.Key.AssetName, null, Capture(p.Key.AssetType, p.Key.AssetName, p.Value, raw: false))).ToArray());
        Execute(command, confirmNonUndoable, () =>
        {
            cancellation.ThrowIfCancellationRequested();
            if (!plan.Targets(Archive)) throw new InvalidOperationException("The archive changed after preview. Prepare a new import plan.");
            plan.ValidateCurrent();
        }, write, cancellation);
        return plan.AdditionCount;
    }

    /// <summary>Replaces only an existing typed entry. Identical bytes are a no-op; native definitions retain raw-replacement limitations.</summary>
    public bool Replace(AssetTypes type, string name, byte[] replacement, Func<long, bool>? confirmNonUndoable = null) =>
        ReplaceCore(type, name, replacement, confirmNonUndoable, null);

    internal bool ReplaceCore(AssetTypes type, string name, byte[] replacement, Func<long, bool>? confirmNonUndoable,
        Action<AssetsFileEntry, byte[]>? write)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        History.RequireIdle();
        var entry = Archive.GetAllEntries().SingleOrDefault(e => e.AssetType == type && string.Equals(e.AssetName, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Asset entry not found: {type}/{name}");
        using var original = Archive.Get(type, entry.AssetName)!;
        using var buffer = new MemoryStream();
        original.CopyTo(buffer);
        var before = buffer.ToArray();
        if (before.AsSpan().SequenceEqual(replacement)) return false;
        var command = new EntryCommand(this, $"Replace {entry.AssetName}",
            [new(type, entry.AssetName, Capture(type, entry.AssetName, before, raw: true), Capture(type, entry.AssetName, replacement.ToArray(), raw: true))]);
        var capturedArchive = Archive;
        var capturedPath = Archive.SourcePath;
        Execute(command, confirmNonUndoable, () =>
        {
            if (!ReferenceEquals(Archive, capturedArchive) || Archive.SourcePath != capturedPath)
                throw new InvalidOperationException("Archive changed while confirming replacement. Retry the operation.");
            using var current = Archive.Get(type, entry.AssetName);
            using var check = new MemoryStream();
            current?.CopyTo(check);
            if (!Archive.GetAllEntries().Any(e => e.AssetType == type && string.Equals(e.AssetName, entry.AssetName, StringComparison.OrdinalIgnoreCase)) ||
                current is null || !check.ToArray().AsSpan().SequenceEqual(before))
                throw new InvalidOperationException("Entry changed while confirming replacement. Retry the operation.");
        }, write, default);
        return true;
    }

    private void Execute(EntryCommand command, Func<long, bool>? confirm, Action revalidate,
        Action<AssetsFileEntry, byte[]>? write, CancellationToken cancellation)
    {
        bool oversized = command.RetainedBytes > History.ByteLimit;
        if (oversized)
        {
            if (confirm is null) throw new InvalidOperationException("This edit exceeds the binary history budget. Explicit approval is required to proceed without Undo and clear existing Undo/Redo.");
            if (!confirm(command.RetainedBytes)) throw new OperationCanceledException("Oversized edit cancelled; archive and history unchanged.");
        }
        // The confirmation dialog can pump messages: recheck stale plans before committing.
        History.RequireIdle();
        revalidate();
        command.InitialCancellation = cancellation;
        command.InitialWriter = write;
        try
        {
            if (!oversized) History.Execute(command);
            else
            {
                try { History.Capture(command.Execute); }
                catch
                {
                    try { History.Capture(command.Undo); }
                    catch { History.MarkUntracked(); throw; }
                    throw;
                }
                History.MarkUntracked();
            }
        }
        finally { command.InitialCancellation = default; command.InitialWriter = null; }
    }

    private Payload Capture(AssetTypes type, string name, byte[] bytes, bool raw)
    {
        var payload = new Payload(type, name, bytes, Archive.SourcePath!, raw);
        if (payload.IsNative)
        {
            _payloads.RemoveAll(w => !w.TryGetTarget(out _));
            _payloads.Add(new(payload));
        }
        return payload;
    }

    private sealed class Payload
    {
        internal byte[] Bytes { get; }
        private readonly AssetLocation _origin;
        private readonly bool _raw;
        private ReferenceRelocation[]? _references;
        internal bool IsNative => _references is not null;
        internal int ReferenceCount => _references?.Length ?? 0;

        internal Payload(AssetTypes type, string name, byte[] bytes, string origin, bool raw)
        {
            Bytes = bytes;
            _origin = new(type, origin, name);
            _raw = raw;
            if (!PackDefinitionDocument.IsNative(type)) return;
            try
            {
                var definition = PackDefinitionDocument.Read(type, bytes, name)!;
                definition.Validate(_origin.ToString());
                _references = definition.References(_origin).DistinctBy(DefinitionReferenceDiscovery.Key)
                    .Select(location => new ReferenceRelocation(location, location)).ToArray();
                // Validate physical source kinds as well, without touching any external resource.
                definition.Relocate(_origin, _origin, _references, false);
            }
            catch (Exception ex) when (raw && IsOpaqueFailure(ex)) { _references = null; }
        }

        private static bool IsOpaqueFailure(Exception ex) => ex is Newtonsoft.Json.JsonException or InvalidDataException or ArgumentException or NotSupportedException or FormatException or OverflowException;

        internal ReferenceRelocation[] PrepareAdoption(string previous, string destination, HashSet<AssetsFileEntry> entries) =>
            _references!.Select(mapping => mapping.Destination.AssetEntryName is { } entry &&
                DefinitionReferenceDiscovery.Paths.Equals(mapping.Destination.Path, previous) &&
                entries.Contains(new() { AssetType = mapping.Destination.AssetType, AssetName = entry })
                    ? new ReferenceRelocation(mapping.Source, new(mapping.Destination.AssetType, destination, entry)) : mapping).ToArray();

        internal void AcceptAdoption(ReferenceRelocation[] references) => _references = references;

        internal byte[] Materialize(string path)
        {
            // Opaque binaries/raw unsupported definitions never enter the relocation pipeline.
            if (_references is null || DefinitionReferenceDiscovery.Paths.Equals(path, _origin.Path) &&
                _references.All(m => DefinitionReferenceDiscovery.Same(m.Source, m.Destination))) return Bytes;
            try
            {
                return PackDefinitionDocument.Read(_origin.AssetType, Bytes, _origin.AssetEntryName!)!.Relocate(
                    _origin, new(_origin.AssetType, path, _origin.AssetEntryName), _references, false);
            }
            catch (Exception ex) when (_raw && IsOpaqueFailure(ex)) { return Bytes; }
        }
    }

    private sealed record Delta(AssetTypes Type, string Name, Payload? Before, Payload After);

    private sealed class EntryCommand(AssetEditSession document, string description, Delta[] deltas) : IEditCommand
    {
        public string Description => description;
        public long RetainedBytes => deltas.Sum(d => (long)(d.Before?.Bytes.Length ?? 0) + d.After.Bytes.Length);
        internal CancellationToken InitialCancellation;
        internal Action<AssetsFileEntry, byte[]>? InitialWriter;
        public void Execute() => Apply(undo: false);
        public void Undo() => Apply(undo: true);

        private void Apply(bool undo)
        {
            var archive = document.Archive;
            var path = archive.SourcePath!;
            // Captured reference targets follow successful adoptions, independently of undo position.
            // Each native payload has only its own dependency mappings; binaries have none.
            var prepared = deltas.Select(d => (Delta: d, Bytes: (undo ? d.Before : d.After)?.Materialize(path))).ToArray();
            foreach (var item in prepared)
            {
                if (!undo) InitialCancellation.ThrowIfCancellationRequested();
                var d = item.Delta;
                if (item.Bytes is null) archive.Remove(d.Type, d.Name);
                else if (!undo && InitialWriter is { } writer) writer(new() { AssetType = d.Type, AssetName = d.Name }, item.Bytes);
                else
                {
                    using var stream = new MemoryStream(item.Bytes, writable: false);
                    archive.Add(d.Type, d.Name, stream);
                }
            }
            if (!undo) InitialCancellation.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Releases retained commands and the current borrowed archive reference; does not dispose the archive.</summary>
    public void Dispose() { History.Dispose(); _payloads.Clear(); _archive = null; }
}
