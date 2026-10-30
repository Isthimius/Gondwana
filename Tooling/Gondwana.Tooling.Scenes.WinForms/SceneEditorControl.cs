using System.Drawing.Drawing2D;
using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Shared standalone and Studio editor for the logical definitions in a GSCN document.</summary>
public sealed partial class SceneEditorControl : UserControl
{
    /// <summary>Validates and adds one packed GTS member with its exact entry identity and preview.</summary>
    public void AddPackedTilesheetSource(PackedResourceSelection selection)
    {
        _preview.CancelGesture();
        selection.Validate(_packages, AssetTypes.TilesheetDefinition);
        var resource = selection.Resource;
        var source = SceneTilesheetSource.LoadPacked(resource.Path, resource.EntryName!, selection.Member!, _packages);
        try { Document.AddPackedTilesheetSource(selection.Member!, resource.Path, resource.EntryName!); }
        catch { source.Dispose(); throw; }
        _tilesheetSources.Add(source);
        RefreshSourceTree();
        RefreshPreview();
        UpdateValidation();
        SelectTilesheetRoot(source);
    }

    /// <summary>Validates and adds one packed GANI member and its representative frames.</summary>
    public void AddPackedAnimationSource(PackedResourceSelection selection)
    {
        _preview.CancelGesture();
        selection.Validate(_packages, AssetTypes.AnimationDefinition);
        var resource = selection.Resource;
        var source = SceneAnimationSource.LoadPacked(resource.Path, resource.EntryName!, selection.Member!, _packages);
        try { Document.AddPackedAnimationSource(selection.Member!, resource.Path, resource.EntryName!); }
        catch { source.Dispose(); throw; }
        _animationSources.Add(source);
        RefreshAnimationList();
        RefreshPreview();
        UpdateValidation();
        SelectAnimation(source);
    }

    private const int SourceFrameThumbnailSize = 32;
    private enum PaintSourceKind { Frame, Animation }
    private PaintSourceKind? _paintSourceKind;
    private bool _restoringSourceSelection;
    private readonly ToolStripLabel _paintSourceLabel = new()
    {
        AutoSize = false,
        Width = 220,
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Dictionary<SceneEditorTool, ToolStripButton> _toolButtons = [];
    internal SceneEditorTool ActiveTool { get; private set; } = SceneEditorTool.Select;

    /// <summary>Gets the pane titles accepted by the pane visibility methods.</summary>
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
        ShowNodeToolTips = true,
        CheckBoxes = true
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
    private readonly Dictionary<string, DockContent> _panes =
        new(StringComparer.OrdinalIgnoreCase);
    private EditorDockWorkspace _workspace = null!;

    private SceneLayerDefinition? _selectedLayer;
    private readonly Dictionary<SceneLayerDefinition, bool> _previewLayerVisibility = [];
    private bool _syncingStructureChecks;
    private bool _syncingTileCoordinates;
    private bool _refreshPending;

    /// <summary>Gets the mutable authoring document displayed by this editor.</summary>
    public SceneDocument Document { get; }
    private readonly AssetPackageCatalog _packages;
    private readonly bool _ownsPackages;
    /// <summary>Gets the mutable definition currently selected in the document.</summary>
    public SceneDefinition Definition => Document.Definition;

    /// <summary>Gets or sets the optional synchronous save callback used before launching the separate Scene Viewer; true indicates a successful save.</summary>
    public Func<bool>? SaveRequested { get; set; }
    /// <summary>Gets or sets the asynchronous save callback used before launching the separate Scene Viewer; takes precedence over the synchronous callback and must return true on success.</summary>
    public Func<Task<bool>>? SaveRequestedAsync { get; set; }

    /// <summary>Creates an editor with its own asset-package cache.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public SceneEditorControl(SceneDocument document) : this(document, null) { }

    /// <summary>Creates an editor and loads its declared sources for previews.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <param name="packages">A borrowed package cache, or null to create a cache owned by the control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public SceneEditorControl(SceneDocument document, AssetPackageCatalog? packages)
    {
        _packages = packages ?? PackedResourcePicker.CreateCatalog(this);
        _ownsPackages = packages is null;
        Document = document ?? throw new ArgumentNullException(nameof(document));
        var shortcuts = UndoRedoMenu.Route(this, () => Document.History, CommitEdits);
        Disposed += (_, _) => shortcuts.Dispose();
        Size = new Size(1400, 900);
        Dock = DockStyle.Fill;

        _sourceTree.ImageList = _sourceFrameImages;
        _sourceTree.BeforeExpand += SourceTreeBeforeExpand;
        _sourceTree.AfterSelect += (_, _) =>
        {
            if (_restoringSourceSelection) return;
            _paintSourceKind = PaintSourceKind.Frame;
            UpdatePaintSourceLabel();
        };
        _sourceTree.Enter += (_, _) =>
        {
            if (_restoringSourceSelection) return;
            _paintSourceKind = PaintSourceKind.Frame;
            UpdatePaintSourceLabel();
        };
        _animationList.Enter += (_, _) =>
        {
            if (_restoringSourceSelection) return;
            _paintSourceKind = PaintSourceKind.Animation;
            UpdatePaintSourceLabel();
        };
        _animationList.SelectedIndexChanged += (_, _) =>
        {
            if (_restoringSourceSelection) return;
            if (_animationList.SelectedItems.Count == 1)
                _paintSourceKind = PaintSourceKind.Animation;
            UpdatePaintSourceLabel();
        };
        _sourceTree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node.Tag is FrameTag frame)
                AssignFrame(frame);
        };

        _structure.AfterSelect += (_, _) => StructureSelectionChanged();
        _structure.AfterCheck += StructureLayerCheckChanged;

        _animationList.Columns.Add("Animation key", 210);
        _animationList.Columns.Add("Source", 260);
        _animationList.Columns.Add("Frames", 60, HorizontalAlignment.Right);
        _animationList.ItemActivate += (_, _) => AssignSelectedAnimation();

        _preview.GestureStarting += BeginPreviewGesture;
        _preview.TileSelected += PreviewCellClicked;
        _preview.GestureEnded += EndPreviewGesture;
        Document.EditingContextChanging += EditingContextChanging;

        _properties.PropertyValueChanged += (_, _) =>
        {
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

        var definitionBar = new DefinitionSelectionBar<SceneDefinition>(
            () => Document.Definitions, () => Document.SelectedDefinition, definition => definition.ID,
            Document.SelectDefinition, () => Document.AddDefinition(), () => Document.DuplicateDefinition(),
            () => Document.RemoveDefinition(Document.SelectedDefinition), Document.RenameDefinition, RefreshSelectedDefinition,
            "gscn", (path, select) => Document.ImportDefinitions(path, select));
        Controls.Add(definitionBar);
        Document.Changed += definitionBar.RefreshMembers;
        definitionBar.Disposed += (_, _) => Document.Changed -= definitionBar.RefreshMembers;
        NavigationSizing.Attach(_structure);
        NavigationSizing.Attach(_sourceTree);
        ToolingAppearance.Apply(this);
        RefreshView();
        _displayedDefinition = Definition; _displayedSources = SourceSignature();
    }

    /// <summary>Loads the members of loose tilesheet files for authoring and records their source paths in the document.</summary>
    /// <param name="paths">Definition files to load; relative paths use the process's current directory.</param>
    /// <remarks>All source-reference changes form one transaction; a load failure cancels the group.</remarks>
    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        _preview.CancelGesture();
        ArgumentNullException.ThrowIfNull(paths);
        using var transaction = Document.History.BeginTransaction("Add tilesheet sources");
        // Source previews are derived state; rebuild them if loading or cancellation changes the group.
        _displayedSources = null;


        SceneTilesheetSource? last = null;
        foreach (var path in paths)
            foreach (var source in SceneTilesheetSource.LoadAll(path, _packages))
            {
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
        transaction.Commit();
    }

    /// <summary>Loads the members of loose animation files for authoring and records their source paths in the document.</summary>
    /// <param name="paths">Definition files to load; relative paths use the process's current directory.</param>
    /// <remarks>All source-reference changes form one transaction; a load failure cancels the group.</remarks>
    public void AddAnimationSources(IEnumerable<string> paths)
    {
        _preview.CancelGesture();
        ArgumentNullException.ThrowIfNull(paths);
        using var transaction = Document.History.BeginTransaction("Add animation sources");
        // Source previews are derived state; rebuild them if loading or cancellation changes the group.
        _displayedSources = null;


        SceneAnimationSource? last = null;
        foreach (var path in paths)
            foreach (var source in SceneAnimationSource.LoadAll(path, _packages))
            {
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
        transaction.Commit();
    }

    /// <summary>Validates the document and refreshes the diagnostics pane.</summary>
    /// <returns>The document's structural validation errors. Source and preview warnings shown in the pane are not included.</returns>
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

    /// <summary>Moves focus out of the property editor and validates pending control input without saving the document.</summary>
    /// <returns><see langword="true"/> if control validation succeeds and the property editor releases focus; otherwise, <see langword="false"/>.</returns>
    public bool CommitEdits()
    {
        _preview.CancelGesture();
        _validation.Focus();
        return !_properties.ContainsFocus &&
               !_tileProperties.ContainsFocus &&
               ValidateChildren();
    }

    /// <summary>
    /// Shows the named editor pane.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the named pane was found; otherwise, <see langword="false"/>.</returns>
    public bool ShowPane(string paneName)
    {
        if (!_panes.TryGetValue(paneName, out var pane) ||
            pane.IsDisposed)
        {
            return false;
        }

        pane.Show();
        pane.Activate();
        return true;
    }

    /// <summary>
    /// Determines whether the named editor pane is visible.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the pane is visible; otherwise, <see langword="false"/>.</returns>
    public bool IsPaneVisible(string paneName) =>
        _panes.TryGetValue(paneName, out var pane) &&
        !pane.IsDisposed &&
        !pane.IsHidden;

    /// <summary>Restore the default pane arrangement for this editor type.</summary>
    public void ResetLayout() => _workspace.ResetLayout();

    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes()
    {
        foreach (var pane in _panes.Values)
        {
            if (!pane.IsDisposed)
                pane.Show();
        }
    }

    private void BuildLayout()
    {
        _workspace = new EditorDockWorkspace("gscn");
        Controls.Add(_workspace);

        var dock = _workspace.DockPanel;
        var structure = _workspace.AddPane("scene-structure", "Scene structure",
            _structure,
            BuildStructureToolbar());

        var preview = _workspace.AddPane("scene-preview", "Scene preview",
            _preview,
            BuildPreviewToolbar());

        var gts = _workspace.AddPane("gts-sources", "GTS frame sources",
            _sourceTree,
            BuildGtsToolbar());

        var animations = _workspace.AddPane("gani-animations", "GANI animations",
            _animationList,
            BuildAnimationToolbar());

        var properties = _workspace.AddPane("properties", "Properties",
            _properties);

        var tileProperties = _workspace.AddPane("tile-properties", "Tile properties",
            _tileProperties,
            BuildTileToolbar());

        var validation = _workspace.AddPane("validation", "Validation",
            _validation);

        _panes.Clear();
        foreach (var pane in new[]
                 {
                     structure,
                     preview,
                     gts,
                     animations,
                     properties,
                     tileProperties,
                     validation
                 })
        {
            _panes.Add(pane.Text, pane);
        }

        _workspace.Place(preview, () => preview.Show(dock, DockState.Document));
        _workspace.Place(structure, () => structure.Show(preview.Pane, DockAlignment.Left, 300d / 1400));
        _workspace.Place(properties, () => properties.Show(preview.Pane, DockAlignment.Right, 330d / 1100));
        _workspace.Place(tileProperties, () => tileProperties.Show(properties.Pane, DockAlignment.Bottom, .52));
        _workspace.Place(gts, () => gts.Show(preview.Pane, DockAlignment.Bottom, .34));
        _workspace.Place(animations, () => animations.Show(gts.Pane, DockAlignment.Right, .34));
        _workspace.Place(validation, () => validation.Show(tileProperties.Pane, DockAlignment.Bottom, .28));
        _workspace.InitializeLayout();
    }

    private ToolStrip BuildPreviewToolbar()
    {
        var bar = NewToolbar();
        foreach (var tool in Enum.GetValues<SceneEditorTool>())
        {
            var button = new ToolStripButton(tool.ToString())
            {
                Checked = tool == ActiveTool,
                ToolTipText = tool switch
                {
                    SceneEditorTool.Select => "Click a cell or drag to select a grid rectangle",
                    SceneEditorTool.Paint => "Drag to paint with the last-selected GTS frame or GANI animation",
                    _ => "Drag to erase complete explicit tile definitions"
                }
            };
            button.Click += (_, _) =>
            {
                _preview.CancelGesture();
                ClearMultiSelection();
                ActiveTool = tool;
                _preview.RequireSelectedLayer = tool != SceneEditorTool.Select;
                foreach (var (mode, item) in _toolButtons) item.Checked = mode == tool;
            };
            _toolButtons.Add(tool, button);
            bar.Items.Add(button);
        }
        bar.Items.Add(new ToolStripSeparator());

        bar.Items.Add("−", null, (_, _) => _preview.ZoomOut());

        var zoom = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 75,
            BackColor = ToolingAppearance.Palette.Background,
            ForeColor = ToolingAppearance.Palette.Foreground,
            FlatStyle = FlatStyle.Flat
        };

        var zoomSelector = new ZoomSelector(zoom.ComboBox, requested =>
        {
            if (requested is { } value) _preview.SetZoom(value);
            else _preview.Fit();
        });
        _preview.ZoomChanged += () => zoomSelector.Show(_preview.Zoom, _preview.IsFit);

        bar.Items.Add(zoom);
        bar.Items.Add("+", null, (_, _) => _preview.ZoomIn());
        bar.Items.Add(new ToolStripSeparator());

        var grid = new ToolStripButton("Grid")
        {
            CheckOnClick = true,
            Checked = true,
            ToolTipText = "Show or hide scene preview grid lines"
        };

        grid.CheckedChanged += (_, _) =>
        {
            _preview.ShowGridLines = grid.Checked;
            _preview.Invalidate();
        };

        bar.Items.Add(grid);
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("View Scene", null, async (_, _) => await ViewSceneAsync());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(_paintSourceLabel);
        UpdatePaintSourceLabel();
        return bar;
    }

    /// <summary>Launches the existing Scene Viewer workflow for the current definition, including packed-scene snapshots and save prompts.</summary>
    public async Task ViewSceneAsync()
    {
        if (!CommitEdits())
            return;
        try
        {
            if (Document.PackedLocation is not null)
            {
                var snapshot = SceneViewerLauncher.CreateTemporaryScene(Document);
                try
                {
                    SceneViewerLauncher.Launch(snapshot,
                        string.IsNullOrWhiteSpace(Document.SelectedDefinition.ID) ? null : Document.SelectedDefinition.ID,
                        Document.BaseDirectory, temporaryScene: true);
                }
                catch
                {
                    File.Delete(snapshot);
                    throw;
                }
                return;
            }
            if (SaveRequestedAsync is not null)
            {
                if (Document.IsDirty || Document.FilePath is null)
                {
                    if (ThemedMessageBox.Show(this, "Save and view the scene?", "View Scene", MessageBoxButtons.OKCancel) != DialogResult.OK ||
                        !await SaveRequestedAsync()) return;
                }
                if (Document.FilePath is { } savedPath && !Document.IsDirty)
                    SceneViewerLauncher.Launch(savedPath, Document.SelectedDefinition.ID);
                return;
            }
            SceneViewerLauncher.ViewSavedScene(Document, () =>
                ThemedMessageBox.Show(this,
                    "This scene must be saved before viewing. Save and view the scene?",
                    "View Scene — saved content", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question) == DialogResult.OK,
                () => SaveRequested?.Invoke() ?? false,
                path => SceneViewerLauncher.Launch(path, Document.SelectedDefinition.ID));
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, ex.Message, "Unable to view scene",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Show all", null, (_, _) => SetAllPreviewLayersVisible(true))
            .ToolTipText = "Show every layer in the editor preview";
        bar.Items.Add("Hide all", null, (_, _) => SetAllPreviewLayersVisible(false))
            .ToolTipText = "Hide every layer in the editor preview";
        return bar;
    }

    private ToolStrip BuildGtsToolbar()
    {
        var bar = NewToolbar();
        bar.Items.Add("Reload sources", null, (_, _) => ReloadSources());
        bar.Items.Add("Add GTS…", null, (_, _) => ChooseTilesheetSources());
        bar.Items.Add("Add from GAF…", null, (_, _) => PackedResourcePicker.PickAndApply(
            this, _packages, Document.BaseDirectory, AssetTypes.TilesheetDefinition, AddPackedTilesheetSource));
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
        bar.Items.Add("Add from GAF…", null, (_, _) => PackedResourcePicker.PickAndApply(
            this, _packages, Document.BaseDirectory, AssetTypes.AnimationDefinition, AddPackedAnimationSource));
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
        foreach (var (label, operation) in new[]
        {
            ("Rotate left 90°", TileTransform.Rotate270),
            ("Rotate right 90°", TileTransform.Rotate90),
            ("Flip horizontal", TileTransform.FlipHorizontal),
            ("Flip vertical", TileTransform.FlipVertical)
        })
        {
            var button = bar.Items.Add(label, null, (_, _) =>
            {
                if (_tileProperties.SelectedObject is TilePropertyAdapter adapter) adapter.ApplyTransform(operation);
            });
            button.ToolTipText = label;
        }
        bar.Items.Add("Clear tile", null, (_, _) => ClearSelectedTile());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Apply to selection", null, (_, _) => EditSelectedCells(erase: false))
            .ToolTipText = "Assign the selected source to all selected cells as one undo operation";
        bar.Items.Add("Erase selection", null, (_, _) => EditSelectedCells(erase: true))
            .ToolTipText = "Remove explicit tiles in the selection as one undo operation";
        bar.Items.Add(_selectionLabel);
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

    /// <summary>Clears the package cache, releases loaded previews, and reloads the document's declared sources.</summary>
    /// <remarks>Also clears a cache supplied by the host. Does not save or change the authoring document.</remarks>
    public void ReloadSources()
    {
        _preview.CancelGesture();
        ClearMultiSelection();
        _packages.Clear();
        foreach (var source in _tilesheetSources) source.Dispose();
        foreach (var source in _animationSources) source.Dispose();
        _tilesheetSources.Clear(); _animationSources.Clear(); _sourceDiagnostics.Clear();
        LoadDeclaredSources();
        RefreshView();
    }

    private void LoadDeclaredSources()
    {
        foreach (var source in Definition.TilesheetSources)
        {
            if (source.Kind != SceneTilesheetSourceKind.LooseDefinitionFile ||
                string.IsNullOrWhiteSpace(source.GtsPath))
            {
                try
                {
                    _tilesheetSources.Add(SceneTilesheetSource.LoadPacked(Document.ResolveReferencePath(source.AssetsFilePath!), source.AssetEntryName!, source.Tilesheet, _packages));
                }
                catch (Exception ex) { _sourceDiagnostics.Add(ex.Message); }
                continue;
            }

            try
            {
                var loaded = SceneTilesheetSource.LoadMember(
                    Document.ResolveReferencePath(source.GtsPath), source.Tilesheet, _packages);

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
                try
                {
                    _animationSources.Add(SceneAnimationSource.LoadPacked(Document.ResolveReferencePath(source.AssetsFilePath!), source.AssetEntryName!, source.AnimationKey, _packages));
                }
                catch (Exception ex) { _sourceDiagnostics.Add(ex.Message); }
                continue;
            }

            try
            {
                var loaded = SceneAnimationSource.LoadMember(
                    Document.ResolveReferencePath(source.GaniPath), source.AnimationKey, _packages);

                if (!string.Equals(
                        loaded.Definition.Key,
                        source.AnimationKey,
                        StringComparison.OrdinalIgnoreCase))
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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(key =>
                Definition.AnimationSources.All(source =>
                    !string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase)))
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
                foreach (var loaded in SceneTilesheetSource.LoadAll(path, _packages))
                {
                    if (!required.Contains(loaded.Definition.Name))
                    {
                        loaded.Dispose();
                        continue;
                    }

                    if (!candidates.TryGetValue(loaded.Definition.Name, out var list))
                        candidates[loaded.Definition.Name] = list = [];
                    list.Add(loaded);
                }
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
        var candidates = new Dictionary<string, List<SceneAnimationSource>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(
                     Document.BaseDirectory,
                     "*.gani",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                foreach (var loaded in SceneAnimationSource.LoadAll(path, _packages))
                {
                    if (!required.Contains(loaded.Definition.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        loaded.Dispose();
                        continue;
                    }

                    if (!candidates.TryGetValue(loaded.Definition.Key, out var list))
                        candidates[loaded.Definition.Key] = list = [];
                    list.Add(loaded);
                }
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
        _preview.CancelGesture();
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
        _preview.CancelGesture();
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

    private FrameTag? SelectedPaintFrame()
    {
        if (_sourceTree.SelectedNode?.Tag is not FrameTag frame ||
            !_tilesheetSources.Contains(frame.Source) ||
            string.IsNullOrWhiteSpace(frame.Source.Definition.Name) ||
            string.IsNullOrWhiteSpace(frame.Region.Name)) return null;
        var definition = frame.Source.CreateFrame(frame.Region, frame.X, frame.Y);
        return frame.Source.TryResolve(definition, out _, out _) ? frame : null;
    }

    private SceneAnimationSource? SelectedPaintAnimation() =>
        _animationList.SelectedItems.Count == 1 &&
        _animationList.SelectedItems[0].Tag is SceneAnimationSource source &&
        _animationSources.Contains(source) &&
        AnimationDefinitionValidator.Validate(source.Definition).Count == 0 ? source : null;

    private void UpdatePaintSourceLabel()
    {
        _paintSourceLabel.Text = _paintSourceKind switch
        {
            PaintSourceKind.Frame when SelectedPaintFrame() is { } frame =>
                $"Paint: GTS {frame.Source.Definition.Name}:{frame.Region.Name} ({frame.X},{frame.Y})",
            PaintSourceKind.Animation when SelectedPaintAnimation() is { } animation =>
                $"Paint: GANI {animation.Definition.Key}",
            _ => "Paint unavailable: select a valid GTS frame or GANI animation"
        };
        _paintSourceLabel.ToolTipText = _paintSourceLabel.Text +
            "\nPaint uses the last-selected source pane. Select a frame or animation there to change the source.";
    }

    private void AssignFrame(FrameTag frame)
    {
        if (_selectedLayer is null)
            return;

        Document.EditTile(_selectedLayer, (int)_tileX.Value, (int)_tileY.Value, "Assign frame",
            FrameAssignment(frame));
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

        Document.Edit(tile, "Clear frame", () => tile.Frame = null);
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

        Document.EditTile(_selectedLayer, (int)_tileX.Value, (int)_tileY.Value, "Assign animation", AnimationAssignment(source));

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

        Document.Edit(tile, "Clear animation", () => { tile.AnimationKey = null; tile.StartAnimation = false; });

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
        int y,
        bool preserveSelection = false)
    {
        if (!ReferenceEquals(_selectedLayer, layer))
        {
            _preview.CancelGesture();
            preserveSelection = false;
        }
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

        if (!preserveSelection || _selectedCells.IsEmpty)
            _selectedCells = new Rectangle((int)_tileX.Value, (int)_tileY.Value, 1, 1);
        _selectedCells.Intersect(new Rectangle(0, 0, layer.Columns, layer.Rows));
        UpdateSelectionDisplay();
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
            _selectedCells = Rectangle.Empty;
            UpdateSelectionDisplay();
            _tileProperties.SelectedObject = null;
            _preview.SetSelection(_selectedLayer, 0, 0);
            return;
        }

        SetSelectedTile(
            _selectedLayer,
            Math.Min((int)_tileX.Value, _selectedLayer.Columns - 1),
            Math.Min((int)_tileY.Value, _selectedLayer.Rows - 1), preserveSelection: true);
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
            },
            () =>
            {
                var tile = Document.FindTile(_selectedLayer, (int)_tileX.Value, (int)_tileY.Value);
                var animation = tile?.AnimationKey is { } key ? FindAnimation(key) : null;
                var frame = animation?.FirstPreviewFrame() ?? tile?.Frame;
                if (frame is null) return (null, null);
                var source = animation?.Definition.TilesheetSources.Any(item => item.Tilesheet == frame.Tilesheet) == true
                    ? animation.FindTilesheet(frame.Tilesheet) : FindTilesheet(frame.Tilesheet) ?? animation?.FindTilesheet(frame.Tilesheet);
                return (source?.TryResolve(frame, out var region, out _) == true ? region : null, frame);
            });
    }

    private bool IsLayerVisibleInPreview(SceneLayerDefinition layer) =>
        _previewLayerVisibility.TryGetValue(layer, out bool shown) ? shown : layer.Visible;

    private void SetAllPreviewLayersVisible(bool shown)
    {
        foreach (var layer in Definition.Layers)
            _previewLayerVisibility[layer] = shown;

        _syncingStructureChecks = true;
        try
        {
            foreach (TreeNode node in _structure.Nodes[0].Nodes)
            {
                if (node.Tag is SceneLayerDefinition layer)
                    node.Checked = IsLayerVisibleInPreview(layer);
            }

            _structure.Nodes[0].Checked =
                Definition.Layers.Count > 0 && Definition.Layers.All(IsLayerVisibleInPreview);
        }
        finally
        {
            _syncingStructureChecks = false;
        }
        RefreshPreview();
    }

    private void StructureLayerCheckChanged(object? sender, TreeViewEventArgs e)
    {
        if (_syncingStructureChecks)
            return;

        if (e.Node.Tag is SceneLayerDefinition layer)
        {
            _previewLayerVisibility[layer] = e.Node.Checked;
        }
        else if (ReferenceEquals(e.Node.Tag, Definition))
        {
            // The scene checkbox is a bulk toggle, not an authored Visible property.
            foreach (var current in Definition.Layers)
                _previewLayerVisibility[current] = e.Node.Checked;
        }
        else
        {
            return;
        }

        _syncingStructureChecks = true;
        try
        {
            foreach (TreeNode node in _structure.Nodes[0].Nodes)
            {
                if (node.Tag is SceneLayerDefinition current)
                    node.Checked = IsLayerVisibleInPreview(current);
            }

            // A partially visible scene has an unchecked "all layers" box.
            _structure.Nodes[0].Checked =
                Definition.Layers.Count > 0 && Definition.Layers.All(IsLayerVisibleInPreview);
        }
        finally
        {
            _syncingStructureChecks = false;
        }

        RefreshPreview();
    }

    private void StructureSelectionChanged()
    {
        if (_refreshingStructure) return;
        _properties.SelectedObject = _structure.SelectedNode?.Tag is { } target ? new ScenePropertyView(Document, target) : null;

        if (_structure.SelectedNode?.Tag is SceneLayerDefinition layer)
        {
            if (!ReferenceEquals(_selectedLayer, layer)) _preview.CancelGesture();
            SetSelectedTile(layer, 0, 0);
        }

        RefreshPreview();
    }

    /// <summary>Selects an owned definition and refreshes its source previews and controls without reloading or saving the document.</summary>
    public void SelectDefinition(SceneDefinition definition)
    {
        if (ReferenceEquals(Document.SelectedDefinition, definition)) return;
        Document.SelectDefinition(definition);
        RefreshSelectedDefinition();
    }

    private void RefreshSelectedDefinition()
    {
        _preview.CancelGesture();
        ClearMultiSelection();
        foreach (var source in _tilesheetSources) source.Dispose();
        foreach (var source in _animationSources) source.Dispose();
        _tilesheetSources.Clear(); _animationSources.Clear(); _sourceDiagnostics.Clear();
        LoadDeclaredSources(); RefreshView();
        _displayedDefinition = Definition; _displayedSources = SourceSignature();
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
            _selectedCells = Rectangle.Empty;
            UpdateSelectionDisplay();
            _properties.SelectedObject = new ScenePropertyView(Document, Definition);
            _tileProperties.SelectedObject = null;
            _preview.SetSelection(null, 0, 0);
        }

        RefreshPreview();
        UpdateValidation();
    }

    private void RefreshStructure()
    {
        _refreshingStructure = true;
        try
        {
            object? selected = _structure.SelectedNode?.Tag;

            // Discard viewer-only state for deleted layers and removed scene members.
            var currentLayers = Document.Definitions.SelectMany(definition => definition.Layers).ToHashSet();
            foreach (var stale in _previewLayerVisibility.Keys.Where(layer => !currentLayers.Contains(layer)).ToArray())
                _previewLayerVisibility.Remove(stale);

            _syncingStructureChecks = true;
            _structure.BeginUpdate();
            try
            {
                _structure.Nodes.Clear();

                var root = new TreeNode(
                    string.IsNullOrWhiteSpace(Definition.ID)
                        ? "(scene)"
                        : Definition.ID)
                {
                    Tag = Definition,
                    Checked = Definition.Layers.Count > 0 && Definition.Layers.All(IsLayerVisibleInPreview),
                    ToolTipText = "Check to show all layers; uncheck to hide all layers in the preview"
                };

                foreach (var layer in Definition.Layers
                             .OrderBy(layer => layer.ZOrder))
                {
                    root.Nodes.Add(
                        new TreeNode(
                            $"{LayerLabel(layer)}  [{layer.Columns}×{layer.Rows}]  Z={layer.ZOrder}")
                        {
                            Tag = layer,
                            Checked = IsLayerVisibleInPreview(layer),
                            ToolTipText = "Toggle this layer in the preview only; does not change the GSCN"
                        });
                }

                _structure.Nodes.Add(root);
                root.Expand();
            }
            finally
            {
                _structure.EndUpdate();
                _syncingStructureChecks = false;
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
            _properties.SelectedObject = _structure.SelectedNode?.Tag is { } target ? new ScenePropertyView(Document, target) : null;
        }
        finally { _refreshingStructure = false; }
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
            NavigationSizing.RefreshImages(_sourceTree);
            _sourceTree.EndUpdate();
            UpdatePaintSourceLabel();
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
        UpdatePaintSourceLabel();
    }

    private void RefreshPreview()
    {
        UpdateSelectionDisplay();
        _preview.Configure(
            Definition,
            FindTilesheet,
            FindAnimation,
            IsLayerVisibleInPreview);

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
                NavigationSizing.RefreshImages(_sourceTree);
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

        _ = _sourceFrameImages.Handle;
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
                StringComparison.OrdinalIgnoreCase));

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

    private SceneDefinition? _displayedDefinition;
    private string? _displayedSources;
    private bool _refreshingStructure;

    private string SourceSignature() => Newtonsoft.Json.JsonConvert.SerializeObject(new { Definition.TilesheetSources, Definition.AnimationSources });

    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (_gestureLayer is not null && !GestureTargetIsValid()) _preview.CancelGesture();
        if (_refreshPending || !IsHandleCreated)
            return;

        _refreshPending = true;
        BeginInvoke(() =>
        {
            _refreshPending = false;
            if (!IsDisposed)
            {
                string sources = SourceSignature();
                if (!ReferenceEquals(_displayedDefinition, Definition) || _displayedSources != sources)
                {
                    // Restoring pane selection must not choose a different paint source kind.
                    _restoringSourceSelection = true;
                    try
                    {
                        var selectedSourceNode = _sourceTree.SelectedNode;
                        while (selectedSourceNode?.Parent is { } parent) selectedSourceNode = parent;
                        string? selectedTilesheet = (selectedSourceNode?.Tag as SceneTilesheetSource)?.Definition.Name;
                        string? selectedAnimation = _animationList.SelectedItems.Cast<ListViewItem>().Select(item => (item.Tag as SceneAnimationSource)?.Definition.Key).FirstOrDefault();
                        foreach (var source in _tilesheetSources) source.Dispose();
                        foreach (var source in _animationSources) source.Dispose();
                        _tilesheetSources.Clear(); _animationSources.Clear(); _sourceDiagnostics.Clear();
                        LoadDeclaredSources(); RefreshSourceTree(); RefreshAnimationList();
                        if (selectedTilesheet is not null && FindTilesheet(selectedTilesheet) is { } selectedTilesheetSource)
                            SelectTilesheetRoot(selectedTilesheetSource);
                        if (selectedAnimation is not null && _animationSources.FirstOrDefault(source => source.Definition.Key == selectedAnimation) is { } selectedSource)
                            SelectAnimation(selectedSource);
                        _displayedDefinition = Definition; _displayedSources = sources;
                    }
                    finally
                    {
                        _restoringSourceSelection = false;
                        UpdatePaintSourceLabel();
                    }
                }
                if (_selectedLayer is null || !Definition.Layers.Contains(_selectedLayer))
                {
                    _selectedLayer = Definition.Layers.FirstOrDefault();
                    _selectedCells = Rectangle.Empty;
                }
                RefreshStructure();
                ClampTileSelection();
                RefreshTileProperties();
                _properties.Refresh();
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
        ThemedMessageBox.Show(
            this,
            ex.Message,
            "GSCN editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.CancelGesture();
            Document.EditingContextChanging -= EditingContextChanging;
            Document.Changed -= DocumentChanged;

            foreach (var source in _tilesheetSources)
                source.Dispose();
            _tilesheetSources.Clear();

            foreach (var source in _animationSources)
                source.Dispose();
            _animationSources.Clear();

            _sourceFrameImages.Dispose();
            _panes.Clear();
        }

        if (disposing && _ownsPackages) _packages.Dispose();
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
    /// <inheritdoc/>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _preview.HasGesture) { _preview.CancelGesture(); return true; }
        return UndoRedoMenu.TryShortcut(this, keyData, Document.History, CommitEdits) || base.ProcessCmdKey(ref msg, keyData);
    }

}
