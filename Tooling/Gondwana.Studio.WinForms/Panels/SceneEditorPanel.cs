using Gondwana.Studio.Core.Geometry;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.WinForms.Panels;

/// <summary>
/// Panel for the scene editor.
/// Hosts a tile-palette grid and an interactive scene canvas.
/// When WeifenLuo.WinFormsUI.DockPanel is added, this can be made a DockContent.
/// </summary>
public sealed class SceneEditorPanel : UserControl
{
    private readonly SceneEditorViewModel _vm;
    private readonly DataGridView _paletteGrid;
    private readonly Panel _canvas;
    private readonly Label _statusLabel;

    private bool _isPainting;

    /// <summary>
    /// SceneEditorPanel.
    /// </summary>
    /// <param name="vm">ViewModel.</param>
    public SceneEditorPanel(SceneEditorViewModel vm)
    {
        _vm = vm;

        var toolbar = new ToolStrip();
        var openTileBtn = new ToolStripButton("Open Tilesheet…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var toolTile = new ToolStripButton("Tile Tool") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var toolEntity = new ToolStripButton("Entity Tool") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var toolCollider = new ToolStripButton("Collider Tool") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveBtn = new ToolStripButton("Save…") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        openTileBtn.Click += async (_, _) => await _vm.OpenTilesheetCommand.ExecuteAsync(null);
        toolTile.Click += (_, _) => _vm.ActiveTool = "Tile";
        toolEntity.Click += (_, _) => _vm.ActiveTool = "Entity";
        toolCollider.Click += (_, _) => _vm.ActiveTool = "Collider";
        saveBtn.Click += async (_, _) => await _vm.SaveSceneCommand.ExecuteAsync(null);
        toolbar.Items.AddRange([
            openTileBtn, new ToolStripSeparator(),
            toolTile, toolEntity, toolCollider, new ToolStripSeparator(),
            saveBtn
        ]);

        _paletteGrid = new DataGridView
        {
            Dock = DockStyle.Left,
            Width = 160,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false
        };

        var paletteSource = new BindingSource { DataSource = _vm.TilePalette };
        _paletteGrid.DataSource = paletteSource;

        System.Collections.Specialized.NotifyCollectionChangedEventHandler paletteChanged = (_, _) =>
            paletteSource.ResetBindings(false);

        _vm.TilePalette.CollectionChanged += paletteChanged;
        Disposed += (_, _) => _vm.TilePalette.CollectionChanged -= paletteChanged;
        _paletteGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#", DataPropertyName = "Index", Width = 40 },
            new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }
        );
        _paletteGrid.SelectionChanged += OnPaletteSelectionChanged;

        _canvas = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(50, 50, 50),
            Cursor = Cursors.Cross
        };
        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;
        _canvas.Paint += OnCanvasPaint;
        _vm.PaintedTiles.CollectionChanged += (_, _) => _canvas.Invalidate();
        _vm.Colliders.CollectionChanged += (_, _) => _canvas.Invalidate();
        _vm.Entities.CollectionChanged += (_, _) => _canvas.Invalidate();

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 22,
            Text = _vm.StatusText
        };

        Controls.Add(_canvas);
        Controls.Add(_paletteGrid);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnPaletteSelectionChanged(object? sender, EventArgs e)
    {
        if (_paletteGrid.SelectedRows.Count > 0 && _paletteGrid.SelectedRows[0].DataBoundItem is TileCellViewModel tile)
            _vm.SelectedPaletteTile = tile;
    }

    private void OnCanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _isPainting = true;
        HandleCanvasInteraction(e.Location);
    }

    private void OnCanvasMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isPainting && e.Button == MouseButtons.Left)
            HandleCanvasInteraction(e.Location);
    }

    private void OnCanvasMouseUp(object? sender, MouseEventArgs e)
    {
        _isPainting = false;
    }

    private void HandleCanvasInteraction(System.Drawing.Point screenPoint)
    {
        var worldX = screenPoint.X + _vm.CameraX;
        var worldY = screenPoint.Y + _vm.CameraY;

        if (_vm.ActiveTool == "Collider")
        {
            var tileX = (int)Math.Floor(worldX / Math.Max(1, _vm.TileWidth)) * _vm.TileWidth;
            var tileY = (int)Math.Floor(worldY / Math.Max(1, _vm.TileHeight)) * _vm.TileHeight;
            _vm.AddCollider(new RectD(tileX, tileY, _vm.TileWidth, _vm.TileHeight));
        }
        else
        {
            _vm.ApplyToolAt(worldX, worldY);
        }
    }

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;

        foreach (var tile in _vm.PaintedTiles)
        {
            var rect = new System.Drawing.Rectangle(
                (int)(tile.PixelX - _vm.CameraX),
                (int)(tile.PixelY - _vm.CameraY),
                _vm.TileWidth,
                _vm.TileHeight);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(80, 120, 80));
            g.FillRectangle(brush, rect);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(100, 160, 100));
            g.DrawRectangle(pen, rect);
        }

        foreach (var collider in _vm.Colliders)
        {
            var rect = new System.Drawing.Rectangle(
                (int)(collider.X - _vm.CameraX),
                (int)(collider.Y - _vm.CameraY),
                (int)collider.Width,
                (int)collider.Height);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.Yellow, 1.5f);
            g.DrawRectangle(pen, rect);
        }

        foreach (var entity in _vm.Entities)
        {
            var x = (int)(entity.X - _vm.CameraX);
            var y = (int)(entity.Y - _vm.CameraY);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.DodgerBlue);
            g.FillEllipse(brush, x - 6, y - 6, 12, 12);
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnVmPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(SceneEditorViewModel.StatusText))
            _statusLabel.Text = _vm.StatusText;

        if (e.PropertyName is nameof(SceneEditorViewModel.TilesheetPath) or
            nameof(SceneEditorViewModel.CameraX) or
            nameof(SceneEditorViewModel.CameraY) or
            nameof(SceneEditorViewModel.Zoom))
        {
            _canvas.Invalidate();
        }
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">disposing.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        base.Dispose(disposing);
    }
}
