using Gondwana.Assets;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.WinForms.Panels;

/// <summary>
/// Panel for managing a Gondwana asset file (.gaf).
/// When WeifenLuo.WinFormsUI.DockPanel is added, this can be made a DockContent.
/// </summary>
public sealed class AssetFilesPanel : UserControl
{
    private readonly AssetFilesViewModel _vm;
    private readonly DataGridView _grid;
    private readonly ComboBox _typeFilter;
    private readonly TextBox _searchBox;
    private readonly Label _statusLabel;

    /// <summary>
    /// AssetFilesPanel.
    /// </summary>
    /// <param name="vm">ViewModel.</param>
    public AssetFilesPanel(AssetFilesViewModel vm)
    {
        _vm = vm;

        var toolbar = new ToolStrip();
        var newBtn = new ToolStripButton("New…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var openBtn = new ToolStripButton("Open…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveBtn = new ToolStripButton("Save") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveAsBtn = new ToolStripButton("Save As…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var addBtn = new ToolStripButton("Add…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var replaceBtn = new ToolStripButton("Replace…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var renameBtn = new ToolStripButton("Rename…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var exportBtn = new ToolStripButton("Export…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var deleteBtn = new ToolStripButton("Delete") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var refreshBtn = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        newBtn.Click += async (_, _) => await _vm.NewCommand.ExecuteAsync(null);
        openBtn.Click += async (_, _) => await _vm.OpenCommand.ExecuteAsync(null);
        saveBtn.Click += async (_, _) => await _vm.SaveCommand.ExecuteAsync(null);
        saveAsBtn.Click += async (_, _) => await _vm.SaveAsCommand.ExecuteAsync(null);
        addBtn.Click += async (_, _) => await _vm.AddCommand.ExecuteAsync(null);
        replaceBtn.Click += async (_, _) => await _vm.ReplaceCommand.ExecuteAsync(null);
        renameBtn.Click += async (_, _) => await _vm.RenameCommand.ExecuteAsync(null);
        exportBtn.Click += async (_, _) => await _vm.ExportCommand.ExecuteAsync(null);
        deleteBtn.Click += async (_, _) => await _vm.DeleteCommand.ExecuteAsync(null);
        refreshBtn.Click += (_, _) => _vm.RefreshCommand.Execute(null);

        toolbar.Items.AddRange([
            newBtn, openBtn, saveBtn, saveAsBtn, new ToolStripSeparator(),
            addBtn, replaceBtn, renameBtn, exportBtn, deleteBtn, new ToolStripSeparator(),
            refreshBtn
        ]);

        var filterBar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(4, 4, 4, 0) };
        var filterLabel = new Label { Text = "Type:", AutoSize = true, Location = new System.Drawing.Point(4, 6) };
        _typeFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(44, 2),
            Width = 120
        };
        foreach (var item in _vm.TypeFilterItems)
            _typeFilter.Items.Add(item);
        _typeFilter.SelectedIndex = 0;
        _typeFilter.SelectedIndexChanged += (_, _) => _vm.SelectedTypeIndex = _typeFilter.SelectedIndex;

        var searchLabel = new Label { Text = "Search:", AutoSize = true, Location = new System.Drawing.Point(175, 6) };
        _searchBox = new TextBox { Location = new System.Drawing.Point(224, 3), Width = 200 };
        _searchBox.TextChanged += (_, _) => _vm.SearchText = _searchBox.Text;

        filterBar.Controls.AddRange([filterLabel, _typeFilter, searchLabel, _searchBox]);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            DataSource = _vm.Records
        };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "AssetName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "AssetType", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "DisplaySize", Width = 80 }
        );
        _grid.SelectionChanged += OnGridSelectionChanged;

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 22,
            Text = _vm.StatusText
        };

        Controls.Add(_grid);
        Controls.Add(filterBar);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].DataBoundItem is AssetRecordViewModel record)
            _vm.SelectedRecord = record;
        else
            _vm.SelectedRecord = null;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnVmPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(AssetFilesViewModel.StatusText))
            _statusLabel.Text = _vm.StatusText;

        if (e.PropertyName == nameof(AssetFilesViewModel.FilePath) && !string.IsNullOrWhiteSpace(_vm.FilePath))
            Text = $"Assets — {System.IO.Path.GetFileName(_vm.FilePath)}";
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">disposing.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.Dispose();
        }
        base.Dispose(disposing);
    }
}
