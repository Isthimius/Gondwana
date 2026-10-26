using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>Reviews per-file types and destination names, and selects a dependency policy.</summary>
internal sealed class AssetTypePickerForm : Form
{
    private readonly DataGridView _files = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    /// <summary>Gets whether known dependencies will be copied recursively.</summary>
    public bool ImportDependencies { get; private set; }
    /// <summary>Gets reviewed source files, types, and exact entry names.</summary>
    public IReadOnlyList<AssetImportSource> Sources => _files.Rows.Cast<DataGridViewRow>().Select(row =>
        new AssetImportSource((string)row.Tag!, (AssetTypes)row.Cells[1].Value, Convert.ToString(row.Cells[2].Value) ?? string.Empty)).ToArray();

    /// <summary>Creates a mixed-type file review dialog.</summary>
    /// <param name="filePaths">Selected loose files.</param>
    public AssetTypePickerForm(IEnumerable<string> filePaths)
    {
        Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon();
        Text = "Review asset import";
        Size = new Size(850, 430);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        _files.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source file", ReadOnly = true });
        _files.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Asset type", DataSource = Enum.GetValues<AssetTypes>() });
        _files.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Destination entry" });
        foreach (var file in filePaths)
        {
            var type = AssetImportPlan.InferType(file);
            var row = _files.Rows[_files.Rows.Add(file, type, Path.GetFileName(file))];
            row.Tag = file;
            row.Cells[1].ReadOnly = type is AssetTypes.TilesheetDefinition or AssetTypes.AnimationDefinition or AssetTypes.SceneDefinition or AssetTypes.SpriteDefinition or AssetTypes.AudioDefinition;
        }
        var description = new Label { Dock = DockStyle.Top, Height = 64, Text = "Review types and entry names. Native definition types are fixed.\nImport dependencies copies known referenced files recursively. Keep external preserves their locations.\nThe next step previews all changes. Original files are never moved or changed.", Padding = new Padding(8) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var external = new Button { Text = "Keep external references", AutoSize = true };
        var dependencies = new Button { Text = "Import dependencies", AutoSize = true };
        external.Click += (_, _) => Complete(false);
        dependencies.Click += (_, _) => Complete(true);
        buttons.Controls.AddRange([cancel, external, dependencies]);
        Controls.Add(_files);
        Controls.Add(description);
        Controls.Add(buttons);
        CancelButton = cancel;
        ToolingAppearance.Apply(this);
    }

    private void Complete(bool dependencies)
    {
        _files.EndEdit();
        ImportDependencies = dependencies;
        DialogResult = DialogResult.OK;
    }
}
