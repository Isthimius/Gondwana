using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.WinForms.Panels;

/// <summary>
/// Panel for the tilesheet editor.
/// Hosts a PictureBox for the tilesheet image preview and a DataGridView for tile cells.
/// When WeifenLuo.WinFormsUI.DockPanel is added, this can be made a DockContent.
/// </summary>
public sealed class TilesheetEditorPanel : UserControl
{
    private readonly TilesheetEditorViewModelBase _vm;
    private readonly PictureBox _preview;
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;

    /// <summary>
    /// TilesheetEditorPanel.
    /// </summary>
    /// <param name="vm">ViewModel.</param>
    public TilesheetEditorPanel(TilesheetEditorViewModelBase vm)
    {
        _vm = vm;

        var toolbar = new ToolStrip();
        var openImageBtn = new ToolStripButton("Open Image…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var rebuildBtn = new ToolStripButton("Rebuild Grid") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveBtn = new ToolStripButton("Save…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        openImageBtn.Click += async (_, _) => await _vm.OpenImageCommand.ExecuteAsync(null);
        rebuildBtn.Click += (_, _) => _vm.RebuildGridCommand.Execute(null);
        saveBtn.Click += async (_, _) => await _vm.SaveCommand.ExecuteAsync(null);
        toolbar.Items.AddRange([openImageBtn, new ToolStripSeparator(), rebuildBtn, new ToolStripSeparator(), saveBtn]);

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 22,
            Text = _vm.StatusText
        };

        _preview = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 256,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = System.Drawing.Color.FromArgb(40, 40, 40)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false
        };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Index", DataPropertyName = "Index", Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "X", DataPropertyName = "X", Width = 45 },
            new DataGridViewTextBoxColumn { HeaderText = "Y", DataPropertyName = "Y", Width = 45 }
        );
        _grid.DataSource = _vm.TileCells;
        _grid.SelectionChanged += OnGridSelectionChanged;

        var contentPanel = new Panel { Dock = DockStyle.Fill };
        contentPanel.Controls.Add(_grid);
        contentPanel.Controls.Add(_preview);

        Controls.Add(contentPanel);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].DataBoundItem is TileCellViewModel tile)
            _vm.SelectedTile = tile;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnVmPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(TilesheetEditorViewModelBase.StatusText))
            _statusLabel.Text = _vm.StatusText;

        if (e.PropertyName == nameof(TilesheetEditorViewModelBase.ImagePath)
            && !string.IsNullOrWhiteSpace(_vm.ImagePath))
        {
            _preview.ImageLocation = _vm.ImagePath;
            Text = $"Tilesheet — {System.IO.Path.GetFileName(_vm.ImagePath)}";
        }
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
            _preview.Dispose();
        }
        base.Dispose(disposing);
    }
}
