using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>Shared deletion scope, credential, review, and explicit break-references confirmation.</summary>
public static class AssetDeleteDialog
{
    /// <summary>Reviews a physical batch and calls the guarded host executor only after confirmation.</summary>
    /// <param name="owner">Dialog owner.</param>
    /// <param name="targets">Exact loose resources or typed entries.</param>
    /// <param name="archives">Whole archive paths.</param>
    /// <param name="scope">Suggested directory or archive scope.</param>
    /// <param name="execute">Host executor with explicit force authorization.</param>
    /// <param name="knownPasswords">Known per-archive passwords.</param>
    /// <returns>Whether deletion completed.</returns>
    public static bool Show(IWin32Window owner, IReadOnlyList<AssetLocation> targets, IReadOnlyList<string> archives,
        string scope, Action<AssetDeletionPlan, bool> execute, IReadOnlyDictionary<string, string?>? knownPasswords = null)
    {
        var selectedScope = InputDialog.Show(owner, "Delete search scope", "Directory to inspect recursively, or one GAF/ZIP. References outside the inspected scope and game code are UNKNOWN:", scope);
        if (string.IsNullOrWhiteSpace(selectedScope)) return false;
        var credential = AssetOperationDialog.Credentials(owner, knownPasswords);
        try
        {
            var plan = AssetDeletionPlan.Create(selectedScope, targets, archives, credential);
            if (!AssetOperationDialog.Review(owner, "Delete preview — changes will be saved to disk", plan.Preview, plan.ForceRequired ? "Delete anyway…" : "Delete")) return false;
            if (plan.ForceRequired && ThemedMessageBox.Show(owner,
                    $"Explicitly break all {plan.BlockingReferences.Count} surviving relationships listed in the preview? Referencing documents remain unchanged. Dependents will NOT be deleted.",
                    "Authorize broken references", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return false;
            execute(plan, plan.ForceRequired);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { ThemedMessageBox.Show(owner, ex.Message, "Delete not completed", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
}
