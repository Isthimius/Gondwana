using Gondwana.Drawing.Sprites.GSPR;
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
    private readonly List<SceneDefinition> _sceneDefinitions = [];
    private readonly List<string> _sourceWarnings = [];
    public SpriteInstanceDefinition? SelectedSprite => _sprites.SelectedItem as SpriteInstanceDefinition;

    public SpriteEditorControl(SpriteDocument document)
    {
        Document = document;
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
        button.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = filter, Multiselect = true }; if (dialog.ShowDialog(this) == DialogResult.OK) add(dialog.FileNames); };
        return button;
    }

    private void LoadSources()
    {
        foreach (var source in Document.Definition.SceneSources)
            if (source.Kind == SpriteSceneSourceKind.LooseDefinitionFile && source.GscnPath is { } path)
                TryLoad(() => LoadScene(Document.ResolveReferencePath(path)));
        foreach (var source in Document.Definition.TilesheetSources)
            if (source.Kind == SpriteTilesheetSourceKind.LooseDefinitionFile && source.GtsPath is { } path)
                TryLoad(() => LoadTilesheet(Document.ResolveReferencePath(path)));
    }

    private void TryLoad(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
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
        _sceneDefinitions.Add(scene);
        var root = _scenes.Nodes.Add(scene.ID);
        foreach (var layer in scene.Layers) root.Nodes.Add(new TreeNode(layer.ID) { Tag = (scene, layer) });
        root.Expand();
        return scene;
    }

    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        foreach (var path in paths) { var source = LoadTilesheet(path); Document.SetLooseTilesheetSource(source.Definition.Name, path); }
        RefreshSelection();
    }
    private SpriteTilesheetSource LoadTilesheet(string path)
    {
        var source = SpriteTilesheetSource.Load(path);
        _sources.Add(source);
        var root = _frames.Nodes.Add(source.Definition.Name);
        foreach (var region in source.Definition.Regions)
        {
            var node = root.Nodes.Add(region.Name);
            var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
                node.Nodes.Add(new TreeNode($"{x}, {y}") { Tag = source.CreateFrame(region, x, y) });
        }
        root.Expand();
        return source;
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
        _properties.SelectedObject = SelectedSprite;
        var entry = SelectedSprite;
        var matches = _sources.Where(source => source.Definition.Name == entry?.Frame?.Tilesheet).ToList();
        var scenes = _sceneDefinitions.Where(scene => scene.ID == entry?.SceneId).ToList();
        var layer = scenes.Count == 1 ? scenes[0].Layers.FirstOrDefault(layer => layer.ID == entry?.SceneLayerId) : null;
        _preview.SetSelection(entry, matches.Count == 1 ? matches[0] : null, layer);
        UpdateValidation();
    }
    private void DocumentChanged(object? sender, EventArgs e) => UpdateValidation();
    public bool CommitEdits() => ValidateChildren();
    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        var lines = errors.Select(error => "ERROR: " + error).ToList();
        if (errors.Count == 0) lines.Add("VALID: GSPR structural validation passed.");
        lines.AddRange(_sourceWarnings.Select(warning => "WARNING: " + warning));
        foreach (var entry in Document.Definition.Sprites)
        {
            var scenes = _sceneDefinitions.Where(scene => scene.ID == entry.SceneId).ToList();
            if (scenes.Count != 1) lines.Add($"WARNING: Sprite '{entry.Nickname}': Scene source missing or ambiguous.");
            else if (!scenes[0].Layers.Any(layer => layer.ID == entry.SceneLayerId)) lines.Add($"WARNING: Sprite '{entry.Nickname}': SceneLayer not found.");
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
        if (disposing) { Document.Changed -= DocumentChanged; foreach (var source in _sources) source.Dispose(); }
        base.Dispose(disposing);
    }
}
