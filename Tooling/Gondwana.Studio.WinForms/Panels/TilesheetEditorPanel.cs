using Gondwana.Physics.Collisions;
using Gondwana.Studio.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Gondwana.Studio.WinForms.Panels;

/// <summary>
/// WinForms tilesheet editor panel with region/frame collision-bound editing and preview.
/// </summary>
public sealed class TilesheetEditorPanel : UserControl
{
    private readonly TilesheetEditorViewModelBase _vm;
    private readonly PictureBox _preview;
    private readonly DataGridView _grid;
    private readonly BindingSource _tileCellsSource;
    private readonly Label _statusLabel;
    private readonly TextBox _tileNameTextBox;
    private readonly NumericUpDown _frameCollisionTop;
    private readonly NumericUpDown _frameCollisionBottom;
    private readonly NumericUpDown _frameCollisionLeft;
    private readonly NumericUpDown _frameCollisionRight;

    /// <summary>
    /// Initializes a new tilesheet editor panel.
    /// </summary>
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
        toolbar.Items.AddRange([
            openImageBtn,
            new ToolStripSeparator(),
            rebuildBtn,
            gtsSettingsBtn,
            new ToolStripSeparator(),
            saveBtn]);

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
            Width = 320,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(40, 40, 40)
        };
        _preview.Paint += OnPreviewPaint;
        _preview.LoadCompleted += (_, _) => _preview.Invalidate();

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
            new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "X", DataPropertyName = "X", Width = 45 },
            new DataGridViewTextBoxColumn { HeaderText = "Y", DataPropertyName = "Y", Width = 45 },
            new DataGridViewTextBoxColumn { HeaderText = "C.Top", DataPropertyName = "CollisionAdjustTop", Width = 58 },
            new DataGridViewTextBoxColumn { HeaderText = "C.Bottom", DataPropertyName = "CollisionAdjustBottom", Width = 68 },
            new DataGridViewTextBoxColumn { HeaderText = "C.Left", DataPropertyName = "CollisionAdjustLeft", Width = 58 },
            new DataGridViewTextBoxColumn { HeaderText = "C.Right", DataPropertyName = "CollisionAdjustRight", Width = 60 });

        _tileCellsSource = new BindingSource { DataSource = _vm.TileCells };
        _grid.DataSource = _tileCellsSource;
        _grid.SelectionChanged += OnGridSelectionChanged;

        var editPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 260,
            Padding = new Padding(8),
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 0
        };
        editPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        editPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        AddSectionLabel(editPanel, "Selected Frame");
        AddLabel(editPanel, "Name");
        _tileNameTextBox = new TextBox { Dock = DockStyle.Fill };
        AddControl(editPanel, _tileNameTextBox);

        var applyNameBtn = new Button { Dock = DockStyle.Fill, Height = 28, Text = "Apply Name" };
        applyNameBtn.Click += (_, _) =>
        {
            _vm.SelectedTileName = _tileNameTextBox.Text;
            _vm.ApplyTileNameCommand.Execute(null);
        };
        AddFullWidthControl(editPanel, applyNameBtn);

        AddSectionLabel(editPanel, "Frame Collision Adjust");
        _frameCollisionTop = AddNumber(editPanel, "Top", 0, allowNegative: true);
        _frameCollisionBottom = AddNumber(editPanel, "Bottom", 0, allowNegative: true);
        _frameCollisionLeft = AddNumber(editPanel, "Left", 0, allowNegative: true);
        _frameCollisionRight = AddNumber(editPanel, "Right", 0, allowNegative: true);

        var applyCollisionBtn = new Button
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Text = "Apply Collision Bounds"
        };
        applyCollisionBtn.Click += (_, _) => ApplySelectedFrameCollisionAdjust();
        AddFullWidthControl(editPanel, applyCollisionBtn);

        var contentPanel = new Panel { Dock = DockStyle.Fill };
        contentPanel.Controls.Add(_grid);
        contentPanel.Controls.Add(_preview);
        contentPanel.Controls.Add(editPanel);

        Controls.Add(contentPanel);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.TileCells.CollectionChanged += OnTileCellsChanged;
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0 ||
            _grid.SelectedRows[0].DataBoundItem is not TileCellViewModel tile)
        {
            return;
        }

        _vm.SelectedTile = tile;
        LoadSelectedFrameEditors(tile);
        _preview.Invalidate();
    }

    private void ApplySelectedFrameCollisionAdjust()
    {
        if (_vm.SelectedTile is not { } tile)
            return;

        tile.CollisionAdjust = new CollisionAdjust(
            top: (int)_frameCollisionTop.Value,
            bottom: (int)_frameCollisionBottom.Value,
            left: (int)_frameCollisionLeft.Value,
            right: (int)_frameCollisionRight.Value);

        _tileCellsSource.ResetBindings(false);
        _preview.Invalidate();
    }

    private void LoadSelectedFrameEditors(TileCellViewModel tile)
    {
        _tileNameTextBox.Text = tile.Name;
        _frameCollisionTop.Value = ClampToNumeric(_frameCollisionTop, tile.CollisionAdjustTop);
        _frameCollisionBottom.Value = ClampToNumeric(_frameCollisionBottom, tile.CollisionAdjustBottom);
        _frameCollisionLeft.Value = ClampToNumeric(_frameCollisionLeft, tile.CollisionAdjustLeft);
        _frameCollisionRight.Value = ClampToNumeric(_frameCollisionRight, tile.CollisionAdjustRight);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnVmPropertyChanged(sender, e)));
            return;
        }

        if (e.PropertyName == nameof(TilesheetEditorViewModelBase.StatusText))
            _statusLabel.Text = _vm.StatusText;
        else if (e.PropertyName == nameof(TilesheetEditorViewModelBase.SelectedTileName))
            _tileNameTextBox.Text = _vm.SelectedTileName;
        else if (e.PropertyName is nameof(TilesheetEditorViewModelBase.CollisionAdjustTop)
            or nameof(TilesheetEditorViewModelBase.CollisionAdjustBottom)
            or nameof(TilesheetEditorViewModelBase.CollisionAdjustLeft)
            or nameof(TilesheetEditorViewModelBase.CollisionAdjustRight))
        {
            _tileCellsSource.ResetBindings(false);
            _preview.Invalidate();
        }

        if (e.PropertyName == nameof(TilesheetEditorViewModelBase.ImagePath) &&
            !string.IsNullOrWhiteSpace(_vm.ImagePath))
        {
            _preview.ImageLocation = _vm.ImagePath;
            Text = $"Tilesheet — {Path.GetFileName(_vm.ImagePath)}";
        }
    }

    private void OnTileCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _tileCellsSource.ResetBindings(false);
        _preview.Invalidate();
    }

    private void OnPreviewPaint(object? sender, PaintEventArgs e)
    {
        if (_preview.Image is not { Width: > 0, Height: > 0 } image)
            return;

        var imageBounds = GetZoomedImageBounds(_preview.ClientRectangle, image.Size);
        if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            return;

        var scaleX = (float)imageBounds.Width / image.Width;
        var scaleY = (float)imageBounds.Height / image.Height;

        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var framePen = new Pen(Color.FromArgb(190, 255, 59, 48), 1.5f);
        using var selectedPen = new Pen(Color.FromArgb(255, 255, 210, 0), 2f);

        foreach (var tile in _vm.TileCells)
        {
            var width = (float)tile.CollisionWidth * scaleX;
            var height = (float)tile.CollisionHeight * scaleY;
            if (width <= 0f || height <= 0f)
                continue;

            var rectangle = new RectangleF(
                imageBounds.Left + ((float)tile.CollisionLeft * scaleX),
                imageBounds.Top + ((float)tile.CollisionTop * scaleY),
                width,
                height);

            e.Graphics.DrawRectangle(
                ReferenceEquals(tile, _vm.SelectedTile) ? selectedPen : framePen,
                rectangle.X,
                rectangle.Y,
                rectangle.Width,
                rectangle.Height);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.TileCells.CollectionChanged -= OnTileCellsChanged;
            _preview.Paint -= OnPreviewPaint;
            _preview.Dispose();
            _tileCellsSource.Dispose();
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

    private static Rectangle GetZoomedImageBounds(Rectangle client, Size imageSize)
    {
        var scale = Math.Min(
            (float)client.Width / imageSize.Width,
            (float)client.Height / imageSize.Height);

        var width = (int)MathF.Round(imageSize.Width * scale);
        var height = (int)MathF.Round(imageSize.Height * scale);

        return new Rectangle(
            client.X + ((client.Width - width) / 2),
            client.Y + ((client.Height - height) / 2),
            width,
            height);
    }

    private static decimal ClampToNumeric(NumericUpDown control, int value) =>
        Math.Clamp((decimal)value, control.Minimum, control.Maximum);

    private static void AddSectionLabel(TableLayoutPanel table, string text)
    {
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Height = 28
        };
        AddFullWidthControl(table, label);
    }

    private static void AddLabel(TableLayoutPanel table, string text)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
    }

    private static void AddControl(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount - 1;
        table.Controls.Add(control, 1, row);
    }

    private static void AddFullWidthControl(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(30, control.Height)));
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }

    private static NumericUpDown AddNumber(
        TableLayoutPanel table,
        string label,
        int value,
        bool allowNegative)
    {
        AddLabel(table, label);
        var number = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = allowNegative ? -32768 : 0,
            Maximum = 32768,
            Value = Math.Clamp(value, allowNegative ? -32768 : 0, 32768)
        };
        AddControl(table, number);
        return number;
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
    private readonly NumericUpDown _collisionTop;
    private readonly NumericUpDown _collisionBottom;
    private readonly NumericUpDown _collisionLeft;
    private readonly NumericUpDown _collisionRight;
    private readonly CheckBox _premultiplyAlpha;

    public GtsSettingsDialog(TilesheetEditorViewModelBase vm)
    {
        Text = "GTS Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 520;
        Height = 680;

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

        _collisionTop = AddNumber(table, "Collision Top", vm.CollisionAdjustTop, 18, allowNegative: true);
        _collisionBottom = AddNumber(table, "Collision Bottom", vm.CollisionAdjustBottom, 19, allowNegative: true);
        _collisionLeft = AddNumber(table, "Collision Left", vm.CollisionAdjustLeft, 20, allowNegative: true);
        _collisionRight = AddNumber(table, "Collision Right", vm.CollisionAdjustRight, 21, allowNegative: true);

        var premultiplyLabel = new Label
        {
            Text = "Premultiply Alpha",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _premultiplyAlpha = new CheckBox { Checked = vm.PremultiplyAlpha, Dock = DockStyle.Left };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(premultiplyLabel, 0, 22);
        table.SetColumnSpan(premultiplyLabel, 3);
        table.Controls.Add(_premultiplyAlpha, 3, 22);

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
        vm.CollisionAdjustTop = (int)_collisionTop.Value;
        vm.CollisionAdjustBottom = (int)_collisionBottom.Value;
        vm.CollisionAdjustLeft = (int)_collisionLeft.Value;
        vm.CollisionAdjustRight = (int)_collisionRight.Value;
        vm.PremultiplyAlpha = _premultiplyAlpha.Checked;
    }

    private static NumericUpDown AddNumber(
        TableLayoutPanel table,
        string label,
        int value,
        int row,
        bool allowNegative = false)
    {
        var text = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
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
