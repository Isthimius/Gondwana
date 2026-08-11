using System.ComponentModel;
using Gondwana.Assets;

namespace Gondwana.Tooling.Assets.WinForms;

public sealed partial class MainForm : Form
{
    private AssetsFile? _assetsFile;
    private readonly BindingList<AssetRecord> _records = new();

    private readonly DataGridView _grid;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ComboBox _typeComboBox;
    private readonly TextBox _searchTextBox;

    private readonly Button _newButton;
    private readonly Button _openButton;
    private readonly Button _saveButton;
    private readonly Button _saveAsButton;
    private readonly Button _refreshButton;

    private readonly Button _addButton;
    private readonly Button _replaceButton;
    private readonly Button _renameButton;
    private readonly Button _exportButton;
    private readonly Button _deleteButton;

    public MainForm()
    {
        Text = "AssetFiles Editor";
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8),
            AutoSize = false,
            WrapContents = false
        };

        _newButton = CreateButton("New", (_, _) => CreateNewAssetsFile());
        _openButton = CreateButton("Open", (_, _) => OpenAssetsFile());
        _saveButton = CreateButton("Save", (_, _) => SaveAssetsFile());
        _saveAsButton = CreateButton("Save As", (_, _) => SaveAssetsFileAs());
        _refreshButton = CreateButton("Refresh", (_, _) => ReloadGrid());

        topPanel.Controls.AddRange([
            _newButton,
            _openButton,
            _saveButton,
            _saveAsButton,
            _refreshButton
        ]);

        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8),
            AutoSize = false,
            WrapContents = false
        };

        _typeComboBox = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.Add("All Types");
        foreach (var value in Enum.GetValues<AssetTypes>())
            _typeComboBox.Items.Add(value);
        _typeComboBox.SelectedIndex = 0;
        _typeComboBox.SelectedIndexChanged += (_, _) => ReloadGrid();

        _searchTextBox = new TextBox
        {
            Width = 250,
            PlaceholderText = "Filter by asset name..."
        };
        _searchTextBox.TextChanged += (_, _) => ReloadGrid();

        _addButton = CreateButton("Add / Import", (_, _) => AddAsset());
        _replaceButton = CreateButton("Replace", (_, _) => ReplaceSelectedAsset());
        _renameButton = CreateButton("Rename", (_, _) => RenameSelectedAsset());
        _exportButton = CreateButton("Export", (_, _) => ExportSelectedAsset());
        _deleteButton = CreateButton("Delete", (_, _) => DeleteSelectedAsset());

        filterPanel.Controls.Add(new Label { Text = "Type:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        filterPanel.Controls.Add(_typeComboBox);
        filterPanel.Controls.Add(new Label { Text = "Search:", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filterPanel.Controls.Add(_searchTextBox);
        filterPanel.Controls.Add(_addButton);
        filterPanel.Controls.Add(_replaceButton);
        filterPanel.Controls.Add(_renameButton);
        filterPanel.Controls.Add(_exportButton);
        filterPanel.Controls.Add(_deleteButton);

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

        _grid.CellDoubleClick += (_, _) => ExportSelectedAsset();
        _grid.SelectionChanged += (_, _) => UpdateUiState();

        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("No asset file loaded.");
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(_grid);
        Controls.Add(filterPanel);
        Controls.Add(topPanel);
        Controls.Add(statusStrip);

        UpdateUiState();
    }

    private Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(4, 0, 4, 0)
        };
        button.Click += onClick;
        return button;
    }

    private void CreateNewAssetsFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Create Asset File",
            Filter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*",
            DefaultExt = "gaf"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var encrypt = MessageBox.Show(this,
            "Enable password protection for this asset file?",
            "Encryption",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        string? password = null;
        if (encrypt)
        {
            password = InputDialog.Show(this, "Password", "Enter password for the new asset file:");
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(this, "A password is required when encryption is enabled.", "Password Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        try
        {
            _assetsFile?.Dispose();
            _assetsFile = AssetsFile.LoadOrCreate(dialog.FileName, password, encrypt);
            _assetsFile.Save();
            ReloadGrid();
            SetStatus($"Created: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to create asset file.", ex);
        }
    }

    private void OpenAssetsFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open Asset File",
            Filter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _assetsFile?.Dispose();

            try
            {
                _assetsFile = AssetsFile.LoadOrCreate(dialog.FileName);
                _ = _assetsFile.GetAllEntries().ToList();
            }
            catch
            {
                var password = InputDialog.Show(this, "Password", "Enter password for this asset file:");
                _assetsFile?.Dispose();
                _assetsFile = AssetsFile.LoadOrCreate(dialog.FileName, password, encrypt: true);
                _ = _assetsFile.GetAllEntries().ToList();
            }

            ReloadGrid();
            SetStatus($"Opened: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to open asset file.", ex);
        }
    }

    private void SaveAssetsFile()
    {
        if (!EnsureAssetsFileLoaded())
            return;

        try
        {
            _assetsFile!.Save();
            ReloadGrid();
            SetStatus($"Saved: {_assetsFile.FilePath}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save asset file.", ex);
        }
    }

    private void SaveAssetsFileAs()
    {
        if (!EnsureAssetsFileLoaded())
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Save Asset File As",
            Filter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*",
            DefaultExt = Path.GetExtension(_assetsFile!.FilePath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var encrypt = MessageBox.Show(this,
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
                    MessageBox.Show(this, "A password is required when encryption is enabled.", "Password Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var copy = AssetsFile.LoadOrCreate(dialog.FileName, password, encrypt);
            foreach (var entry in _assetsFile.GetAllEntries())
            {
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is null)
                    continue;

                copy.Add(entry.AssetType, entry.AssetName, stream);
            }

            copy.Save();
            copy.Dispose();
            SetStatus($"Saved copy: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save copy of asset file.", ex);
        }
    }

    private void AddAsset()
    {
        if (!EnsureAssetsFileLoaded())
            return;

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
            foreach (var file in dialog.FileNames)
            {
                var customName = InputDialog.Show(this,
                    "Asset Name",
                    $"Enter asset name for '{Path.GetFileName(file)}' (leave as-is to keep original file name):",
                    Path.GetFileName(file));

                if (string.IsNullOrWhiteSpace(customName))
                    continue;

                _assetsFile!.Add(typePicker.SelectedType, file, customName);
            }

            ReloadGrid();
            SetStatus($"Imported {dialog.FileNames.Length} asset(s).");
        }
        catch (Exception ex)
        {
            ShowError("Failed to import one or more assets.", ex);
        }
    }

    private void ReplaceSelectedAsset()
    {
        if (!EnsureAssetsFileLoaded())
            return;

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
            _assetsFile!.Add(selected.AssetType, selected.AssetName, stream);
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
        if (!EnsureAssetsFileLoaded())
            return;

        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        var newName = InputDialog.Show(this, "Rename Asset", "Enter new asset name:", selected.AssetName);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, selected.AssetName, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                MessageBox.Show(this, "The selected asset could not be read.", "Read Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (!EnsureAssetsFileLoaded())
            return;

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
            using var stream = _assetsFile![selected.AssetType, selected.AssetName];
            if (stream is null)
            {
                MessageBox.Show(this, "The selected asset could not be read.", "Read Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (!EnsureAssetsFileLoaded())
            return;

        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        if (MessageBox.Show(this,
                $"Delete '{selected.AssetName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            _assetsFile!.Remove(selected.AssetType, selected.AssetName);
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

        if (_assetsFile is null)
        {
            UpdateUiState();
            return;
        }

        try
        {
            var entries = _assetsFile.GetAllEntries();
            var selectedType = _typeComboBox.SelectedItem;
            var search = _searchTextBox.Text.Trim();

            foreach (var entry in entries.OrderBy(e => e.AssetType).ThenBy(e => e.AssetName))
            {
                if (selectedType is AssetTypes assetType && entry.AssetType != assetType)
                    continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    entry.AssetName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                long size = 0;
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is not null && stream.CanSeek)
                    size = stream.Length;
                else if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    size = ms.Length;
                }

                _records.Add(new AssetRecord
                {
                    AssetType = entry.AssetType,
                    AssetName = entry.AssetName,
                    SizeBytes = size
                });
            }

            SetStatus(_assetsFile.FilePath + $" ({_records.Count} asset(s))");
        }
        catch (Exception ex)
        {
            ShowError("Failed to load asset entries.", ex);
        }

        UpdateUiState();
    }

    private AssetRecord? GetSelectedRecord()
    {
        return _grid.CurrentRow?.DataBoundItem as AssetRecord;
    }

    private bool EnsureAssetsFileLoaded()
    {
        if (_assetsFile is not null)
            return true;

        MessageBox.Show(this, "Open or create an asset file first.", "No Asset File",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private void UpdateUiState()
    {
        var hasFile = _assetsFile is not null;
        var hasSelection = GetSelectedRecord() is not null;

        _saveButton.Enabled = hasFile;
        _saveAsButton.Enabled = hasFile;
        _refreshButton.Enabled = hasFile;
        _addButton.Enabled = hasFile;
        _replaceButton.Enabled = hasFile && hasSelection;
        _renameButton.Enabled = hasFile && hasSelection;
        _exportButton.Enabled = hasFile && hasSelection;
        _deleteButton.Enabled = hasFile && hasSelection;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, message + Environment.NewLine + Environment.NewLine + ex.Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        SetStatus(message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _assetsFile?.Dispose();
        base.OnFormClosing(e);
    }
}
