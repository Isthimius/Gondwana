using Gondwana.Assets;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Assets;

/// <summary>Document-local entry deltas over the current authoritative AssetsFile; never owns the borrowed archive.</summary>
public sealed class AssetEditSession : IDisposable
{
    /// <summary>Default per-document retained binary payload quota (64 MiB), including undo and redo.</summary>
    public const long DefaultByteLimit = 64L * 1024 * 1024;
    private AssetsFile? _archive;
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
        _ = archive.GetAllEntries().ToArray();
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
            plan.Additions.Select(p => new Delta(p.Key.AssetType, p.Key.AssetName, null, p.Value, Archive.SourcePath!)).ToArray());
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
            [new(type, entry.AssetName, before, replacement.ToArray(), Archive.SourcePath!)]);
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

    private sealed record Delta(AssetTypes Type, string Name, byte[]? Before, byte[] After, string Origin);

    private sealed class EntryCommand(AssetEditSession document, string description, Delta[] deltas) : IEditCommand
    {
        public string Description => description;
        public long RetainedBytes => deltas.Sum(d => (long)(d.Before?.Length ?? 0) + d.After.Length);
        internal CancellationToken InitialCancellation;
        internal Action<AssetsFileEntry, byte[]>? InitialWriter;
        public void Execute() => Apply(undo: false);
        public void Undo() => Apply(undo: true);

        private void Apply(bool undo)
        {
            var archive = document.Archive;
            var path = archive.SourcePath!;
            // Rebase native payloads from their capture location, including entries absent
            // during Save As. Binary entries remain byte-exact. Do not mutate retained bytes.
            var names = archive.GetAllEntries().Concat(deltas.Select(d => new AssetsFileEntry { AssetType = d.Type, AssetName = d.Name })).Distinct().ToArray();
            var prepared = deltas.Select(d =>
            {
                byte[]? bytes = undo ? d.Before : d.After;
                if (bytes is not null && !string.Equals(path, d.Origin, StringComparison.OrdinalIgnoreCase))
                {
                    var mappings = names.Select(n => new ReferenceRelocation(new(n.AssetType, d.Origin, n.AssetName), new(n.AssetType, path, n.AssetName))).ToArray();
                    bytes = PackDefinitionDocument.Read(d.Type, bytes, d.Name)?.Relocate(
                        new(d.Type, d.Origin, d.Name), new(d.Type, path, d.Name), mappings, false) ?? bytes;
                }
                return (Delta: d, Bytes: bytes);
            }).ToArray();
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
    public void Dispose() { History.Dispose(); _archive = null; }
}
