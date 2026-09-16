using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.SkiaSharp;
using Gondwana.Tooling.Tilesheets.Editing;
using SkiaSharp;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal sealed class EditorDocument : DockContent
{
    public TilesheetDocument Document { get; }
    private readonly OverlaySettings _overlaySettings;
    private readonly ImageViewport _viewport = new();
    private readonly PropertyGrid _definitionProperties = CreatePropertyGrid();
    private readonly PropertyGrid _regionProperties = CreatePropertyGrid();
    private readonly PropertyGrid _frameProperties = CreatePropertyGrid();
    private readonly ComboBox _regions = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
    private readonly NumericUpDown _column = new() { Width = 80 };
    private readonly NumericUpDown _row = new() { Width = 80 };
    private readonly Label _gridSize = new() { AutoSize = true };
    private readonly TextBox _validation = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
    private string? _previewKey;
    private string? _previewError;
    private bool _refreshing;
    private bool _refreshPending;
    private readonly Func<EditorDocument, bool, bool> _save;
    public bool CloseApproved { get; set; }
    private TilesheetRegionDefinition? SelectedRegion => _regions.SelectedItem as TilesheetRegionDefinition;
    public Size? ImageSize => _viewport.Image?.Size;

    public EditorDocument(TilesheetDocument document, Func<EditorDocument, bool, bool> save, OverlaySettings? overlaySettings = null)
    {
        Document = document;
        _overlaySettings = overlaySettings ?? OverlaySettings.Default;
        _viewport.Colors = _overlaySettings;
        _save = save;
        DockAreas = DockAreas.Document | DockAreas.Float;
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1000, 650), SplitterDistance = 640, Panel2MinSize = 270 };
        var previewSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            Size = new Size(640, 650), SplitterDistance = 495, Panel2MinSize = 70 };
        previewSplit.Panel1.Controls.Add(_viewport);
        previewSplit.Panel1.Controls.Add(BuildPreviewToolbar());
        previewSplit.Panel2.Controls.Add(_validation);
        split.Panel1.Controls.Add(previewSplit);

        var regionTools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        regionTools.Items.Add("+ Region", null, (_, _) => AddRegion());
        regionTools.Items.Add("− Region", null, (_, _) => RemoveRegion());
        regionTools.Items.Add("Prune invalid frames…", null, (_, _) => PruneFrames());
        var navigator = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        navigator.Controls.AddRange([new Label { Text = "Frame X", AutoSize = true }, _column,
            new Label { Text = "Y", AutoSize = true }, _row, _gridSize]);
        _column.ValueChanged += (_, _) => SelectFrame();
        _row.ValueChanged += (_, _) => SelectFrame();
        _regions.SelectedIndexChanged += (_, _) => { if (!_refreshing) RefreshView(); };
        var lowerInspectors = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            Size = new Size(360, 430), SplitterDistance = 210,
            Panel1MinSize = 100, Panel2MinSize = 100
        };
        lowerInspectors.Panel1.Controls.Add(InspectorSection("Region", _regionProperties, regionTools, _regions));
        lowerInspectors.Panel2.Controls.Add(InspectorSection("Frame", _frameProperties, navigator));
        var inspectors = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            Size = new Size(360, 650), SplitterDistance = 210,
            Panel1MinSize = 100, Panel2MinSize = 204
        };
        inspectors.Panel1.Controls.Add(InspectorSection("Definition", _definitionProperties));
        inspectors.Panel2.Controls.Add(lowerInspectors);
        split.Panel2.Controls.Add(inspectors);
        Controls.Add(split);
        DarkTheme.Apply(this);
        _viewport.FrameSelected += (region, p) =>
        {
            // A GTS can describe disjoint grids. Select the clicked region before
            // setting coordinates so the navigator uses that region's dimensions.
            _refreshing = true;
            _regions.SelectedItem = region;
            _refreshing = false;
            RefreshView();
            _refreshing = true;
            _column.Value = Math.Clamp(p.X, 0, _column.Maximum);
            _row.Value = Math.Clamp(p.Y, 0, _row.Maximum);
            _refreshing = false;
            SelectFrame();
        };
        Document.Changed += DocumentChanged;
        _overlaySettings.Changed += OverlayColorsChanged;
        FormClosing += (_, e) => { if (!CloseApproved) e.Cancel = !ConfirmClose(); };
        RefreshView();
    }

    private ToolStrip BuildPreviewToolbar()
    {
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        bar.Items.Add("Image…", null, (_, _) => ChooseImage());
        bar.Items.Add("Reload image", null, (_, _) => { _previewKey = null; RefreshView(); });
        bar.Items.Add("−", null, (_, _) => _viewport.SetZoom(_viewport.Zoom / 2));
        var zoom = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 75,
            BackColor = DarkTheme.Background, ForeColor = DarkTheme.Foreground, FlatStyle = FlatStyle.Flat };
        zoom.Items.AddRange(["25%", "50%", "100%", "200%", "400%", "Fit"]);
        zoom.SelectedIndex = 2;
        zoom.SelectedIndexChanged += (_, _) =>
        {
            if (zoom.SelectedIndex == 5) _viewport.Fit();
            else _viewport.SetZoom(new[] { .25f, .5f, 1, 2, 4 }[zoom.SelectedIndex]);
        };
        bar.Items.Add(zoom);
        bar.Items.Add("+", null, (_, _) => _viewport.SetZoom(_viewport.Zoom * 2));
        var overlays = new ToolStripDropDownButton("Overlays / legend");
        foreach (var (kind, description) in new[]
        {
            (OverlayKind.Regions, "Regions: other region bounds"),
            (OverlayKind.SelectedRegion, "Selected region bounds"),
            (OverlayKind.Frames, "Frames: tile bounds"),
            (OverlayKind.SelectedFrame, "Selected frame"),
            (OverlayKind.Margin, "Margin: inner region margin"),
            (OverlayKind.Padding, "Padding: padded cell"),
            (OverlayKind.Overhang, "Overhang: world extent at source scale"),
            (OverlayKind.Collision, "Collision: effective bounds")
        })
        {
            var row = new OverlayLegendRow(kind, description, _overlaySettings,
                visible =>
                {
                    if (visible) _viewport.Overlays.Add(kind.ToString()); else _viewport.Overlays.Remove(kind.ToString());
                    _viewport.Invalidate();
                },
                () =>
                {
                    overlays.HideDropDown();
                    using var dialog = new ColorDialog { Color = _overlaySettings[kind], FullOpen = true };
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try { _overlaySettings.SetColor(kind, dialog.Color); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        MessageBox.Show(this, $"Could not save overlay colors to {_overlaySettings.FilePath}.\n{ex.Message}",
                            "Overlay settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            overlays.DropDownItems.Add(new ToolStripControlHost(row) { AutoSize = false, Size = row.Size, Margin = Padding.Empty, Padding = Padding.Empty });
        }
        bar.Items.Add(overlays);
        return bar;
    }

    private void OverlayColorsChanged(object? sender, EventArgs e)
    {
        _viewport.Invalidate();
        UpdateValidation();
    }

    public void ChooseImage(string? path = null)
    {
        if (path is null)
        {
            using var dialog = new OpenFileDialog { Filter = MainForm.ImageFilter, InitialDirectory = Document.BaseDirectory };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.FileName;
        }
        Document.SetImage(Path.GetFullPath(path));
        RefreshView();
    }

    public void AddRegion()
    {
        string name = "default";
        int number = 1;
        while (Document.Definition.Regions.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))) name = "region" + number++;
        var size = ImageSize ?? new Size(32, 32);
        var region = new TilesheetRegionDefinition { Name = name, Area = new Rectangle(Point.Empty, size),
            TileSize = new Size(Math.Min(32, size.Width), Math.Min(32, size.Height)) };
        Document.Definition.Regions.Add(region);
        Document.MarkChanged();
        RefreshView();
        _regions.SelectedItem = region;
        RefreshProperties();
    }

    private void RemoveRegion()
    {
        if (SelectedRegion is not { } region) return;
        if (MessageBox.Show(this, $"Remove region '{region.Name}' and all its frame metadata?", "Remove region",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Document.Definition.Regions.Remove(region);
        Document.MarkChanged();
        RefreshView();
    }

    private void PruneFrames()
    {
        if (SelectedRegion is not { } region) return;
        if (MessageBox.Show(this, "Delete metadata outside this region's current grid? Restore the geometry instead to keep it.",
            "Prune invalid frame metadata", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Document.RemoveOutOfGridMetadata(region);
        RefreshView();
    }

    private void DocumentChanged(object? sender, EventArgs e)
    {
        Text = (Document.FilePath is null ? Document.Definition.Name : Path.GetFileName(Document.FilePath)) + (Document.IsDirty ? " *" : "");
        // Rebind after PropertyGrid finishes committing, never while it is editing a descriptor.
        if (_refreshPending || !IsHandleCreated) return;
        _refreshPending = true;
        BeginInvoke(() => { _refreshPending = false; if (!IsDisposed) RefreshView(); });
    }

    private void SelectFrame()
    {
        if (_refreshing) return;
        _viewport.SelectedFrame = new Point((int)_column.Value, (int)_row.Value);
        _viewport.RevealSelectedFrame();
        _viewport.Invalidate();
        RefreshProperties();
    }

    private static PropertyGrid CreatePropertyGrid() => new()
    {
        Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = false
    };

    private static Control InspectorSection(string title, PropertyGrid grid, params Control[] controls)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(grid);
        foreach (var control in controls.Reverse()) panel.Controls.Add(control);
        panel.Controls.Add(new Label
        {
            Text = title, Dock = DockStyle.Top, Height = 26,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0)
        });
        return panel;
    }

    private void RefreshProperties()
    {
        BindProperties(_definitionProperties, PropertyFields.Definition(Document, Document.MarkChanged));
        PropertyFields? regionFields = null;
        PropertyFields? frameFields = null;
        if (SelectedRegion is { } region)
        {
            regionFields = new PropertyFields();
            regionFields.AddModel(region, Document.MarkChanged, "Frames");
            if (_column.Enabled)
                frameFields = PropertyFields.Frame(Document, region, (int)_column.Value, (int)_row.Value, Document.MarkChanged);
        }
        BindProperties(_regionProperties, regionFields);
        BindProperties(_frameProperties, frameFields);
    }

    private static void BindProperties(PropertyGrid grid, PropertyFields? fields)
    {
        string? selectedProperty = grid.SelectedGridItem?.PropertyDescriptor?.Name;
        var collapsedCategories = GridItems(grid).Where(item => item.GridItemType == GridItemType.Category && !item.Expanded)
            .Select(item => item.Label).ToHashSet();
        grid.SelectedObject = fields;
        grid.Enabled = fields is not null;
        foreach (var item in GridItems(grid))
        {
            if (item.GridItemType == GridItemType.Category && collapsedCategories.Contains(item.Label)) item.Expanded = false;
            if (selectedProperty is not null && item.PropertyDescriptor?.Name == selectedProperty)
                grid.SelectedGridItem = item;
        }
    }

    private static IEnumerable<GridItem> GridItems(PropertyGrid grid)
    {
        var root = grid.SelectedGridItem;
        if (root is null) yield break;
        while (root.Parent is not null) root = root.Parent;
        foreach (var item in Descendants(root)) yield return item;

        static IEnumerable<GridItem> Descendants(GridItem parent)
        {
            foreach (GridItem child in parent.GridItems)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }
    public void RefreshView()
    {
        _refreshing = true;
        var selected = SelectedRegion;
        _regions.Items.Clear();
        _regions.Items.AddRange(Document.Definition.Regions.Cast<object>().ToArray());
        if (selected is not null && _regions.Items.Contains(selected)) _regions.SelectedItem = selected;
        else if (_regions.Items.Count > 0) _regions.SelectedIndex = 0;
        var (columns, rows) = SelectedRegion is { } r ? TilesheetDefinitionValidator.GridSize(r) : (0L, 0L);
        _column.Value = Math.Min(_column.Value, Math.Max(0, columns - 1));
        _row.Value = Math.Min(_row.Value, Math.Max(0, rows - 1));
        _column.Maximum = Math.Clamp(columns - 1, 0, int.MaxValue);
        _row.Maximum = Math.Clamp(rows - 1, 0, int.MaxValue);
        _column.Enabled = _row.Enabled = columns > 0 && rows > 0;
        _gridSize.Text = $"{columns} × {rows} frames";
        _refreshing = false;
        LoadPreview();
        _viewport.Definition = Document.Definition;
        _viewport.SelectedRegion = SelectedRegion;
        _viewport.SelectedFrame = _column.Enabled ? new Point((int)_column.Value, (int)_row.Value) : null;
        _viewport.Invalidate();
        RefreshProperties();
        UpdateValidation();
        Text = (Document.FilePath is null ? Document.Definition.Name : Path.GetFileName(Document.FilePath)) + (Document.IsDirty ? " *" : "");
        ToolTipText = Document.FilePath ?? "Unsaved GTS definition";
    }

    private void LoadPreview()
    {
        var model = Document.Definition;
        var mask = model.Mask;
        string key = $"{Document.BaseDirectory}|{model.Image?.FilePath}|{model.Image?.AssetsFilePath}|{model.Image?.AssetEntryName}|{model.PremultiplyAlpha}|" +
            (mask is null ? "none" : $"{mask.Red},{mask.Green},{mask.Blue},{mask.Alpha},{mask.Tolerance}");
        if (key == _previewKey) return;
        _previewKey = key;
        _viewport.Image?.Dispose();
        _viewport.Image = null;
        _previewError = null;
        try
        {
            if (Document.ResolveImagePath() is not { } path)
            {
                _viewport.Message = "No loose image source. Packed .gaf images cannot be previewed; their fields are preserved.";
                return;
            }
            // Use the runtime decoder for format parity (including WebP), then GDI+ for
            // all display and overlays. No runtime tilesheet or engine is instantiated.
            using var decoded = SKBitmap.Decode(path) ?? throw new InvalidDataException("Unsupported or corrupt image.");
            using var rgba = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            using var pixels = decoded.PeekPixels();
            if (!pixels.ReadPixels(rgba.Info, rgba.GetPixels(), rgba.RowBytes, 0, 0))
                throw new InvalidDataException("Could not decode image pixels.");
            if (mask is not null) rgba.ApplyAlphaMask(new SKColor(mask.Red, mask.Green, mask.Blue, mask.Alpha), mask.Tolerance);
            using var encoded = rgba.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = encoded.AsStream();
            using var bitmap = new Bitmap(stream);
            _viewport.Image = new Bitmap(bitmap);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            _previewError = "Image cannot be previewed: " + ex.Message;
            _viewport.Message = _previewError;
        }
        finally { _viewport.UpdateExtent(); }
    }

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate(ImageSize).ToList();
        if (_previewError is not null) errors.Add(_previewError);
        var lines = errors.Select(e => "ERROR: " + e).ToList();
        if (_overlaySettings.Warning is { } settingsWarning) lines.Add("WARNING: " + settingsWarning);
        if (string.IsNullOrWhiteSpace(Document.Definition.Image?.FilePath))
            lines.Add("WARNING: Packed image preview and asset validation are deferred; existing packed fields are preserved.");
        lines.Add("INFO: Geometry changes retain frame metadata at its original coordinates. Use Prune invalid frames only to explicitly discard it.");
        lines.Add("INFO: Overhang shows world extent at source scale; it does not change the extracted frame. Dense grids are sampled visually; use X/Y to inspect any frame.");
        if (Document.Definition.Mask is not null)
            lines.Add("INFO: Mask preview matches runtime RGB tolerance (alpha is stored, but runtime matching ignores it). GDI+ composites alpha for display.");
        if (errors.Count == 0) lines.Insert(0, "VALID: GTS geometry and available loose-image checks passed.");
        _validation.Text = string.Join(Environment.NewLine, lines);
        return errors;
    }

    public bool ConfirmClose()
    {
        if (!CommitEdits()) return false;
        if (!Document.IsDirty) return true;
        var result = MessageBox.Show(this, $"Save changes to {Text.TrimEnd(' ', '*')}?", "Unsaved definition",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        return result == DialogResult.No || result == DialogResult.Yes && _save(this, false);
    }

    public bool CommitEdits()
    {
        _validation.Focus();
        return !_definitionProperties.ContainsFocus && !_regionProperties.ContainsFocus && !_frameProperties.ContainsFocus && ValidateChildren();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.Changed -= DocumentChanged;
            _overlaySettings.Changed -= OverlayColorsChanged;
            _viewport.Image?.Dispose();
            _viewport.Image = null;
        }
        base.Dispose(disposing);
    }
}


