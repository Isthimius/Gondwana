using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.WinForms;

/// <summary>Shared dependency picker, linked into standalone controls and reused by Studio.</summary>
internal static class PackedResourcePicker
{
    internal static AssetPackageCatalog CreateCatalog(IWin32Window owner)
    {
        var catalog = new AssetPackageCatalog();
        var credentials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        catalog.PasswordAccepted += (path, password) => credentials[path] = password;
        catalog.PasswordProvider = path =>
        {
            if (credentials.Remove(path, out var password)) return password;
            using var dialog = new Form
            {
                Text = "Asset password",
                Width = 480,
                Height = 170,
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            };
            var label = new Label { Text = $"Password for {Path.GetFileName(path)}", Dock = DockStyle.Top, AutoSize = true };
            var input = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom };
            dialog.Controls.AddRange([input, label, cancel, ok]);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;
            ToolingAppearance.Apply(dialog);
            return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
        };
        return catalog;
    }

    internal static void PickAndApply(IWin32Window owner, AssetPackageCatalog catalog, string directory,
        AssetTypes type, Action<PackedResourceSelection> apply)
    {
        using var open = new OpenFileDialog
        {
            Title = $"Choose package containing {type}",
            Filter = "Gondwana asset packages|*.gaf;*.zip|All files|*.*",
            InitialDirectory = directory
        };
        if (open.ShowDialog(owner) != DialogResult.OK) return;
        using var dialog = CreateDialog(catalog, open.FileName, type, apply);
        dialog.ShowDialog(owner);
    }

    internal static Form CreateDialog(AssetPackageCatalog catalog, string archivePath,
        AssetTypes type, Action<PackedResourceSelection> apply)
    {
        var dialog = new Form
        {
            Text = $"Choose packed {type} — {archivePath}",
            Size = new Size(720, 520),
            MinimumSize = new Size(480, 350),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };
        var entries = new ListBox { Name = "entries", Dock = DockStyle.Fill, HorizontalScrollbar = true, DisplayMember = nameof(AuthoringResource.EntryName) };
        var members = new ListBox { Name = "members", Dock = DockStyle.Bottom, Height = 130, HorizontalScrollbar = true };
        var status = new TextBox { Name = "status", Dock = DockStyle.Bottom, Height = 65, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        var extension = new TextBox { Dock = DockStyle.Bottom, PlaceholderText = "Audio source extension (required for extensionless entries, e.g. .wav)", Visible = type == AssetTypes.Audio };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Name = "add", Text = "Add", AutoSize = true, Enabled = false };
        var reload = new Button { Name = "reload", Text = "Reload / retry", AutoSize = true };
        buttons.Controls.AddRange([cancel, ok, reload]);
        dialog.Controls.AddRange([entries, members, extension, status, buttons]);
        dialog.CancelButton = cancel;
        dialog.AcceptButton = ok;
        void LoadEntries()
        {
            entries.Items.Clear();
            members.Items.Clear();
            ok.Enabled = false;
            try
            {
                entries.Items.AddRange(catalog.GetResources(archivePath, type).Cast<object>().ToArray());
                status.Text = entries.Items.Count == 0 ? $"No {type} entries in this archive." : "Select an exact entry, then a logical member for definitions.";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        entries.SelectedIndexChanged += (_, _) =>
        {
            members.Items.Clear();
            ok.Enabled = false;
            if (entries.SelectedItem is not AuthoringResource resource) return;
            try
            {
                members.Items.AddRange(PackedResourceSelection.GetMembers(catalog, resource).Cast<object>().ToArray());
                bool media = type is AssetTypes.Audio or AssetTypes.Image;
                members.Visible = !media;
                ok.Enabled = media;
                if (members.Items.Count == 1) members.SelectedIndex = 0;
                status.Text = media ? resource.EntryName : "Select the logical definition to add.";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        members.SelectedIndexChanged += (_, _) => ok.Enabled = members.SelectedItem is string;
        reload.Click += (_, _) => { catalog.Invalidate(archivePath); LoadEntries(); };
        ok.Click += (_, _) =>
        {
            if (entries.SelectedItem is not AuthoringResource resource) return;
            try
            {
                var selection = new PackedResourceSelection(resource, members.SelectedItem as string,
                    string.IsNullOrWhiteSpace(extension.Text) ? null : extension.Text.Trim());
                selection.Validate(catalog, type);
                apply(selection);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        ToolingAppearance.Apply(dialog);
        LoadEntries();
        return dialog;
    }
}
