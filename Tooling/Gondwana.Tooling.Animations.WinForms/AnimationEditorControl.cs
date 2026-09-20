using System.Drawing.Drawing2D;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Animations.WinForms;

/// <summary>
/// Hostable WinForms surface for editing one GANI animation definition.
/// The standalone application hosts this control inside a dock document; Studio can
/// host the same control later without depending on MainForm.
/// </summary>
public sealed class AnimationEditorControl : UserControl
{
    public static IReadOnlyList<string> PaneNames { get; } =
        Array.AsReadOnly(
        [
            "GTS frame sources",
            "Preview",
            "Animation frames",
            "Animation properties",
            "Validation"
        ]);

    private const int SourceFrameThumbnailSize = 32;

    private sealed record RegionTag(
        TilesheetSource Source,
        TilesheetRegionDefinition Region);

    private sealed record RowTag(
        TilesheetSource Source,
        TilesheetRegionDefinition Region,
        int Y);

    private sealed record FrameTag(
        TilesheetSource Source,
        TilesheetRegionDefinition Region,
        int X,
        int Y);

    private readonly List<TilesheetSource> _sources = [];
    private readonly List<string> _sourceDiagnostics = [];
    private readonly ImageList _sourceFrameImages = new()
    {
        ImageSize = new Size(
            SourceFrameThumbnailSize,
            SourceFrameThumbnailSize),
        ColorDepth = ColorDepth.Depth32Bit,
        TransparentColor = Color.Transparent
    };

    private readonly TreeView _sourceTree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    private readonly ListView _frames = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false
    };

    private readonly PropertyGrid _properties = new()
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
        ScrollBars = ScrollBars.Both,
        WordWrap = false
    };

    private readonly AnimationPreviewControl _preview = new();
    private readonly ToolStripButton _playButton = new("Play");
    private readonly AnimationPropertyAdapter _propertyAdapter;
    private bool _refreshPending;
    private bool _syncingPreviewSelection;
    private bool _syncingSourceTreeSelection;
    private EditorDockWorkspace _workspace = null!;

    public AnimationDocument Document { get; }

    public AnimationDefinition Definition => Document.Definition;

    public AnimationEditorControl()
        : this(AnimationDocument.Create(Environment.CurrentDirectory))
    {
    }

    public AnimationEditorControl(AnimationDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _propertyAdapter = new AnimationPropertyAdapter(Document);

        // Start with the existing editor proportions; the host may resize it.
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;

        _sourceTree.ImageList = _sourceFrameImages;
        _sourceTree.ImageIndex = -1;
        _sourceTree.SelectedImageIndex = -1;
        _sourceTree.ItemHeight = Math.Max(
            _sourceTree.ItemHeight,
            SourceFrameThumbnailSize + 4);

        BuildLayout();

        _properties.SelectedObject = _propertyAdapter;

        _sourceTree.BeforeExpand += SourceTreeBeforeExpand;
        _sourceTree.AfterSelect += SourceTreeAfterSelect;
        _sourceTree.NodeMouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left ||
                e.Node.Tag is not FrameTag frame)
            {
                return;
            }

            // A click on an already-selected source node does not raise
            // AfterSelect, so handle the user gesture explicitly as well.
            ClearAnimationFrameSelection();
            ShowSourceFrame(frame);
        };
        _sourceTree.NodeMouseDoubleClick += (_, e) =>
        {
            _sourceTree.SelectedNode = e.Node;
            AddSelectedSourceFrame();
        };

        _preview.DoubleClick += (_, _) =>
        {
            if (_preview.IsShowingSourceFrame)
                AddSelectedSourceFrame();
        };

        _frames.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncingPreviewSelection)
                ShowSelectedAnimationFrameSource();
        };

        _frames.ItemActivate += (_, _) =>
        {
            if (!_syncingPreviewSelection)
                ShowSelectedAnimationFrameSource();
        };

        _preview.CurrentFrameChanged += (_, _) =>
        {
            if (_preview.CurrentFrameIndex >= 0 &&
                _preview.CurrentFrameIndex < _frames.Items.Count)
            {
                _syncingPreviewSelection = true;

                try
                {
                    _frames.Items[_preview.CurrentFrameIndex].Selected = true;
                    _frames.Items[_preview.CurrentFrameIndex].EnsureVisible();
                }
                finally
                {
                    _syncingPreviewSelection = false;
                }
            }
        };

        LoadDefinitionTilesheetSources();

        Document.Changed += DocumentChanged;

        DarkTheme.Apply(this);
        RefreshView();
    }

    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        bool changed = false;

        try
        {
            foreach (var path in paths)
                changed |= AddTilesheetSourceCore(path);
        }
        finally
        {
            if (changed)
                RefreshAfterSourceChange();
        }
    }

    public void AddTilesheetSource(string path)
    {
        if (AddTilesheetSourceCore(path))
            RefreshAfterSourceChange();
    }

    private bool AddTilesheetSourceCore(
        string path,
        bool persistReference = true,
        string? expectedTilesheet = null)
    {
        path = Path.GetFullPath(path);

        if (_sources.Any(source =>
                string.Equals(
                    source.FilePath,
                    path,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var loaded = TilesheetSource.Load(path);

        if (!string.IsNullOrWhiteSpace(expectedTilesheet) &&
            !string.Equals(
                loaded.Definition.Name,
                expectedTilesheet,
                StringComparison.Ordinal))
        {
            loaded.Dispose();
            throw new InvalidDataException(
                $"GANI expects tilesheet '{expectedTilesheet}', but '{path}' defines '{loaded.Definition.Name}'.");
        }

        var duplicateName = _sources.FirstOrDefault(source =>
            string.Equals(
                source.Definition.Name,
                loaded.Definition.Name,
                StringComparison.Ordinal));

        if (duplicateName is not null)
        {
            loaded.Dispose();
            throw new InvalidOperationException(
                $"Tilesheet name '{duplicateName.Definition.Name}' is already loaded from '{duplicateName.FilePath}'. " +
                "GANI frame references resolve by logical tilesheet name, so two sources with the same name would be ambiguous.");
        }

        _sources.Add(loaded);

        if (persistReference)
        {
            Document.SetLooseTilesheetSource(
                loaded.Definition.Name,
                path);

            string diagnosticKey =
                $"tilesheet '{loaded.Definition.Name}'";

            _sourceDiagnostics.RemoveAll(message =>
                message.Contains(
                    diagnosticKey,
                    StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private void RefreshAfterSourceChange()
    {
        RefreshSourceTree();
        UpdateValidation();
        _preview.Invalidate();
    }

    private void LoadDefinitionTilesheetSources()
    {
        _sourceDiagnostics.Clear();
        Definition.TilesheetSources ??= [];

        var explicitTilesheets = new HashSet<string>(
            Definition.TilesheetSources
                .Where(source => !string.IsNullOrWhiteSpace(source.Tilesheet))
                .Select(source => source.Tilesheet),
            StringComparer.Ordinal);

        foreach (var sourceReference in Definition.TilesheetSources.ToList())
        {
            switch (sourceReference.Kind)
            {
                case AnimationTilesheetSourceKind.LooseDefinitionFile:
                    LoadLooseDefinitionSource(sourceReference);
                    break;

                case AnimationTilesheetSourceKind.PackedDefinitionFile:
                    _sourceDiagnostics.Add(
                        $"INFO: Tilesheet '{sourceReference.Tilesheet}' is recorded as packed GTS entry " +
                        $"'{sourceReference.AssetEntryName}' in '{sourceReference.AssetsFilePath}'. " +
                        "Packed GTS authoring sources are preserved but are not previewed by this editor yet.");
                    break;
            }
        }

        foreach (var tilesheet in Definition.Frames
                     .Select(frame => frame.Tilesheet)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.Ordinal))
        {
            if (FindSource(tilesheet) is not null ||
                explicitTilesheets.Contains(tilesheet))
            {
                continue;
            }

            TryRecoverLegacyLooseSource(tilesheet);
        }
    }

    private void LoadLooseDefinitionSource(
        AnimationTilesheetSourceDefinition sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference.GtsPath))
            return;

        try
        {
            string path = Document.ResolveReferencePath(
                sourceReference.GtsPath);

            if (!File.Exists(path))
            {
                _sourceDiagnostics.Add(
                    $"WARNING: Tilesheet '{sourceReference.Tilesheet}' references missing GTS file '{sourceReference.GtsPath}'.");
                return;
            }

            AddTilesheetSourceCore(
                path,
                persistReference: false,
                expectedTilesheet: sourceReference.Tilesheet);
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            _sourceDiagnostics.Add(
                $"WARNING: Could not load GTS source for tilesheet '{sourceReference.Tilesheet}': {ex.Message}");
        }
    }

    private void TryRecoverLegacyLooseSource(string tilesheet)
    {
        List<string> matches = [];

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                         Document.BaseDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly)
                     .Where(path =>
                         Path.GetExtension(path).Equals(
                             ".gts",
                             StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var definition =
                        TilesheetDefinitionSerializer.Load(path);

                    if (string.Equals(
                            definition.Name,
                            tilesheet,
                            StringComparison.Ordinal))
                    {
                        matches.Add(path);
                    }
                }
                catch (Exception ex) when (
                    ex is IOException or
                    InvalidDataException or
                    ArgumentException or
                    UnauthorizedAccessException or
                    NotSupportedException)
                {
                    // A neighboring unrelated GTS should not prevent opening
                    // a legacy GANI document.
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException)
        {
            _sourceDiagnostics.Add(
                $"WARNING: Could not inspect '{Document.BaseDirectory}' for legacy GTS dependencies: {ex.Message}");
            return;
        }

        if (matches.Count == 0)
            return;

        if (matches.Count > 1)
        {
            _sourceDiagnostics.Add(
                $"WARNING: Legacy GANI references tilesheet '{tilesheet}', but multiple matching GTS files exist beside the GANI. Add the intended source explicitly.");
            return;
        }

        try
        {
            string path = matches[0];
            if (AddTilesheetSourceCore(
                    path,
                    persistReference: false,
                    expectedTilesheet: tilesheet))
            {
                Document.SetLooseTilesheetSource(
                    tilesheet,
                    path);

                _sourceDiagnostics.Add(
                    $"INFO: Recovered legacy tilesheet source '{tilesheet}' from '{Path.GetFileName(path)}'. Save the GANI to persist this dependency.");
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            _sourceDiagnostics.Add(
                $"WARNING: Could not recover legacy GTS source for tilesheet '{tilesheet}': {ex.Message}");
        }
    }

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate().ToList();
        var lines = errors.Select(error => "ERROR: " + error).ToList();
        lines.AddRange(_sourceDiagnostics);

        for (int i = 0; i < Definition.Frames.Count; i++)
        {
            var frame = Definition.Frames[i];
            var source = FindSource(frame.Tilesheet);

            if (source is null)
            {
                lines.Add(
                    $"WARNING: Frame {i} references tilesheet '{frame.Tilesheet}', but no matching GTS source is loaded.");
                continue;
            }

            if (!source.TryResolve(frame, out _, out _))
            {
                lines.Add(
                    $"WARNING: Frame {i} cannot resolve {frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile}) in the loaded GTS.");
            }
            else if (source.PreviewWarning is { } warning)
            {
                lines.Add(
                    $"WARNING: {source.Definition.Name}: {warning}");
            }
        }

        if (errors.Count == 0)
            lines.Insert(0, "VALID: GANI structural validation passed.");

        lines.Add(
            "INFO: GANI frame references remain logical at runtime; TilesheetSources records portable authoring locations for GTS dependencies.");

        _validation.Text = string.Join(Environment.NewLine, lines.Distinct());
        return errors;
    }

    public bool ShowPane(string paneName) =>
        _workspace.ShowPane(paneName);

    public bool IsPaneVisible(string paneName) =>
        _workspace.IsPaneVisible(paneName);

    public void ShowAllPanes() =>
        _workspace.ShowAllPanes();

    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus && ValidateChildren();
    }

    private void BuildLayout()
    {
        _frames.Columns.Add("#", 42, HorizontalAlignment.Right);
        _frames.Columns.Add("Tilesheet", 150);
        _frames.Columns.Add("Region", 130);
        _frames.Columns.Add("X", 55, HorizontalAlignment.Right);
        _frames.Columns.Add("Y", 55, HorizontalAlignment.Right);

        _workspace = new EditorDockWorkspace();
        Controls.Add(_workspace);
        var dock = _workspace.DockPanel;
        var sources = _workspace.AddPane("GTS frame sources", _sourceTree, BuildSourceToolbar());
        var preview = _workspace.AddPane("Preview", _preview, BuildPreviewToolbar());
        var frames = _workspace.AddPane("Animation frames", _frames, BuildSequenceToolbar());
        var properties = _workspace.AddPane("Animation properties", _properties);
        var validation = _workspace.AddPane("Validation", _validation);
        preview.Show(dock, DockState.Document);
        sources.Show(preview.Pane, DockAlignment.Left, 320d / 1200);
        frames.Show(preview.Pane, DockAlignment.Bottom, 410d / 800);
        properties.Show(frames.Pane, DockAlignment.Right, 320d / 880);
        validation.Show(properties.Pane, DockAlignment.Bottom, 180d / 410);
    }

    private ToolStrip BuildSourceToolbar()
    {
        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        bar.Items.Add("Add GTS…", null, (_, _) => ChooseTilesheetSources());
        bar.Items.Add("Remove", null, (_, _) => RemoveSelectedSource());
        return bar;
    }

    private ToolStrip BuildSequenceToolbar()
    {
        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        bar.Items.Add("Add", null, (_, _) => AddSelectedSourceFrame());
        bar.Items.Add("Remove", null, (_, _) => RemoveSelectedFrame());
        bar.Items.Add("Up", null, (_, _) => MoveSelectedFrame(-1));
        bar.Items.Add("Down", null, (_, _) => MoveSelectedFrame(1));
        return bar;
    }

    private ToolStrip BuildPreviewToolbar()
    {
        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        _playButton.Click += (_, _) =>
        {
            _preview.TogglePlay();
            _playButton.Text = _preview.IsPlaying
                ? "Pause"
                : "Play";
        };

        bar.Items.Add(_playButton);
        bar.Items.Add("Restart", null, (_, _) =>
        {
            _preview.Restart();
            _preview.Play();
            _playButton.Text = _preview.IsPlaying
                ? "Pause"
                : "Play";
        });

        bar.Items.Add("Step", null, (_, _) =>
        {
            _preview.Step();
            _playButton.Text = "Play";
        });

        var zoom = new ToolStripComboBox
        {
            Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = DarkTheme.Background,
            ForeColor = DarkTheme.Foreground,
            FlatStyle = FlatStyle.Flat
        };

        zoom.Items.AddRange(["Fit", "1x", "2x", "4x"]);
        zoom.SelectedIndex = 0;
        zoom.SelectedIndexChanged += (_, _) =>
        {
            _preview.SetZoom(
                zoom.SelectedIndex switch
                {
                    1 => 1f,
                    2 => 2f,
                    3 => 4f,
                    _ => null
                });
        };

        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripLabel("Zoom"));
        bar.Items.Add(zoom);

        return bar;
    }

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
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            ShowError(ex);
        }
    }

    private void RemoveSelectedSource()
    {
        var node = _sourceTree.SelectedNode;
        if (node is null)
            return;

        while (node.Parent is not null)
            node = node.Parent;

        if (node.Tag is not TilesheetSource source)
            return;

        _preview.ClearSourceFrame();
        _sources.Remove(source);
        Document.RemoveTilesheetSource(source.Definition.Name);
        source.Dispose();
        RefreshSourceTree();
        UpdateValidation();
        _preview.Invalidate();
    }

    private void AddSelectedSourceFrame()
    {
        if (_sourceTree.SelectedNode?.Tag is not FrameTag frame)
            return;

        Definition.Frames.Add(
            frame.Source.CreateFrame(
                frame.Region,
                frame.X,
                frame.Y));

        Document.MarkChanged();
        SelectFrame(Definition.Frames.Count - 1);
    }

    private void RemoveSelectedFrame()
    {
        if (_frames.SelectedIndices.Count != 1)
            return;

        int index = _frames.SelectedIndices[0];
        Definition.Frames.RemoveAt(index);
        Document.MarkChanged();

        if (Definition.Frames.Count > 0)
            SelectFrame(Math.Min(index, Definition.Frames.Count - 1));
    }

    private void MoveSelectedFrame(int offset)
    {
        if (_frames.SelectedIndices.Count != 1)
            return;

        int index = _frames.SelectedIndices[0];
        int target = index + offset;

        if (target < 0 || target >= Definition.Frames.Count)
            return;

        var frame = Definition.Frames[index];
        Definition.Frames.RemoveAt(index);
        Definition.Frames.Insert(target, frame);
        Document.MarkChanged();
        SelectFrame(target);
    }

    private void SelectFrame(int index)
    {
        RefreshFrameList();

        if (index < 0 || index >= _frames.Items.Count)
            return;

        _frames.Items[index].Selected = true;
        _frames.Items[index].Focused = true;
        _frames.Items[index].EnsureVisible();
    }

    private void RefreshView()
    {
        RefreshSourceTree();
        RefreshFrameList();
        _properties.Refresh();
        _preview.Configure(Definition, ResolveFramePreview);
        UpdateValidation();
    }

    private void RefreshFrameList()
    {
        int selected = _frames.SelectedIndices.Count == 1
            ? _frames.SelectedIndices[0]
            : -1;

        _frames.BeginUpdate();
        try
        {
            _frames.Items.Clear();

            for (int i = 0; i < Definition.Frames.Count; i++)
            {
                var frame = Definition.Frames[i];
                var item = new ListViewItem((i + 1).ToString())
                {
                    Tag = frame
                };

                item.SubItems.Add(frame.Tilesheet);
                item.SubItems.Add(frame.RegionName);
                item.SubItems.Add(frame.XTile.ToString());
                item.SubItems.Add(frame.YTile.ToString());
                _frames.Items.Add(item);
            }
        }
        finally
        {
            _frames.EndUpdate();
        }

        if (selected >= 0 && selected < _frames.Items.Count)
            _frames.Items[selected].Selected = true;
    }

    private void RefreshSourceTree()
    {
        _sourceTree.BeginUpdate();
        try
        {
            _sourceTree.Nodes.Clear();
            _sourceFrameImages.Images.Clear();

            foreach (var source in _sources.OrderBy(
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
                        regionNode.Nodes.Add(new TreeNode("Expand to load rows…"));

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

    private void SourceTreeAfterSelect(
        object? sender,
        TreeViewEventArgs e)
    {
        if (e.Node.Tag is not FrameTag frame)
            return;

        if (!_syncingSourceTreeSelection)
            ClearAnimationFrameSelection();

        ShowSourceFrame(frame);
    }

    private void ShowSelectedAnimationFrameSource()
    {
        if (_frames.SelectedIndices.Count != 1)
            return;

        var item = _frames.Items[_frames.SelectedIndices[0]];
        if (item.Tag is not AnimationFrameDefinition frame)
            return;

        _preview.Pause();
        _playButton.Text = "Play";

        var sourceNode = FindSourceFrameNode(frame);
        if (sourceNode?.Tag is not FrameTag sourceFrame)
        {
            _sourceTree.SelectedNode = null;
            _preview.ShowSourceFrame(
                ResolveFramePreview(frame),
                $"{frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile})");
            return;
        }

        bool selectionChanged =
            !ReferenceEquals(_sourceTree.SelectedNode, sourceNode);

        _syncingSourceTreeSelection = true;
        try
        {
            _sourceTree.SelectedNode = sourceNode;
            sourceNode.EnsureVisible();
        }
        finally
        {
            _syncingSourceTreeSelection = false;
        }

        // Assigning the already-selected node does not raise AfterSelect.
        if (!selectionChanged)
            ShowSourceFrame(sourceFrame);
    }

    private TreeNode? FindSourceFrameNode(
        AnimationFrameDefinition frame)
    {
        var source = FindSource(frame.Tilesheet);
        if (source is null)
            return null;

        var root = _sourceTree.Nodes
            .Cast<TreeNode>()
            .FirstOrDefault(node =>
                ReferenceEquals(node.Tag, source));

        if (root is null)
            return null;

        var regionNode = root.Nodes
            .Cast<TreeNode>()
            .FirstOrDefault(node =>
                node.Tag is RegionTag tag &&
                string.Equals(
                    tag.Region.Name,
                    frame.RegionName,
                    StringComparison.OrdinalIgnoreCase));

        if (regionNode?.Tag is not RegionTag regionTag)
            return null;

        if (regionNode.Nodes.Count == 1 &&
            regionNode.Nodes[0].Tag is null)
        {
            PopulateRows(regionNode, regionTag);
        }

        regionNode.Expand();

        var rowNode = regionNode.Nodes
            .Cast<TreeNode>()
            .FirstOrDefault(node =>
                node.Tag is RowTag tag &&
                tag.Y == frame.YTile);

        if (rowNode?.Tag is not RowTag rowTag)
            return null;

        if (rowNode.Nodes.Count == 1 &&
            rowNode.Nodes[0].Tag is null)
        {
            PopulateFrames(rowNode, rowTag);
        }

        rowNode.Expand();

        return rowNode.Nodes
            .Cast<TreeNode>()
            .FirstOrDefault(node =>
                node.Tag is FrameTag tag &&
                tag.X == frame.XTile &&
                tag.Y == frame.YTile);
    }

    private void ClearAnimationFrameSelection()
    {
        foreach (ListViewItem item in _frames.SelectedItems.Cast<ListViewItem>().ToList())
            item.Selected = false;
    }

    private void ShowSourceFrame(FrameTag frame)
    {
        _preview.Pause();
        _playButton.Text = "Play";

        var definition = frame.Source.CreateFrame(
            frame.Region,
            frame.X,
            frame.Y);

        _preview.ShowSourceFrame(
            ResolveFramePreview(definition),
            $"{definition.Tilesheet}:{definition.RegionName} ({definition.XTile},{definition.YTile})");
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
                Tag = new RowTag(
                    tag.Source,
                    tag.Region,
                    y)
            };

            rowNode.Nodes.Add(new TreeNode("Expand to load frames…"));
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

        var sourceBounds = TilesheetSource.FrameBounds(
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

    private FramePreview? ResolveFramePreview(
        AnimationFrameDefinition frame)
    {
        var source = FindSource(frame.Tilesheet);
        if (source?.Image is null)
            return null;

        if (!source.TryResolve(
                frame,
                out _,
                out var bounds))
        {
            return null;
        }

        return new FramePreview(
            source.Image,
            bounds);
    }

    private TilesheetSource? FindSource(string logicalName) =>
        _sources.FirstOrDefault(source =>
            string.Equals(
                source.Definition.Name,
                logicalName,
                StringComparison.Ordinal));

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
                if (!Document.IsDirty)
                {
                    _sourceDiagnostics.RemoveAll(message =>
                        message.StartsWith(
                            "INFO: Recovered legacy tilesheet source",
                            StringComparison.Ordinal));
                }

                _properties.Refresh();
                RefreshFrameList();
                _preview.Configure(Definition, ResolveFramePreview);
                UpdateValidation();
            }
        });
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(
            this,
            ex.Message,
            "GANI editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.Changed -= DocumentChanged;

            foreach (var source in _sources)
                source.Dispose();

            _sources.Clear();
            _sourceFrameImages.Dispose();
        }

        base.Dispose(disposing);
    }
}
