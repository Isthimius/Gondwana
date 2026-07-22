using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.WinForms.Panels;

/// <summary>
/// Panel for the animation editor.
/// When WeifenLuo.WinFormsUI.DockPanel is added, this can be made a DockContent.
/// </summary>
public sealed class AnimationEditorPanel : UserControl
{
    private readonly AnimationEditorViewModel _vm;
    private readonly DataGridView _paletteGrid;
    private readonly DataGridView _framesGrid;
    private readonly Label _previewLabel;
    private readonly Label _statusLabel;

    /// <summary>
    /// AnimationEditorPanel.
    /// </summary>
    /// <param name="vm">ViewModel.</param>
    public AnimationEditorPanel(AnimationEditorViewModel vm)
    {
        _vm = vm;

        var toolbar = new ToolStrip();
        var openBtn = new ToolStripButton("Open Tilesheet…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var addFrameBtn = new ToolStripButton("Add Frame") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var removeFrameBtn = new ToolStripButton("Remove Frame") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var upBtn = new ToolStripButton("▲") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var downBtn = new ToolStripButton("▼") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var playBtn = new ToolStripButton("▶ Play") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var stopBtn = new ToolStripButton("■ Stop") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var saveBtn = new ToolStripButton("Save…") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        openBtn.Click += async (_, _) => await _vm.OpenTilesheetCommand.ExecuteAsync(null);
        addFrameBtn.Click += (_, _) => _vm.AddFrameCommand.Execute(null);
        removeFrameBtn.Click += (_, _) => _vm.RemoveFrameCommand.Execute(null);
        upBtn.Click += (_, _) => _vm.MoveFrameUpCommand.Execute(null);
        downBtn.Click += (_, _) => _vm.MoveFrameDownCommand.Execute(null);
        playBtn.Click += (_, _) => _vm.StartPreviewCommand.Execute(null);
        stopBtn.Click += (_, _) => _vm.StopPreviewCommand.Execute(null);
        saveBtn.Click += async (_, _) => await _vm.SaveCommand.ExecuteAsync(null);
        toolbar.Items.AddRange([
            openBtn, new ToolStripSeparator(),
            addFrameBtn, removeFrameBtn, upBtn, downBtn, new ToolStripSeparator(),
            playBtn, stopBtn, new ToolStripSeparator(),
            saveBtn
        ]);

        _paletteGrid = BuildGrid(["Index", "Name"], ["Index", "Name"]);
        _paletteGrid.DataSource = _vm.TilePalette;
        _paletteGrid.SelectionChanged += OnPaletteSelectionChanged;

        _framesGrid = BuildGrid(["Tile Index", "Tile Name", "Duration (ms)"], ["TileIndex", "TileName", "DurationMs"]);
        _framesGrid.DataSource = _vm.Frames;
        _framesGrid.SelectionChanged += OnFramesSelectionChanged;

        _previewLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 24,
            Text = _vm.PreviewText,
            Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 22,
            Text = _vm.StatusText
        };

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 200
        };
        splitContainer.Panel1.Controls.Add(_paletteGrid);
        splitContainer.Panel2.Controls.Add(_framesGrid);

        Controls.Add(splitContainer);
        Controls.Add(_previewLabel);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private static DataGridView BuildGrid(string[] headers, string[] properties)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false
        };
        for (var i = 0; i < headers.Length; i++)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headers[i],
                DataPropertyName = properties[i],
                AutoSizeMode = i == headers.Length - 1 ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                Width = i == 0 ? 60 : 120
            });
        }
        return grid;
    }

    private void OnPaletteSelectionChanged(object? sender, EventArgs e)
    {
        if (_paletteGrid.SelectedRows.Count > 0 && _paletteGrid.SelectedRows[0].DataBoundItem is TileCellViewModel tile)
            _vm.SelectedPaletteTile = tile;
    }

    private void OnFramesSelectionChanged(object? sender, EventArgs e)
    {
        if (_framesGrid.SelectedRows.Count > 0 && _framesGrid.SelectedRows[0].DataBoundItem is AnimationFrameViewModel frame)
            _vm.SelectedFrame = frame;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnVmPropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AnimationEditorViewModel.StatusText):
                _statusLabel.Text = _vm.StatusText;
                break;
            case nameof(AnimationEditorViewModel.PreviewText):
                _previewLabel.Text = _vm.PreviewText;
                break;
            case nameof(AnimationEditorViewModel.TilesheetPath) when !string.IsNullOrWhiteSpace(_vm.TilesheetPath):
                Text = $"Animation — {System.IO.Path.GetFileName(_vm.TilesheetPath)}";
                break;
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
            _vm.Dispose();
        }
        base.Dispose(disposing);
    }
}
