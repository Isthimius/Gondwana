namespace Gondwana.Tooling.Assets;

public sealed partial class AssetRelocationPlan
{
    /// <summary>Stages all changes, rechecks snapshots, installs destinations/references, and retires sources last.</summary>
    /// <remarks>Ordinary exceptions and cancellation restore all touched files. Interrupted processes retain sibling backups and a recovery manifest; multi-file moves are not crash-atomic.</remarks>
    /// <param name="cancellation">Cancellation token, honored through source retirement.</param>
    public void Commit(CancellationToken cancellation = default) => CommitCore(null, cancellation);

    /// <summary>Executes with a fault hook for transaction regression tests.</summary>
    /// <param name="checkpoint">Checkpoint observer.</param>
    /// <param name="cancellation">Cancellation token.</param>
    internal void CommitCore(Action<string, int>? checkpoint, CancellationToken cancellation)
        => new AssetFileTransaction(_scan, _looseWrites, _archiveWrites, _removals, _mappings, _preview).CommitCore(checkpoint, cancellation);
}
