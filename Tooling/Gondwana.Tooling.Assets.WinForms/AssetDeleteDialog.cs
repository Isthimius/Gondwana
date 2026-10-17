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
        var passwords = new Dictionary<string, string?>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (knownPasswords is not null) foreach (var pair in knownPasswords) passwords.Add(pair.Key, pair.Value);
        string? Credential(string path)
        {
            if (passwords.TryGetValue(path, out var password)) return password;
            password = InputDialog.Show(owner, "Archive credential", $"Password for {path} (empty for none):", password: true);
            if (password is null) throw new OperationCanceledException();
            passwords[path] = string.IsNullOrEmpty(password) ? null : password;
            return passwords[path];
        }
        try
        {
            var plan = AssetDeletionPlan.Create(selectedScope, targets, archives, Credential);
            using var preview = new Form { Text = "Delete preview — changes will be saved to disk", Size = new Size(950, 550), StartPosition = FormStartPosition.CenterParent };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = string.Join(Environment.NewLine, plan.Preview) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var commit = new Button { Text = plan.ForceRequired ? "Delete anyway…" : "Delete", AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.AddRange([cancel, commit]);
            preview.Controls.Add(text);
            preview.Controls.Add(buttons);
            preview.CancelButton = cancel;
            DarkTheme.Apply(preview);
            if (preview.ShowDialog(owner) != DialogResult.OK) return false;
            if (plan.ForceRequired && MessageBox.Show(owner,
                    $"Explicitly break all {plan.BlockingReferences.Count} surviving relationships listed in the preview? Referencing documents remain unchanged. Dependents will NOT be deleted.",
                    "Authorize broken references", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return false;
            execute(plan, plan.ForceRequired);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Delete not completed", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
}
