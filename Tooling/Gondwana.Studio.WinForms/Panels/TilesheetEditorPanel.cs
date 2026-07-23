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
    private readonly TextBox _tileNameTextBox;

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
        var gtsSettingsBtn = new ToolStripButton("GTS Settings…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveBtn = new ToolStripButton("Save…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        openImageBtn.Click += async (_, _) => await _vm.OpenImageCommand.ExecuteAsync(null);
        rebuildBtn.Click += (_, _) => _vm.RebuildGridCommand.Execute(null);
        gtsSettingsBtn.Click += (_, _) => ShowGtsSettingsDialog();
        saveBtn.Click += async (_, _) => await _vm.SaveCommand.ExecuteAsync(null);
        toolbar.Items.AddRange([openImageBtn, new ToolStripSeparator(), rebuildBtn, gtsSettingsBtn, new ToolStripSeparator(), saveBtn]);

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

        var editPanel = new Panel { Dock = DockStyle.Right, Width = 240, Padding = new Padding(8) };
        var tileNameLabel = new Label { Dock = DockStyle.Top, Height = 20, Text = "Selected Tile Name" };
        _tileNameTextBox = new TextBox { Dock = DockStyle.Top };
        var applyNameBtn = new Button { Dock = DockStyle.Top, Height = 28, Text = "Apply Name" };
        applyNameBtn.Click += (_, _) =>
        {
            _vm.SelectedTileName = _tileNameTextBox.Text;
            _vm.ApplyTileNameCommand.Execute(null);
        };
        editPanel.Controls.Add(applyNameBtn);
        editPanel.Controls.Add(_tileNameTextBox);
        editPanel.Controls.Add(tileNameLabel);

        var contentPanel = new Panel { Dock = DockStyle.Fill };
        contentPanel.Controls.Add(_grid);
        contentPanel.Controls.Add(_preview);
        contentPanel.Controls.Add(editPanel);

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
        else if (e.PropertyName == nameof(TilesheetEditorViewModelBase.SelectedTileName))
            _tileNameTextBox.Text = _vm.SelectedTileName;

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

    private void ShowGtsSettingsDialog()
    {
        using var dialog = new GtsSettingsDialog(_vm);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        dialog.ApplyTo(_vm);
        if (_vm.RebuildGridCommand.CanExecute(null))
            _vm.RebuildGridCommand.Execute(null);
    }
}

file sealed class GtsSettingsDialog : Form
{
    private readonly NumericUpDown _tileWidth;
    private readonly NumericUpDown _tileHeight;
    private readonly NumericUpDown _regionX;
    private readonly NumericUpDown _regionY;
    private readonly NumericUpDown _regionWidth;
    private readonly NumericUpDown _regionHeight;
    private readonly NumericUpDown _paddingLeft;
    private readonly NumericUpDown _paddingTop;
    private readonly NumericUpDown _paddingRight;
    private readonly NumericUpDown _paddingBottom;
    private readonly NumericUpDown _marginLeft;
    private readonly NumericUpDown _marginTop;
    private readonly NumericUpDown _marginRight;
    private readonly NumericUpDown _marginBottom;
    private readonly NumericUpDown _overhangLeft;
    private readonly NumericUpDown _overhangTop;
    private readonly NumericUpDown _overhangRight;
    private readonly NumericUpDown _overhangBottom;
    private readonly CheckBox _premultiplyAlpha;

    public GtsSettingsDialog(TilesheetEditorViewModelBase vm)
    {
        Text = "GTS Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 520;
        Height = 560;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _tileWidth = AddNumber(table, "Tile Width", vm.TileWidth, 0);
        _tileHeight = AddNumber(table, "Tile Height", vm.TileHeight, 1);
        _regionX = AddNumber(table, "Region X", vm.RegionX, 2);
        _regionY = AddNumber(table, "Region Y", vm.RegionY, 3);
        _regionWidth = AddNumber(table, "Region Width (0=auto)", vm.RegionWidth, 4);
        _regionHeight = AddNumber(table, "Region Height (0=auto)", vm.RegionHeight, 5);

        _paddingLeft = AddNumber(table, "Padding Left", vm.TilePaddingLeft, 6);
        _paddingTop = AddNumber(table, "Padding Top", vm.TilePaddingTop, 7);
        _paddingRight = AddNumber(table, "Padding Right", vm.TilePaddingRight, 8);
        _paddingBottom = AddNumber(table, "Padding Bottom", vm.TilePaddingBottom, 9);

        _marginLeft = AddNumber(table, "Margin Left", vm.RegionMarginLeft, 10);
        _marginTop = AddNumber(table, "Margin Top", vm.RegionMarginTop, 11);
        _marginRight = AddNumber(table, "Margin Right", vm.RegionMarginRight, 12);
        _marginBottom = AddNumber(table, "Margin Bottom", vm.RegionMarginBottom, 13);

        _overhangLeft = AddNumber(table, "Overhang Left", vm.OverhangLeft, 14, allowNegative: true);
        _overhangTop = AddNumber(table, "Overhang Top", vm.OverhangTop, 15, allowNegative: true);
        _overhangRight = AddNumber(table, "Overhang Right", vm.OverhangRight, 16, allowNegative: true);
        _overhangBottom = AddNumber(table, "Overhang Bottom", vm.OverhangBottom, 17, allowNegative: true);

        var premultiplyLabel = new Label { Text = "Premultiply Alpha", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        _premultiplyAlpha = new CheckBox { Checked = vm.PremultiplyAlpha, Dock = DockStyle.Left };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(premultiplyLabel, 0, 18);
        table.SetColumnSpan(premultiplyLabel, 3);
        table.Controls.Add(_premultiplyAlpha, 3, 18);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        buttonPanel.Controls.Add(ok);
        buttonPanel.Controls.Add(cancel);

        Controls.Add(table);
        Controls.Add(buttonPanel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public void ApplyTo(TilesheetEditorViewModelBase vm)
    {
        vm.TileWidth = (int)_tileWidth.Value;
        vm.TileHeight = (int)_tileHeight.Value;
        vm.RegionX = (int)_regionX.Value;
        vm.RegionY = (int)_regionY.Value;
        vm.RegionWidth = (int)_regionWidth.Value;
        vm.RegionHeight = (int)_regionHeight.Value;
        vm.TilePaddingLeft = (int)_paddingLeft.Value;
        vm.TilePaddingTop = (int)_paddingTop.Value;
        vm.TilePaddingRight = (int)_paddingRight.Value;
        vm.TilePaddingBottom = (int)_paddingBottom.Value;
        vm.RegionMarginLeft = (int)_marginLeft.Value;
        vm.RegionMarginTop = (int)_marginTop.Value;
        vm.RegionMarginRight = (int)_marginRight.Value;
        vm.RegionMarginBottom = (int)_marginBottom.Value;
        vm.OverhangLeft = (int)_overhangLeft.Value;
        vm.OverhangTop = (int)_overhangTop.Value;
        vm.OverhangRight = (int)_overhangRight.Value;
        vm.OverhangBottom = (int)_overhangBottom.Value;
        vm.PremultiplyAlpha = _premultiplyAlpha.Checked;
    }

    private static NumericUpDown AddNumber(TableLayoutPanel table, string label, int value, int row, bool allowNegative = false)
    {
        var text = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        var number = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = allowNegative ? -32768 : 0,
            Maximum = 32768,
            Value = Math.Clamp(value, allowNegative ? -32768 : 0, 32768)
        };

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(text, 0, row);
        table.SetColumnSpan(text, 3);
        table.Controls.Add(number, 3, row);
        return number;
    }
}
