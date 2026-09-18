using System.ComponentModel;
using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Assets.WinForms;

internal sealed class AssetEditorDocument : DockContent
{
    private const string AssetFileFilter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*";

    private readonly AssetsFile _assetsFile;
    private readonly Action _workspaceChanged;
    private readonly BindingList<AssetRecord> _records = new();

    private readonly DataGridView _grid;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripComboBox _typeComboBox;
    private readonly ToolStripTextBox _searchTextBox;

    private readonly ToolStripButton _saveButton;
    private readonly ToolStripButton _saveAsButton;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _addButton;
    private readonly ToolStripButton _replaceButton;
    private readonly ToolStripButton _renameButton;
    private readonly ToolStripButton _exportButton;
    private readonly ToolStripButton _deleteButton;

    public string FilePath => Path.GetFullPath(_assetsFile.FilePath);

    public AssetEditorDocument(AssetsFile assetsFile, Action workspaceChanged)
    {
        _assetsFile = assetsFile;
        _workspaceChanged = workspaceChanged;

        Text = Path.GetFileName(FilePath);
        TabText = Text;
        ToolTipText = FilePath;
        DockAreas = DockAreas.Document | DockAreas.Float;

        var fileTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        _saveButton = AddButton(fileTools, "Save", (_, _) => Save());
        _saveAsButton = AddButton(fileTools, "Save As…", (_, _) => SaveAs());
        fileTools.Items.Add(new ToolStripSeparator());
        _refreshButton = AddButton(fileTools, "Refresh", (_, _) => ReloadGrid());
        fileTools.Items.Add(new ToolStripSeparator());
        _addButton = AddButton(fileTools, "Add / Import", (_, _) => AddAsset());
        _replaceButton = AddButton(fileTools, "Replace", (_, _) => ReplaceSelectedAsset());
        _renameButton = AddButton(fileTools, "Rename", (_, _) => RenameSelectedAsset());
        _exportButton = AddButton(fileTools, "Export", (_, _) => ExportSelectedAsset());
        _deleteButton = AddButton(fileTools, "Delete", (_, _) => DeleteSelectedAsset());

        var filterTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        filterTools.Items.Add(new ToolStripLabel("Type:"));
        _typeComboBox = new ToolStripComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.Add("All Types");
        foreach (var value in Enum.GetValues<AssetTypes>())
            _typeComboBox.Items.Add(value);
        _typeComboBox.SelectedIndex = 0;
        _typeComboBox.SelectedIndexChanged += (_, _) => ReloadGrid();
        filterTools.Items.Add(_typeComboBox);

        filterTools.Items.Add(new ToolStripSeparator());
        filterTools.Items.Add(new ToolStripLabel("Search:"));
        _searchTextBox = new ToolStripTextBox
        {
            Width = 250,
            AutoSize = false
        };
        _searchTextBox.TextChanged += (_, _) => ReloadGrid();
        filterTools.Items.Add(_searchTextBox);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _records
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Type",
            DataPropertyName = nameof(AssetRecord.AssetType),
            Width = 120
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            DataPropertyName = nameof(AssetRecord.AssetName),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Size",
            DataPropertyName = nameof(AssetRecord.DisplaySize),
            Width = 120
        });

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ExportSelectedAsset();
        };
        _grid.SelectionChanged += (_, _) => UpdateUiState();

        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(_grid);
        Controls.Add(filterTools);
        Controls.Add(fileTools);
        Controls.Add(statusStrip);

        DarkTheme.Apply(this);
        _typeComboBox.BackColor = DarkTheme.Surface;
        _typeComboBox.ForeColor = DarkTheme.Foreground;
        _searchTextBox.BackColor = DarkTheme.Surface;
        _searchTextBox.ForeColor = DarkTheme.Foreground;

        ReloadGrid();
    }

    private static ToolStripButton AddButton(ToolStrip strip, string text, EventHandler onClick)
    {
        var button = new ToolStripButton(text);
        button.Click += onClick;
        strip.Items.Add(button);
        return button;
    }

    public void Save()
    {
        try
        {
            _assetsFile.Save();
            ReloadGrid();
            _workspaceChanged();
            SetStatus($"Saved: {FilePath}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save asset file.", ex);
        }
    }

    public void SaveAs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save Asset File As",
            Filter = AssetFileFilter,
            DefaultExt = Path.GetExtension(FilePath),
            InitialDirectory = Path.GetDirectoryName(FilePath),
            FileName = Path.GetFileName(FilePath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var encrypt = MessageBox.Show(
                this,
                "Enable password protection for the saved copy?",
                "Encryption",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            string? password = null;
            if (encrypt)
            {
                password = InputDialog.Show(this, "Password", "Enter password for the saved copy:");
                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show(
                        this,
                        "A password is required when encryption is enabled.",
                        "Password Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            using var copy = AssetsFile.LoadOrCreate(dialog.FileName, password, encrypt);
            foreach (var entry in _assetsFile.GetAllEntries())
            {
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is not null)
                    copy.Add(entry.AssetType, entry.AssetName, stream);
            }

            copy.Save();
            _workspaceChanged();
            SetStatus($"Saved copy: {Path.GetFullPath(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save copy of asset file.", ex);
        }
    }

    private void AddAsset()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import Asset",
            Multiselect = true,
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        using var typePicker = new AssetTypePickerForm();
        if (typePicker.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var imported = 0;
            foreach (var file in dialog.FileNames)
            {
                var customName = InputDialog.Show(
                    this,
                    "Asset Name",
                    $"Enter asset name for '{Path.GetFileName(file)}' (leave as-is to keep original file name):",
                    Path.GetFileName(file));

                if (string.IsNullOrWhiteSpace(customName))
                    continue;

                _assetsFile.Add(typePicker.SelectedType, file, customName);
                imported++;
            }

            ReloadGrid();
            SetStatus($"Imported {imported} asset(s).");
        }
        catch (Exception ex)
        {
            ShowError("Failed to import one or more assets.", ex);
        }
    }

    private void ReplaceSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = $"Replace '{selected.AssetName}'",
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            _assetsFile.Add(selected.AssetType, selected.AssetName, stream);
            ReloadGrid();
            SetStatus($"Replaced: {selected.AssetName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to replace asset.", ex);
        }
    }

    private void RenameSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        var newName = InputDialog.Show(this, "Rename Asset", "Enter new asset name:", selected.AssetName);
        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(newName, selected.AssetName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var stream = _assetsFile[selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                MessageBox.Show(
                    this,
                    "The selected asset could not be read.",
                    "Read Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _assetsFile.Add(selected.AssetType, newName, stream);
            _assetsFile.Remove(selected.AssetType, selected.AssetName);
            ReloadGrid();
            SetStatus($"Renamed '{selected.AssetName}' to '{newName}'.");
        }
        catch (Exception ex)
        {
            ShowError("Failed to rename asset.", ex);
        }
    }

    private void ExportSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        using var dialog = new SaveFileDialog
        {
            Title = $"Export '{selected.AssetName}'",
            FileName = selected.AssetName,
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var stream = _assetsFile[selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                MessageBox.Show(
                    this,
                    "The selected asset could not be read.",
                    "Read Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var fileStream = File.Create(dialog.FileName);
            stream.CopyTo(fileStream);
            SetStatus($"Exported: {selected.AssetName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to export asset.", ex);
        }
    }

    private void DeleteSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        if (MessageBox.Show(
                this,
                $"Delete '{selected.AssetName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _assetsFile.Remove(selected.AssetType, selected.AssetName);
            ReloadGrid();
            SetStatus($"Deleted: {selected.AssetName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to delete asset.", ex);
        }
    }

    private void ReloadGrid()
    {
        _records.Clear();

        try
        {
            var selectedType = _typeComboBox.SelectedItem;
            var search = _searchTextBox.Text.Trim();

            foreach (var entry in _assetsFile.GetAllEntries()
                         .OrderBy(e => e.AssetType)
                         .ThenBy(e => e.AssetName))
            {
                if (selectedType is AssetTypes assetType && entry.AssetType != assetType)
                    continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    entry.AssetName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                long size = 0;
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is not null && stream.CanSeek)
                {
                    size = stream.Length;
                }
                else if (stream is not null)
                {
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    size = buffer.Length;
                }

                _records.Add(new AssetRecord
                {
                    AssetType = entry.AssetType,
                    AssetName = entry.AssetName,
                    SizeBytes = size
                });
            }

            SetStatus($"{FilePath} ({_records.Count} asset(s))");
        }
        catch (Exception ex)
        {
            ShowError("Failed to load asset entries.", ex);
        }

        UpdateUiState();
    }

    private AssetRecord? GetSelectedRecord() =>
        _grid.CurrentRow?.DataBoundItem as AssetRecord;

    private void UpdateUiState()
    {
        var hasSelection = GetSelectedRecord() is not null;

        _saveButton.Enabled = true;
        _saveAsButton.Enabled = true;
        _refreshButton.Enabled = true;
        _addButton.Enabled = true;
        _replaceButton.Enabled = hasSelection;
        _renameButton.Enabled = hasSelection;
        _exportButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(
            this,
            message + Environment.NewLine + Environment.NewLine + ex.Message,
            "AssetFiles Editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        SetStatus(message);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _assetsFile.Dispose();
        base.OnFormClosed(e);
    }
}
