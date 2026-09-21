using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Sprites.WinForms;

/// <summary>The shared standalone and Studio GSPR authoring surface.</summary>
public sealed class SpriteEditorControl : UserControl
{
    public static IReadOnlyList<string> PaneNames { get; } = Array.AsReadOnly(new[] { "Sprites", "Sprite preview", "GSCN scene/layer sources", "GTS frame sources", "Properties", "Validation" });
    public SpriteDocument Document { get; }
    private readonly EditorDockWorkspace _workspace = new("gspr");
    private readonly ListBox _sprites = new() { Dock = DockStyle.Fill, DisplayMember = "Nickname" };
    private readonly TreeView _scenes = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly TreeView _frames = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly TextBox _validation = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly SpritePreviewControl _preview = new();
    private readonly List<SpriteTilesheetSource> _sources = [];
    private readonly ImageList _thumbnails = new() { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<SceneDefinition> _sceneDefinitions = [];
    private readonly List<string> _sourceWarnings = [];
    private readonly HashSet<string> _loadedScenePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedTilesheetPaths = new(StringComparer.OrdinalIgnoreCase);
    public SpriteInstanceDefinition? SelectedSprite => _sprites.SelectedItem as SpriteInstanceDefinition;

    public SpriteEditorControl(SpriteDocument document)
    {
        Document = document;
        _frames.ImageList = _thumbnails;
        Dock = DockStyle.Fill;
        Controls.Add(_workspace);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
        void Button(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true };
            button.Click += (_, _) => action();
            toolbar.Controls.Add(button);
        }
        Button("Add", () => SelectSprite(Document.AddSprite()));
        Button("Duplicate", () => { if (SelectedSprite is { } entry) SelectSprite(Document.DuplicateSprite(entry)); });
        Button("Remove", () => { if (SelectedSprite is { } entry) { Document.RemoveSprite(entry); RefreshEntries(); } });
        var sprites = _workspace.AddPane("sprites", PaneNames[0], _sprites, toolbar);
        var preview = _workspace.AddPane("sprite-preview", PaneNames[1], _preview);
        var scenes = _workspace.AddPane("scene-sources", PaneNames[2], _scenes, SourceToolbar("Add GSCN…", "GSCN|*.gscn", AddSceneSources));
        var frames = _workspace.AddPane("gts-sources", PaneNames[3], _frames, SourceToolbar("Add GTS…", "GTS|*.gts", AddTilesheetSources));
        var properties = _workspace.AddPane("properties", PaneNames[4], _properties);
        var validation = _workspace.AddPane("validation", PaneNames[5], _validation);
        _workspace.Place(sprites, () => sprites.Show(_workspace.DockPanel, DockState.Document));
        _workspace.Place(preview, () => preview.Show(sprites.Pane, DockAlignment.Right, 0.7));
        _workspace.Place(properties, () => properties.Show(preview.Pane, DockAlignment.Right, 0.3));
        _workspace.Place(scenes, () => scenes.Show(sprites.Pane, DockAlignment.Bottom, 0.4));
        _workspace.Place(frames, () => frames.Show(preview.Pane, DockAlignment.Bottom, 0.4));
        _workspace.Place(validation, () => validation.Show(properties.Pane, DockAlignment.Bottom, 0.4));
        _workspace.InitializeLayout();
        _sprites.SelectedIndexChanged += (_, _) => RefreshSelection();
        _properties.PropertyValueChanged += (_, _) => { Document.MarkChanged(); RefreshEntries(); };
        _scenes.NodeMouseDoubleClick += (_, e) =>
        {
            if (SelectedSprite is { } entry && e.Node.Tag is ValueTuple<SceneDefinition, SceneLayerDefinition> selected)
            { entry.SceneId = selected.Item1.ID; entry.SceneLayerId = selected.Item2.ID; Document.MarkChanged(); RefreshSelection(); }
        };
        _frames.NodeMouseDoubleClick += (_, e) =>
        {
            if (SelectedSprite is { } entry && e.Node.Tag is SpriteFrameDefinition frame)
            { entry.Frame = frame; Document.MarkChanged(); RefreshSelection(); }
        };
        Document.Changed += DocumentChanged;
        LoadSources();
        RefreshEntries();
        DarkTheme.Apply(this);
    }

    private Control SourceToolbar(string title, string filter, Action<IEnumerable<string>> add)
    {
        var button = new Button { Text = title, Dock = DockStyle.Top, Height = 30 };
        button.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter, Multiselect = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                TryLoad(() => add(dialog.FileNames));
                UpdateValidation();
            }
        };
        return button;
    }

    private void LoadSources()
    {
        foreach (var source in Document.Definition.SceneSources)
        {
            if (source.Kind == SpriteSceneSourceKind.LooseDefinitionFile && source.GscnPath is { } path)
                TryLoad(() => LoadScene(Document.ResolveReferencePath(path)));
            else if (source.AssetsFilePath is { } archivePath && source.AssetEntryName is { } entry)
                TryLoad(() =>
                {
                    using var archive = AssetsFile.LoadOrCreate(Document.ResolveReferencePath(archivePath), null, false, register: false);
                    AddSceneDefinition(SceneDefinitionSerializer.Load(archive, entry));
                });
        }
        foreach (var source in Document.Definition.TilesheetSources)
        {
            if (source.Kind == SpriteTilesheetSourceKind.LooseDefinitionFile && source.GtsPath is { } path)
                TryLoad(() => LoadTilesheet(Document.ResolveReferencePath(path)));
            else if (source.AssetsFilePath is { } archivePath && source.AssetEntryName is { } entry)
                TryLoad(() => AddTilesheetDefinition(SpriteTilesheetSource.Load(Document.ResolveReferencePath(archivePath), entry)));
        }
    }

    private void TryLoad(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        { _sourceWarnings.Add(ex.Message); }
    }

    public void AddSceneSources(IEnumerable<string> paths)
    {
        foreach (var path in paths) { var scene = LoadScene(path); Document.SetLooseSceneSource(scene.ID, path); }
        RefreshSelection();
    }
    private SceneDefinition LoadScene(string path)
    {
        var scene = SceneDefinitionSerializer.Load(path);
        if (!_loadedScenePaths.Add(Path.GetFullPath(path))) return scene;
        AddSceneDefinition(scene);
        return scene;
    }
    private void AddSceneDefinition(SceneDefinition scene)
    {
        _sceneDefinitions.Add(scene);
        var root = _scenes.Nodes.Add(scene.ID);
        foreach (var layer in scene.Layers) root.Nodes.Add(new TreeNode(layer.ID) { Tag = (scene, layer) });
        root.Expand();
    }

    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        foreach (var path in paths) { var source = LoadTilesheet(path); Document.SetLooseTilesheetSource(source.Definition.Name, path); }
        RefreshSelection();
    }
    private SpriteTilesheetSource LoadTilesheet(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (_loadedTilesheetPaths.Contains(fullPath)) return _sources.First(source => source.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        var source = SpriteTilesheetSource.Load(path);
        _loadedTilesheetPaths.Add(fullPath);
        AddTilesheetDefinition(source);
        return source;
    }
    private void AddTilesheetDefinition(SpriteTilesheetSource source)
    {
        _sources.Add(source);
        var root = _frames.Nodes.Add(source.Definition.Name);
        foreach (var region in source.Definition.Regions)
        {
            var node = root.Nodes.Add(region.Name);
            var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                var frameNode = new TreeNode($"{x}, {y}") { Tag = source.CreateFrame(region, x, y) };
                if (source.Image is { } image)
                {
                    using var thumbnail = new Bitmap(32, 32);
                    using var graphics = Graphics.FromImage(thumbnail);
                    graphics.DrawImage(image, new Rectangle(0, 0, 32, 32), SpriteTilesheetSource.FrameBounds(region, x, y), GraphicsUnit.Pixel);
                    _thumbnails.Images.Add(thumbnail);
                    frameNode.ImageIndex = frameNode.SelectedImageIndex = _thumbnails.Images.Count - 1;
                }
                node.Nodes.Add(frameNode);
            }
        }
        root.Expand();
    }

    public void SelectSprite(SpriteInstanceDefinition sprite) { RefreshEntries(); _sprites.SelectedItem = sprite; }
    private void RefreshEntries()
    {
        var selected = SelectedSprite;
        _sprites.Items.Clear();
        _sprites.Items.AddRange(Document.Definition.Sprites.Cast<object>().ToArray());
        if (selected is not null && _sprites.Items.Contains(selected)) _sprites.SelectedItem = selected;
        else if (_sprites.Items.Count != 0) _sprites.SelectedIndex = 0;
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        _properties.SelectedObject = SelectedSprite is { } selected ? new SpriteProperties(selected) : null;
        var entry = SelectedSprite;
        var matches = _sources.Where(source => source.Definition.Name == entry?.Frame?.Tilesheet).ToList();
        var scenes = _sceneDefinitions.Where(scene => scene.ID == entry?.SceneId).ToList();
        var layer = scenes.Count == 1 ? scenes[0].Layers.FirstOrDefault(layer => layer.ID == entry?.SceneLayerId) : null;
        _preview.SetSelection(entry, matches.Count == 1 ? matches[0] : null, layer);
        UpdateValidation();
    }
    private void DocumentChanged(object? sender, EventArgs e) => UpdateValidation();
    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus && ValidateChildren();
    }
    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        var lines = errors.Select(error => "ERROR: " + error).ToList();
        if (errors.Count == 0) lines.Add("VALID: GSPR structural validation passed.");
        lines.AddRange(_sourceWarnings.Select(warning => "WARNING: " + warning));
        foreach (var path in _loadedScenePaths.Concat(_loadedTilesheetPaths))
            if (!File.Exists(path)) lines.Add($"WARNING: Loaded authoring source no longer exists: {path}");
        lines.AddRange(_sources.Where(source => source.PreviewWarning is not null)
            .Select(source => $"WARNING: GTS '{source.Definition.Name}': {source.PreviewWarning}"));
        foreach (var entry in Document.Definition.Sprites)
        {
            var scenes = _sceneDefinitions.Where(scene => scene.ID == entry.SceneId).ToList();
            if (scenes.Count != 1) lines.Add($"WARNING: Sprite '{entry.Nickname}': Scene source missing or ambiguous.");
            else if (!scenes[0].Layers.Any(layer => layer.ID == entry.SceneLayerId)) lines.Add($"WARNING: Sprite '{entry.Nickname}': SceneLayer not found.");
            if (scenes.Count == 1 && !string.IsNullOrWhiteSpace(entry.CollisionProfileName) &&
                !scenes[0].CollisionProfiles.Any(profile => profile.Name == entry.CollisionProfileName) &&
                entry.CollisionProfileName is not ("Actor" or "World" or "Projectile" or "Sensor"))
                lines.Add($"WARNING: Sprite '{entry.Nickname}': Collision profile '{entry.CollisionProfileName}' is not declared by the scene source.");
            if (entry.Frame is null) lines.Add($"WARNING: Sprite '{entry.Nickname}': Frame is unassigned.");
            else
            {
                var sources = _sources.Where(source => source.Definition.Name == entry.Frame.Tilesheet).ToList();
                if (sources.Count != 1 || !sources[0].TryResolve(entry.Frame, out _, out _)) lines.Add($"WARNING: Sprite '{entry.Nickname}': GTS frame missing or ambiguous.");
            }
        }
        lines.Add("INFO: Runtime references are logical; source metadata is authoring-only.");
        _validation.Lines = lines.ToArray();
        return errors;
    }
    public bool ShowPane(string name) => _workspace.ShowPane(name);
    public bool IsPaneVisible(string name) => _workspace.IsPaneVisible(name);
    public void ShowAllPanes() => _workspace.ShowAllPanes();
    public void ResetLayout() => _workspace.ResetLayout();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Document.Changed -= DocumentChanged; foreach (var source in _sources) source.Dispose(); _thumbnails.Dispose(); }
        base.Dispose(disposing);
    }
}

