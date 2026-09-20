using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms;

/// <summary>
/// Hostable WinForms surface for editing one GANI animation definition.
/// The standalone application hosts this control inside a dock document; Studio can
/// host the same control later without depending on MainForm.
/// </summary>
public sealed class AnimationEditorControl : UserControl
{
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

        // Give the reusable editor a sensible construction-time size before
        // SplitContainer minimum sizes are applied. A host may resize it immediately
        // afterward, but WinForms validates SplitterDistance during construction.
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;
        BuildLayout();

        _properties.SelectedObject = _propertyAdapter;

        _sourceTree.BeforeExpand += SourceTreeBeforeExpand;
        _sourceTree.NodeMouseDoubleClick += (_, e) =>
        {
            _sourceTree.SelectedNode = e.Node;
            AddSelectedSourceFrame();
        };

        _frames.SelectedIndexChanged += (_, _) =>
        {
            if (_frames.SelectedIndices.Count == 1)
            {
                _preview.Pause();
                _playButton.Text = "Play";
            }
        };

        _preview.CurrentFrameChanged += (_, _) =>
        {
            if (_preview.CurrentFrameIndex >= 0 &&
                _preview.CurrentFrameIndex < _frames.Items.Count)
            {
                _frames.Items[_preview.CurrentFrameIndex].Selected = true;
                _frames.Items[_preview.CurrentFrameIndex].EnsureVisible();
            }
        };

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

    private bool AddTilesheetSourceCore(string path)
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
        return true;
    }

    private void RefreshAfterSourceChange()
    {
        RefreshSourceTree();
        UpdateValidation();
        _preview.Invalidate();
    }

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate().ToList();
        var lines = errors.Select(error => "ERROR: " + error).ToList();

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
            "INFO: GTS files are authoring/preview sources only. GANI persists logical tilesheet, region and coordinate references.");

        _validation.Text = string.Join(Environment.NewLine, lines.Distinct());
        return errors;
    }

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

        var outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1200, 800),
            SplitterDistance = 320,
            Panel1MinSize = 240,
            Panel2MinSize = 450
        };

        outer.Panel1.Controls.Add(_sourceTree);
        outer.Panel1.Controls.Add(BuildSourceToolbar());
        outer.Panel1.Controls.Add(SectionLabel("GTS frame sources"));

        var right = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(880, 800),
            SplitterDistance = 390,
            Panel1MinSize = 220,
            Panel2MinSize = 230
        };

        right.Panel1.Controls.Add(_preview);
        right.Panel1.Controls.Add(BuildPreviewToolbar());
        right.Panel1.Controls.Add(SectionLabel("Preview"));

        var lower = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(880, 410),
            SplitterDistance = 560,
            Panel1MinSize = 330,
            Panel2MinSize = 280
        };

        lower.Panel1.Controls.Add(_frames);
        lower.Panel1.Controls.Add(BuildSequenceToolbar());
        lower.Panel1.Controls.Add(SectionLabel("Animation frames"));

        var inspector = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(320, 410),
            SplitterDistance = 230,
            Panel1MinSize = 130,
            Panel2MinSize = 100
        };

        inspector.Panel1.Controls.Add(_properties);
        inspector.Panel1.Controls.Add(SectionLabel("Animation properties"));
        inspector.Panel2.Controls.Add(_validation);
        inspector.Panel2.Controls.Add(SectionLabel("Validation"));

        lower.Panel2.Controls.Add(inspector);
        right.Panel2.Controls.Add(lower);
        outer.Panel2.Controls.Add(right);
        Controls.Add(outer);
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
        bar.Items.Add("Add frame", null, (_, _) => AddSelectedSourceFrame());
        return bar;
    }

    private ToolStrip BuildSequenceToolbar()
    {
        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

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

    private static Label SectionLabel(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
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

        _sources.Remove(source);
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

    private static void PopulateFrames(
        TreeNode node,
        RowTag tag)
    {
        node.Nodes.Clear();

        var (columns, _) =
            TilesheetDefinitionValidator.GridSize(tag.Region);

        int columnCount = checked((int)Math.Min(columns, int.MaxValue));

        for (int x = 0; x < columnCount; x++)
        {
            node.Nodes.Add(
                new TreeNode($"Frame {x},{tag.Y}")
                {
                    Tag = new FrameTag(
                        tag.Source,
                        tag.Region,
                        x,
                        tag.Y)
                });
        }
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
        }

        base.Dispose(disposing);
    }
}
