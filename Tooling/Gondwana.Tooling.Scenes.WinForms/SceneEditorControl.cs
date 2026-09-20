using System.Drawing.Drawing2D;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>
/// Hostable WinForms authoring surface for one GSCN scene definition.
/// </summary>
public sealed class SceneEditorControl : UserControl
{
    private const int SourceFrameThumbnailSize = 32;

    public static IReadOnlyList<string> PaneNames { get; } =
        Array.AsReadOnly(
        [
            "Scene structure",
            "Scene preview",
            "GTS frame sources",
            "GANI animations",
            "Properties",
            "Tile properties",
            "Validation"
        ]);

    private readonly TreeView _structure = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    private readonly PropertyGrid _properties = new()
    {
        Dock = DockStyle.Fill,
        ToolbarVisible = false,
        HelpVisible = true
    };

    private readonly ScenePreviewControl _preview = new();

    private readonly TreeView _sourceTree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    private readonly ImageList _sourceFrameImages = new()
    {
        ImageSize = new Size(SourceFrameThumbnailSize, SourceFrameThumbnailSize),
        ColorDepth = ColorDepth.Depth32Bit
    };

    private readonly ListView _animationList = new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
        View = View.Details
    };

    private readonly PropertyGrid _tileProperties = new()
    {
        Dock = DockStyle.Fill,
        ToolbarVisible = false,
        HelpVisible = true
    };

    private readonly TextBox _validation = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical
    };

    private readonly NumericUpDown _tileX = new() { Minimum = 0, Width = 64 };
    private readonly NumericUpDown _tileY = new() { Minimum = 0, Width = 64 };
    private readonly List<SceneTilesheetSource> _tilesheetSources = [];
    private readonly List<SceneAnimationSource> _animationSources = [];
    private readonly List<string> _sourceDiagnostics = [];
    private EditorDockWorkspace _workspace = null!;

    private SceneLayerDefinition? _selectedLayer;
    private bool _syncingTileCoordinates;
    private bool _refreshPending;

    public SceneDocument Document { get; }
    public SceneDefinition Definition => Document.Definition;

    public SceneEditorControl(SceneDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Size = new Size(1400, 900);
        Dock = DockStyle.Fill;

        _sourceTree.ImageList = _sourceFrameImages;
        _sourceTree.BeforeExpand += SourceTreeBeforeExpand;
        _sourceTree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node.Tag is FrameTag frame)
                AssignFrame(frame);
        };

        _structure.AfterSelect += (_, _) => StructureSelectionChanged();

        _animationList.Columns.Add("Animation key", 210);
        _animationList.Columns.Add("Source", 260);
        _animationList.Columns.Add("Frames", 60, HorizontalAlignment.Right);
        _animationList.ItemActivate += (_, _) => AssignSelectedAnimation();

        _preview.TileSelected += (layer, x, y) =>
        {
            SelectLayerNode(layer);
            SetSelectedTile(layer, x, y);
        };

        _properties.PropertyValueChanged += (_, _) =>
        {
            Document.MarkChanged();
            ClampTileSelection();
            RefreshStructure();
            RefreshPreview();
            UpdateValidation();
        };

        _tileX.ValueChanged += (_, _) => TileCoordinateChanged();
        _tileY.ValueChanged += (_, _) => TileCoordinateChanged();

        BuildLayout();
        LoadDeclaredSources();
        RecoverLegacyLooseSources();
        Document.Changed += DocumentChanged;

        DarkTheme.Apply(this);
        RefreshView();
    }

    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        SceneTilesheetSource? last = null;
        foreach (var path in paths)
        {
            var source = SceneTilesheetSource.Load(path);
            var existing = FindTilesheet(source.Definition.Name);
            if (existing is not null)
            {
                _tilesheetSources.Remove(existing);
                existing.Dispose();
            }

            Document.SetLooseTilesheetSource(
                source.Definition.Name,
                source.FilePath);

            _tilesheetSources.Add(source);
            last = source;
        }

        RefreshSourceTree();
        RefreshPreview();
        UpdateValidation();

        if (last is not null)
            SelectTilesheetRoot(last);
    }

    public void AddAnimationSources(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        SceneAnimationSource? last = null;
        foreach (var path in paths)
        {
            var source = SceneAnimationSource.Load(path);
            var existing = FindAnimation(source.Definition.Key);
            if (existing is not null)
            {
                _animationSources.Remove(existing);
                existing.Dispose();
            }

            Document.SetLooseAnimationSource(
                source.Definition.Key,
                source.FilePath);

            _animationSources.Add(source);
            last = source;
        }

        RefreshAnimationList();
        RefreshPreview();
        UpdateValidation();

        if (last is not null)
            SelectAnimation(last);
    }

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        var lines = new List<string>();

        if (errors.Count == 0)
            lines.Add("VALID: GSCN structural validation passed.");
        else
            lines.AddRange(errors.Select(error => "ERROR: " + error));

        lines.AddRange(_sourceDiagnostics);

        foreach (var layer in Definition.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Frame is { } frame)
                {
                    var source = FindTilesheet(frame.Tilesheet);
                    if (source is null)
                    {
                        lines.Add(
                            $"WARNING: {LayerLabel(layer)} tile ({tile.X},{tile.Y}) references GTS '{frame.Tilesheet}', but no matching authoring source is loaded.");
                    }
                    else if (!source.TryResolve(frame, out _, out _))
                    {
                        lines.Add(
                            $"WARNING: {LayerLabel(layer)} tile ({tile.X},{tile.Y}) cannot resolve {frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile}).");
                    }
                    else if (source.PreviewWarning is { } warning)
                    {
                        lines.Add($"WARNING: {source.Definition.Name}: {warning}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(tile.AnimationKey))
                {
                    var animation = FindAnimation(tile.AnimationKey);
                    if (animation is null)
                    {
                        lines.Add(
                            $"WARNING: {LayerLabel(layer)} tile ({tile.X},{tile.Y}) references GANI '{tile.AnimationKey}', but no matching authoring source is loaded.");
                    }
                    else
                    {
                        lines.AddRange(animation.Diagnostics);
                    }
                }
            }
        }

        lines.Add(
            "INFO: GSCN runtime references remain logical; TilesheetSources and AnimationSources store portable authoring locations only.");

        _validation.Text = string.Join(
            Environment.NewLine,
            lines.Distinct(StringComparer.Ordinal));

        return errors;
    }

    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus &&
               !_tileProperties.ContainsFocus &&
               ValidateChildren();
    }

    public bool ShowPane(string paneName) =>
        _workspace.ShowPane(paneName);

    public bool IsPaneVisible(string paneName) =>
        _workspace.IsPaneVisible(paneName);

    public void ShowAllPanes() =>
        _workspace.ShowAllPanes();

    private void BuildLayout()
    {
        _workspace = new EditorDockWorkspace();
        Controls.Add(_workspace);

        var dock = _workspace.DockPanel;
        var structure = _workspace.AddPane(
            "Scene structure",
            _structure,
            BuildStructureToolbar());

        var preview = _workspace.AddPane(
            "Scene preview",
            _preview);

        var gts = _workspace.AddPane(
            "GTS frame sources",
            _sourceTree,
            BuildGtsToolbar());

        var animations = _workspace.AddPane(
            "GANI animations",
            _animationList,
            BuildAnimationToolbar());

        var properties = _workspace.AddPane(
            "Properties",
            _properties);

        var tileProperties = _workspace.AddPane(
            "Tile properties",
            _tileProperties,
            BuildTileToolbar());

        var validation = _workspace.AddPane(
            "Validation",
            _validation);

        preview.Show(dock, DockState.Document);
        structure.Show(preview.Pane, DockAlignment.Left, 300d / 1400);
        properties.Show(preview.Pane, DockAlignment.Right, 330d / 1100);
        tileProperties.Show(properties.Pane, DockAlignment.Bottom, .52);
        gts.Show(preview.Pane, DockAlignment.Bottom, .34);
        animations.Show(gts.Pane, DockAlignment.Right, .34);
        validation.Show(tileProperties.Pane, DockAlignment.Bottom, .28);
    }

    private ToolStrip BuildStructureToolbar()
    {
        var bar = NewToolbar();
        bar.Items.Add("+ Layer", null, (_, _) =>
        {
            var layer = Document.AddLayer();
            RefreshStructure();
            SelectLayerNode(layer);
            SetSelectedTile(layer, 0, 0);
            RefreshPreview();
            UpdateValidation();
        });
        bar.Items.Add("− Layer", null, (_, _) =>
        {
            if (_structure.SelectedNode?.Tag is not SceneLayerDefinition layer)
                return;

            if (Document.RemoveLayer(layer))
            {
                if (ReferenceEquals(_selectedLayer, layer))
                    _selectedLayer = null;

                RefreshView();
            }
        });
        return bar;
    }

    private ToolStrip BuildGtsToolbar()
    {
        var bar = NewToolbar();
        bar.Items.Add("Add GTS…", null, (_, _) => ChooseTilesheetSources());
        bar.Items.Add("Remove", null, (_, _) => RemoveSelectedTilesheetSource());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Assign frame", null, (_, _) =>
        {
            if (_sourceTree.SelectedNode?.Tag is FrameTag frame)
                AssignFrame(frame);
        });
        bar.Items.Add("Clear frame", null, (_, _) => ClearFrame());
        return bar;
    }

    private ToolStrip BuildAnimationToolbar()
    {
        var bar = NewToolbar();
        bar.Items.Add("Add GANI…", null, (_, _) => ChooseAnimationSources());
        bar.Items.Add("Remove", null, (_, _) => RemoveSelectedAnimationSource());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Assign", null, (_, _) => AssignSelectedAnimation());
        bar.Items.Add("Clear", null, (_, _) => ClearAnimation());
        return bar;
    }

    private ToolStrip BuildTileToolbar()
    {
        var bar = NewToolbar();
        bar.Items.Add(new ToolStripLabel("X"));
        bar.Items.Add(new ToolStripControlHost(_tileX));
        bar.Items.Add(new ToolStripLabel("Y"));
        bar.Items.Add(new ToolStripControlHost(_tileY));
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Clear tile", null, (_, _) => ClearSelectedTile());
        return bar;
    }

    private static ToolStrip NewToolbar() =>
        new()
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

    private void ChooseTilesheetSources()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana tilesheets|*.gts",
            Multiselect = true,
            InitialDirectory = Document.BaseDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            AddTilesheetSources(dialog.FileNames);
        }
        catch (Exception ex) when (IsAuthoringException(ex))
        {
            ShowError(ex);
        }
    }

    private void ChooseAnimationSources()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana animations|*.gani",
            Multiselect = true,
            InitialDirectory = Document.BaseDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            AddAnimationSources(dialog.FileNames);
        }
        catch (Exception ex) when (IsAuthoringException(ex))
        {
            ShowError(ex);
        }
    }

    private void LoadDeclaredSources()
    {
        foreach (var source in Definition.TilesheetSources)
        {
            if (source.Kind != SceneTilesheetSourceKind.LooseDefinitionFile ||
                string.IsNullOrWhiteSpace(source.GtsPath))
            {
                _sourceDiagnostics.Add(
                    $"INFO: GTS '{source.Tilesheet}' uses a packed source; its metadata is preserved but this editor does not preview packed GTS entries yet.");
                continue;
            }

            try
            {
                var loaded = SceneTilesheetSource.Load(
                    Document.ResolveReferencePath(source.GtsPath));

                if (!string.Equals(
                        loaded.Definition.Name,
                        source.Tilesheet,
                        StringComparison.Ordinal))
                {
                    _sourceDiagnostics.Add(
                        $"WARNING: GTS source '{source.Tilesheet}' resolves to definition '{loaded.Definition.Name}'.");
                }

                _tilesheetSources.Add(loaded);
            }
            catch (Exception ex) when (IsAuthoringException(ex))
            {
                _sourceDiagnostics.Add(
                    $"WARNING: Could not load GTS source '{source.Tilesheet}': {ex.Message}");
            }
        }

        foreach (var source in Definition.AnimationSources)
        {
            if (source.Kind != SceneAnimationSourceKind.LooseDefinitionFile ||
                string.IsNullOrWhiteSpace(source.GaniPath))
            {
                _sourceDiagnostics.Add(
                    $"INFO: GANI '{source.AnimationKey}' uses a packed source; its metadata is preserved but this editor does not preview packed GANI entries yet.");
                continue;
            }

            try
            {
                var loaded = SceneAnimationSource.Load(
                    Document.ResolveReferencePath(source.GaniPath));

                if (!string.Equals(
                        loaded.Definition.Key,
                        source.AnimationKey,
                        StringComparison.Ordinal))
                {
                    _sourceDiagnostics.Add(
                        $"WARNING: GANI source '{source.AnimationKey}' resolves to definition '{loaded.Definition.Key}'.");
                }

                _animationSources.Add(loaded);
            }
            catch (Exception ex) when (IsAuthoringException(ex))
            {
                _sourceDiagnostics.Add(
                    $"WARNING: Could not load GANI source '{source.AnimationKey}': {ex.Message}");
            }
        }
    }

    private void RecoverLegacyLooseSources()
    {
        if (Document.FilePath is null)
            return;

        var requiredTilesheets = Definition.Layers
            .SelectMany(layer => layer.Tiles)
            .Select(tile => tile.Frame?.Tilesheet)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(name =>
                Definition.TilesheetSources.All(source =>
                    !string.Equals(source.Tilesheet, name, StringComparison.Ordinal)))
            .ToList();

        var requiredAnimations = Definition.Layers
            .SelectMany(layer => layer.Tiles)
            .Select(tile => tile.AnimationKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(key =>
                Definition.AnimationSources.All(source =>
                    !string.Equals(source.AnimationKey, key, StringComparison.Ordinal)))
            .ToList();

        if (requiredTilesheets.Count > 0)
            RecoverTilesheets(requiredTilesheets);

        if (requiredAnimations.Count > 0)
            RecoverAnimations(requiredAnimations);
    }

    private void RecoverTilesheets(IReadOnlyCollection<string> required)
    {
        var candidates = new Dictionary<string, List<SceneTilesheetSource>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(
                     Document.BaseDirectory,
                     "*.gts",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var loaded = SceneTilesheetSource.Load(path);
                if (!required.Contains(loaded.Definition.Name))
                {
                    loaded.Dispose();
                    continue;
                }

                if (!candidates.TryGetValue(loaded.Definition.Name, out var list))
                    candidates[loaded.Definition.Name] = list = [];
                list.Add(loaded);
            }
            catch (Exception ex) when (IsAuthoringException(ex))
            {
                _sourceDiagnostics.Add(
                    $"INFO: Ignored candidate GTS '{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        foreach (var name in required)
        {
            if (!candidates.TryGetValue(name, out var matches) || matches.Count == 0)
                continue;

            if (matches.Count != 1)
            {
                _sourceDiagnostics.Add(
                    $"WARNING: Multiple same-directory GTS files define '{name}'; legacy source recovery was skipped.");
                foreach (var source in matches)
                    source.Dispose();
                continue;
            }

            var recovered = matches[0];
            _tilesheetSources.Add(recovered);
            Document.SetLooseTilesheetSource(name, recovered.FilePath);
            _sourceDiagnostics.Add(
                $"INFO: Recovered legacy GTS source '{name}' from {Path.GetFileName(recovered.FilePath)}.");
        }
    }

    private void RecoverAnimations(IReadOnlyCollection<string> required)
    {
        var candidates = new Dictionary<string, List<SceneAnimationSource>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(
                     Document.BaseDirectory,
                     "*.gani",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var loaded = SceneAnimationSource.Load(path);
                if (!required.Contains(loaded.Definition.Key))
                {
                    loaded.Dispose();
                    continue;
                }

                if (!candidates.TryGetValue(loaded.Definition.Key, out var list))
                    candidates[loaded.Definition.Key] = list = [];
                list.Add(loaded);
            }
            catch (Exception ex) when (IsAuthoringException(ex))
            {
                _sourceDiagnostics.Add(
                    $"INFO: Ignored candidate GANI '{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        foreach (var key in required)
        {
            if (!candidates.TryGetValue(key, out var matches) || matches.Count == 0)
                continue;

            if (matches.Count != 1)
            {
                _sourceDiagnostics.Add(
                    $"WARNING: Multiple same-directory GANI files define '{key}'; legacy source recovery was skipped.");
                foreach (var source in matches)
                    source.Dispose();
                continue;
            }

            var recovered = matches[0];
            _animationSources.Add(recovered);
            Document.SetLooseAnimationSource(key, recovered.FilePath);
            _sourceDiagnostics.Add(
                $"INFO: Recovered legacy GANI source '{key}' from {Path.GetFileName(recovered.FilePath)}.");
        }
    }

    private void RemoveSelectedTilesheetSource()
    {
        var node = _sourceTree.SelectedNode;
        if (node is null)
            return;

        while (node.Parent is not null)
            node = node.Parent;

        if (node.Tag is not SceneTilesheetSource source)
            return;

        _tilesheetSources.Remove(source);
        Document.RemoveTilesheetSource(source.Definition.Name);
        source.Dispose();

        RefreshSourceTree();
        RefreshPreview();
        UpdateValidation();
    }

    private void RemoveSelectedAnimationSource()
    {
        if (_animationList.SelectedItems.Count != 1 ||
            _animationList.SelectedItems[0].Tag is not SceneAnimationSource source)
        {
            return;
        }

        _animationSources.Remove(source);
        Document.RemoveAnimationSource(source.Definition.Key);
        source.Dispose();

        RefreshAnimationList();
        RefreshPreview();
        UpdateValidation();
    }

    private void AssignFrame(FrameTag frame)
    {
        if (_selectedLayer is null)
            return;

        var tile = Document.GetOrCreateTile(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);

        tile.Frame = frame.Source.CreateFrame(
            frame.Region,
            frame.X,
            frame.Y);

        Document.MarkChanged();
        RefreshTileProperties();
        RefreshPreview();
        UpdateValidation();
    }

    private void ClearFrame()
    {
        if (_selectedLayer is null)
            return;

        var tile = Document.FindTile(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);

        if (tile?.Frame is null)
            return;

        tile.Frame = null;
        Document.MarkChanged();
        RefreshTileProperties();
        RefreshPreview();
        UpdateValidation();
    }

    private void AssignSelectedAnimation()
    {
        if (_selectedLayer is null ||
            _animationList.SelectedItems.Count != 1 ||
            _animationList.SelectedItems[0].Tag is not SceneAnimationSource source)
        {
            return;
        }

        var tile = Document.GetOrCreateTile(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);

        tile.AnimationKey = source.Definition.Key;
        Document.MarkChanged();

        RefreshTileProperties();
        RefreshPreview();
        UpdateValidation();
    }

    private void ClearAnimation()
    {
        if (_selectedLayer is null)
            return;

        var tile = Document.FindTile(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);

        if (tile is null ||
            (tile.AnimationKey is null && !tile.StartAnimation))
        {
            return;
        }

        tile.AnimationKey = null;
        tile.StartAnimation = false;
        Document.MarkChanged();

        RefreshTileProperties();
        RefreshPreview();
        UpdateValidation();
    }

    private void ClearSelectedTile()
    {
        if (_selectedLayer is null)
            return;

        if (Document.RemoveTile(
                _selectedLayer,
                (int)_tileX.Value,
                (int)_tileY.Value))
        {
            RefreshTileProperties();
            RefreshPreview();
            UpdateValidation();
        }
    }

    private void TileCoordinateChanged()
    {
        if (_syncingTileCoordinates || _selectedLayer is null)
            return;

        SetSelectedTile(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);
    }

    private void SetSelectedTile(
        SceneLayerDefinition layer,
        int x,
        int y)
    {
        _selectedLayer = layer;

        _syncingTileCoordinates = true;
        try
        {
            _tileX.Maximum = Math.Max(0, layer.Columns - 1);
            _tileY.Maximum = Math.Max(0, layer.Rows - 1);
            _tileX.Value = Math.Clamp(x, 0, (int)_tileX.Maximum);
            _tileY.Value = Math.Clamp(y, 0, (int)_tileY.Maximum);
        }
        finally
        {
            _syncingTileCoordinates = false;
        }

        RefreshTileProperties();
        _preview.SetSelection(
            layer,
            (int)_tileX.Value,
            (int)_tileY.Value);
    }

    private void ClampTileSelection()
    {
        if (_selectedLayer is null)
            return;

        if (_selectedLayer.Columns <= 0 || _selectedLayer.Rows <= 0)
        {
            _tileProperties.SelectedObject = null;
            _preview.SetSelection(_selectedLayer, 0, 0);
            return;
        }

        SetSelectedTile(
            _selectedLayer,
            Math.Min((int)_tileX.Value, _selectedLayer.Columns - 1),
            Math.Min((int)_tileY.Value, _selectedLayer.Rows - 1));
    }

    private void RefreshTileProperties()
    {
        if (_selectedLayer is null ||
            _selectedLayer.Columns <= 0 ||
            _selectedLayer.Rows <= 0)
        {
            _tileProperties.SelectedObject = null;
            return;
        }

        _tileProperties.SelectedObject = new TilePropertyAdapter(
            Document,
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value,
            () =>
            {
                _tileProperties.Refresh();
                RefreshPreview();
                UpdateValidation();
            });
    }

    private void StructureSelectionChanged()
    {
        _properties.SelectedObject = _structure.SelectedNode?.Tag;

        if (_structure.SelectedNode?.Tag is SceneLayerDefinition layer)
        {
            _selectedLayer = layer;
            SetSelectedTile(layer, 0, 0);
        }

        RefreshPreview();
    }

    private void RefreshView()
    {
        RefreshStructure();
        RefreshSourceTree();
        RefreshAnimationList();

        if (_selectedLayer is null ||
            !Definition.Layers.Contains(_selectedLayer))
        {
            _selectedLayer = Definition.Layers.FirstOrDefault();
        }

        if (_selectedLayer is not null)
        {
            SelectLayerNode(_selectedLayer);
            SetSelectedTile(_selectedLayer, 0, 0);
        }
        else
        {
            _properties.SelectedObject = Definition;
            _tileProperties.SelectedObject = null;
            _preview.SetSelection(null, 0, 0);
        }

        RefreshPreview();
        UpdateValidation();
    }

    private void RefreshStructure()
    {
        object? selected = _structure.SelectedNode?.Tag;

        _structure.BeginUpdate();
        try
        {
            _structure.Nodes.Clear();

            var root = new TreeNode(
                string.IsNullOrWhiteSpace(Definition.ID)
                    ? "(scene)"
                    : Definition.ID)
            {
                Tag = Definition
            };

            foreach (var layer in Definition.Layers
                         .OrderBy(layer => layer.ZOrder))
            {
                root.Nodes.Add(
                    new TreeNode(
                        $"{LayerLabel(layer)}  [{layer.Columns}×{layer.Rows}]  Z={layer.ZOrder}")
                    {
                        Tag = layer
                    });
            }

            _structure.Nodes.Add(root);
            root.Expand();
        }
        finally
        {
            _structure.EndUpdate();
        }

        if (selected is SceneLayerDefinition selectedLayer &&
            Definition.Layers.Contains(selectedLayer))
        {
            SelectLayerNode(selectedLayer);
        }
        else if (_structure.Nodes.Count > 0)
        {
            _structure.SelectedNode = _structure.Nodes[0];
        }
    }

    private void RefreshSourceTree()
    {
        _sourceTree.BeginUpdate();
        try
        {
            _sourceTree.Nodes.Clear();
            _sourceFrameImages.Images.Clear();

            foreach (var source in _tilesheetSources.OrderBy(
                         source => source.Definition.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                var root = new TreeNode(
                    $"{source.Definition.Name}  [{Path.GetFileName(source.FilePath)}]")
                {
                    Tag = source,
                    ToolTipText = source.FilePath
                };

                foreach (var region in source.Definition.Regions)
                {
                    var (columns, rows) =
                        TilesheetDefinitionValidator.GridSize(region);

                    var regionNode = new TreeNode(
                        $"{region.Name}  ({columns} × {rows})")
                    {
                        Tag = new RegionTag(source, region)
                    };

                    if (columns > 0 && rows > 0)
                        regionNode.Nodes.Add("Expand to load rows…");

                    root.Nodes.Add(regionNode);
                }

                _sourceTree.Nodes.Add(root);
                root.Expand();
            }
        }
        finally
        {
            _sourceTree.EndUpdate();
        }
    }

    private void RefreshAnimationList()
    {
        SceneAnimationSource? selected =
            _animationList.SelectedItems.Count == 1
                ? _animationList.SelectedItems[0].Tag as SceneAnimationSource
                : null;

        _animationList.BeginUpdate();
        try
        {
            _animationList.Items.Clear();

            foreach (var source in _animationSources.OrderBy(
                         source => source.Definition.Key,
                         StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem(source.Definition.Key)
                {
                    Tag = source
                };
                item.SubItems.Add(Path.GetFileName(source.FilePath));
                item.SubItems.Add(source.Definition.Frames.Count.ToString());
                _animationList.Items.Add(item);
            }
        }
        finally
        {
            _animationList.EndUpdate();
        }

        if (selected is not null)
            SelectAnimation(selected);
    }

    private void RefreshPreview()
    {
        _preview.Configure(
            Definition,
            FindTilesheet,
            FindAnimation);

        _preview.SetSelection(
            _selectedLayer,
            (int)_tileX.Value,
            (int)_tileY.Value);
    }

    private void SourceTreeBeforeExpand(
        object? sender,
        TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node is null ||
            node.Nodes.Count != 1 ||
            node.Nodes[0].Tag is not null)
        {
            return;
        }

        switch (node.Tag)
        {
            case RegionTag region:
                PopulateRows(node, region);
                break;

            case RowTag row:
                PopulateFrames(node, row);
                break;
        }
    }

    private static void PopulateRows(
        TreeNode node,
        RegionTag tag)
    {
        node.Nodes.Clear();

        var (columns, rows) =
            TilesheetDefinitionValidator.GridSize(tag.Region);

        if (columns <= 0 || rows <= 0)
            return;

        int rowCount = checked((int)Math.Min(rows, int.MaxValue));
        for (int y = 0; y < rowCount; y++)
        {
            var rowNode = new TreeNode($"Row {y}")
            {
                Tag = new RowTag(tag.Source, tag.Region, y)
            };
            rowNode.Nodes.Add("Expand to load frames…");
            node.Nodes.Add(rowNode);
        }
    }

    private void PopulateFrames(
        TreeNode node,
        RowTag tag)
    {
        node.Nodes.Clear();

        var (columns, _) =
            TilesheetDefinitionValidator.GridSize(tag.Region);

        int columnCount = checked((int)Math.Min(columns, int.MaxValue));
        for (int x = 0; x < columnCount; x++)
        {
            var frameTag = new FrameTag(
                tag.Source,
                tag.Region,
                x,
                tag.Y);

            var frameNode = new TreeNode($"Frame {x},{tag.Y}")
            {
                Tag = frameTag
            };

            int imageIndex = AddSourceFrameThumbnail(frameTag);
            if (imageIndex >= 0)
            {
                frameNode.ImageIndex = imageIndex;
                frameNode.SelectedImageIndex = imageIndex;
            }

            node.Nodes.Add(frameNode);
        }
    }

    private int AddSourceFrameThumbnail(FrameTag frame)
    {
        if (frame.Source.Image is not { } image)
            return -1;

        var sourceBounds = SceneTilesheetSource.FrameBounds(
            frame.Region,
            frame.X,
            frame.Y);

        if (sourceBounds.Width <= 0 ||
            sourceBounds.Height <= 0 ||
            sourceBounds.X < 0 ||
            sourceBounds.Y < 0 ||
            sourceBounds.Right > image.Width ||
            sourceBounds.Bottom > image.Height)
        {
            return -1;
        }

        var thumbnail = new Bitmap(
            SourceFrameThumbnailSize,
            SourceFrameThumbnailSize);

        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;

            float scale = Math.Min(
                (float)SourceFrameThumbnailSize / sourceBounds.Width,
                (float)SourceFrameThumbnailSize / sourceBounds.Height);

            int width = Math.Max(
                1,
                (int)Math.Round(sourceBounds.Width * scale));
            int height = Math.Max(
                1,
                (int)Math.Round(sourceBounds.Height * scale));

            var destination = new Rectangle(
                (SourceFrameThumbnailSize - width) / 2,
                (SourceFrameThumbnailSize - height) / 2,
                width,
                height);

            graphics.DrawImage(
                image,
                destination,
                sourceBounds,
                GraphicsUnit.Pixel);
        }

        _sourceFrameImages.Images.Add(thumbnail);
        thumbnail.Dispose();
        return _sourceFrameImages.Images.Count - 1;
    }

    private SceneTilesheetSource? FindTilesheet(string logicalName) =>
        _tilesheetSources.FirstOrDefault(source =>
            string.Equals(
                source.Definition.Name,
                logicalName,
                StringComparison.Ordinal));

    private SceneAnimationSource? FindAnimation(string key) =>
        _animationSources.FirstOrDefault(source =>
            string.Equals(
                source.Definition.Key,
                key,
                StringComparison.Ordinal));

    private void SelectLayerNode(SceneLayerDefinition layer)
    {
        if (_structure.Nodes.Count == 0)
            return;

        foreach (TreeNode node in _structure.Nodes[0].Nodes)
        {
            if (!ReferenceEquals(node.Tag, layer))
                continue;

            _structure.SelectedNode = node;
            node.EnsureVisible();
            return;
        }
    }

    private void SelectTilesheetRoot(SceneTilesheetSource source)
    {
        foreach (TreeNode node in _sourceTree.Nodes)
        {
            if (!ReferenceEquals(node.Tag, source))
                continue;

            _sourceTree.SelectedNode = node;
            node.EnsureVisible();
            return;
        }
    }

    private void SelectAnimation(SceneAnimationSource source)
    {
        foreach (ListViewItem item in _animationList.Items)
        {
            if (!ReferenceEquals(item.Tag, source))
                continue;

            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            return;
        }
    }

    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (_refreshPending || !IsHandleCreated)
            return;

        _refreshPending = true;
        BeginInvoke(() =>
        {
            _refreshPending = false;
            if (!IsDisposed)
            {
                _properties.Refresh();
                _tileProperties.Refresh();
                RefreshStructure();
                RefreshPreview();
                UpdateValidation();
            }
        });
    }

    private static string LayerLabel(SceneLayerDefinition layer) =>
        string.IsNullOrWhiteSpace(layer.ID)
            ? "(layer)"
            : layer.ID;

    private static bool IsAuthoringException(Exception ex) =>
        ex is IOException or
        InvalidDataException or
        ArgumentException or
        UnauthorizedAccessException or
        InvalidOperationException or
        NotSupportedException;

    private void ShowError(Exception ex) =>
        MessageBox.Show(
            this,
            ex.Message,
            "GSCN editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.Changed -= DocumentChanged;

            foreach (var source in _tilesheetSources)
                source.Dispose();
            _tilesheetSources.Clear();

            foreach (var source in _animationSources)
                source.Dispose();
            _animationSources.Clear();

            _sourceFrameImages.Dispose();
        }

        base.Dispose(disposing);
    }

    private readonly record struct RegionTag(
        SceneTilesheetSource Source,
        TilesheetRegionDefinition Region);

    private readonly record struct RowTag(
        SceneTilesheetSource Source,
        TilesheetRegionDefinition Region,
        int Y);

    private readonly record struct FrameTag(
        SceneTilesheetSource Source,
        TilesheetRegionDefinition Region,
        int X,
        int Y);
}
