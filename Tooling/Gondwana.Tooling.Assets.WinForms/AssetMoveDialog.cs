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
        var selectedScope = InputDialog.Show(owner, "Search scope", "Directory to scan recursively, or one GAF. References outside this scope and game code will NOT be updated:", scope);
        if (string.IsNullOrWhiteSpace(selectedScope)) return false;
        var passwords = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
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
            var plan = wholeArchive
                ? AssetRelocationPlan.MoveArchive(source.Path, destination, selectedScope, Credential)
                : AssetRelocationPlan.Create(selectedScope, [new(source, new(source.AssetType, Path.GetFullPath(destination), entry))], Credential);
            using var preview = new Form { Text = "Move preview — changes will be saved to disk", Size = new Size(950, 550), StartPosition = FormStartPosition.CenterParent };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = string.Join(Environment.NewLine, plan.Preview) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var commit = new Button { Text = "Move", DialogResult = DialogResult.OK };
            buttons.Controls.AddRange([cancel, commit]);
            preview.Controls.Add(text);
            preview.Controls.Add(buttons);
            preview.CancelButton = cancel;
            DarkTheme.Apply(preview);
            if (preview.ShowDialog(owner) != DialogResult.OK) return false;
            execute(plan);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Move not completed", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
}
