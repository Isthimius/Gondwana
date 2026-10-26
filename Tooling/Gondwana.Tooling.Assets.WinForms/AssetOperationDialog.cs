namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>Common review and credential prompts for physical asset operations.</summary>
internal static class AssetOperationDialog
{
    /// <summary>Creates a per-operation credential provider without sharing passwords between archives.</summary>
    /// <param name="owner">Dialog owner.</param>
    /// <param name="knownPasswords">Credentials already supplied by the host.</param>
    /// <returns>A provider that cancels the operation when its prompt is dismissed.</returns>
    internal static Func<string, string?> Credentials(IWin32Window owner, IReadOnlyDictionary<string, string?>? knownPasswords)
    {
        var passwords = new Dictionary<string, string?>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (knownPasswords is not null)
            foreach (var pair in knownPasswords) passwords.Add(Path.GetFullPath(pair.Key), pair.Value);
        return path =>
        {
            path = Path.GetFullPath(path);
            if (passwords.TryGetValue(path, out var password)) return password;
            password = InputDialog.Show(owner, "Archive credential", $"Password for {path} (empty for none):", password: true);
            if (password is null) throw new OperationCanceledException();
            passwords[path] = string.IsNullOrEmpty(password) ? null : password;
            return passwords[path];
        };
    }

    /// <summary>Displays a read-only preview; cancellation is the default action.</summary>
    /// <param name="owner">Dialog owner.</param>
    /// <param name="title">Operation and persistence information.</param>
    /// <param name="lines">Prepared plan details.</param>
    /// <param name="action">Explicit commit button label.</param>
    /// <returns>Whether the user chose the commit button.</returns>
    internal static bool Review(IWin32Window owner, string title, IEnumerable<string> lines, string action)
    {
        using var preview = new Form { Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon(), Text = title, Size = new Size(950, 550), StartPosition = FormStartPosition.CenterParent };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = string.Join(Environment.NewLine, lines) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var commit = new Button { Text = action, AutoSize = true, DialogResult = DialogResult.OK };
        buttons.Controls.AddRange([cancel, commit]);
        preview.Controls.Add(text);
        preview.Controls.Add(buttons);
        preview.CancelButton = cancel;
        preview.AcceptButton = cancel;
        DarkTheme.Apply(preview);
        return preview.ShowDialog(owner) == DialogResult.OK;
    }
}
