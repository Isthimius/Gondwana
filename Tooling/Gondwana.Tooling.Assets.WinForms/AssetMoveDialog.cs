using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>Shared scope, destination, credential, and preview workflow for Studio and standalone moves.</summary>
public static class AssetMoveDialog
{
    /// <summary>Reviews a move and invokes the host's guarded executor only after confirmation.</summary>
    /// <param name="owner">Dialog owner.</param>
    /// <param name="source">Exact original location; for whole archives only its path is used.</param>
    /// <param name="scope">Suggested explicit directory or GAF scope.</param>
    /// <param name="execute">Host executor, responsible for open-document protection and refresh.</param>
    /// <param name="wholeArchive">Whether to move every entry and retire the entire GAF.</param>
    /// <param name="knownPasswords">Known credentials indexed by absolute archive path.</param>
    /// <returns>Whether the transaction completed.</returns>
    public static bool Show(IWin32Window owner, AssetLocation source, string scope, Action<AssetRelocationPlan> execute,
        bool wholeArchive = false, IReadOnlyDictionary<string, string?>? knownPasswords = null)
    {
        var destination = InputDialog.Show(owner, "Move / Rename", "Destination file or GAF path:", source.Path);
        if (string.IsNullOrWhiteSpace(destination)) return false;
        string? entry = null;
        if (!wholeArchive)
        {
            entry = InputDialog.Show(owner, "Destination storage", "Typed GAF entry name; leave empty for a loose destination:", source.AssetEntryName ?? "");
            if (entry is null) return false;
            if (entry.Length == 0) entry = null;
        }
        var selectedScope = InputDialog.Show(owner, "Search scope", "Directory to inspect recursively, or one GAF/ZIP. References outside the inspected scope and game code are UNKNOWN:", scope);
        if (string.IsNullOrWhiteSpace(selectedScope)) return false;
        var credential = AssetOperationDialog.Credentials(owner, knownPasswords);
        try
        {
            var plan = wholeArchive
                ? AssetRelocationPlan.MoveArchive(source.Path, destination, selectedScope, credential)
                : AssetRelocationPlan.Create(selectedScope, [new(source, new(source.AssetType, Path.GetFullPath(destination), entry))], credential);
            if (!AssetOperationDialog.Review(owner, "Move preview — changes will be saved to disk", plan.Preview, "Move")) return false;
            execute(plan);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Move not completed", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
}
