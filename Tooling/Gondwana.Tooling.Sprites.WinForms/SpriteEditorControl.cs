using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Sprites.WinForms;

/// <summary>The shared standalone and Studio GSPR authoring surface.</summary>
public sealed class SpriteEditorControl : UserControl
{
    private sealed record AnimationChoice(string? Key)
    {
        /// <summary>Returns the animation key, or the no-selection label.</summary>
        /// <returns>The key or <c>(none)</c>.</returns>
        public override string ToString() => Key ?? "(none)";
    }

    /// <summary>Gets the pane titles accepted by the pane visibility methods.</summary>
    public static IReadOnlyList<string> PaneNames { get; } = Array.AsReadOnly(new[] { "Sprites", "Sprite preview", "GSCN scene/layer sources", "GTS frame sources", "Properties", "Validation" });
    /// <summary>Gets the mutable authoring document displayed by this editor.</summary>
    public SpriteDocument Document { get; }
    private readonly AssetPackageCatalog _packages;
    private readonly bool _ownsPackages;
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
    private readonly List<AnimationDefinition> _animationDefinitions = [];
    private readonly Dictionary<string, List<SpriteTilesheetSource>> _animationTilesheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ComboBox _animationPicker = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _syncingAnimationPicker;
    private readonly List<string> _sourceWarnings = [];
    private readonly HashSet<string> _loadedScenePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedTilesheetPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedAnimationPaths = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets the selected sprite instance, or null when no sprite is selected.</summary>
    public SpriteInstanceDefinition? SelectedSprite => _sprites.SelectedItem as SpriteInstanceDefinition;

    /// <summary>Creates an editor with its own asset-package cache.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public SpriteEditorControl(SpriteDocument document) : this(document, null) { }

    /// <summary>Creates an editor and loads its declared sources for previews.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <param name="packages">A borrowed package cache, or null to create a cache owned by the control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public SpriteEditorControl(SpriteDocument document, AssetPackageCatalog? packages)
    {
        _packages = packages ?? PackedResourcePicker.CreateCatalog(this);
        _ownsPackages = packages is null;
        Document = document;
        var shortcuts = UndoRedoMenu.Route(this, () => Document.History, CommitEdits);
        Disposed += (_, _) => shortcuts.Dispose();
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
        Button("Reload sources", ReloadSources);
        Button("Add", () => SelectSprite(Document.AddSprite()));
        Button("Duplicate", () => { if (SelectedSprite is { } entry) SelectSprite(Document.DuplicateSprite(entry)); });
        Button("Remove", () => { if (SelectedSprite is { } entry) { Document.RemoveSprite(entry); RefreshEntries(); } });
        Button("Add GANI…", () =>
        {
            using var dialog = new OpenFileDialog { Filter = "Gondwana animations|*.gani", Multiselect = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                TryLoad(() => AddAnimationSources(dialog.FileNames));
                UpdateValidation();
            }
        });
        Button("GANI from GAF…", () => PackedResourcePicker.PickAndApply(
            this, _packages, Document.BaseDirectory, AssetTypes.AnimationDefinition, AddPackedAnimationSource));
        toolbar.AutoSize = true;
        toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        toolbar.Controls.Add(new Label { Text = "Animation:", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        toolbar.Controls.Add(_animationPicker);
        _animationPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingAnimationPicker || SelectedSprite is not { } entry ||
                _animationPicker.SelectedItem is not AnimationChoice selected) return;
            string? key = selected.Key;
            if (entry.AnimationKey == key) return;
            Document.Edit(entry, "Change sprite animation", () =>
            {
                entry.AnimationKey = key;
                if (key is null) entry.StartAnimation = false;
            });
            RefreshSelection();
        };
        var sprites = _workspace.AddPane("sprites", PaneNames[0], _sprites, toolbar);
        var preview = _workspace.AddPane("sprite-preview", PaneNames[1], _preview);
        var scenes = _workspace.AddPane("scene-sources", PaneNames[2], _scenes, SourceToolbar("Add GSCN…", "GSCN|*.gscn", AddSceneSources, AssetTypes.SceneDefinition, AddPackedSceneSource));
        var frames = _workspace.AddPane("gts-sources", PaneNames[3], _frames, SourceToolbar("Add GTS…", "GTS|*.gts", AddTilesheetSources, AssetTypes.TilesheetDefinition, AddPackedTilesheetSource));
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
        _properties.PropertyValueChanged += (_, _) => RefreshEntries();
        _scenes.NodeMouseDoubleClick += (_, e) =>
        {
            if (SelectedSprite is { } entry && e.Node.Tag is ValueTuple<SceneDefinition, SceneLayerDefinition> selected)
            { Document.Edit(entry, "Assign sprite scene layer", () => { entry.SceneId = selected.Item1.ID; entry.SceneLayerId = selected.Item2.ID; }); RefreshSelection(); }
        };
        _frames.NodeMouseDoubleClick += (_, e) =>
        {
            if (SelectedSprite is { } entry && e.Node.Tag is SpriteFrameDefinition frame)
            { Document.AssignFrame(entry, frame); RefreshSelection(); }
        };
        Document.Changed += DocumentChanged;
        LoadSources();
        RefreshEntries();
        var definitionBar = new DefinitionSelectionBar<SpriteDefinition>(
            () => Document.Definitions, () => Document.SelectedDefinition, definition => definition.Key,
            Document.SelectDefinition, () => Document.AddDefinition(), () => Document.DuplicateDefinition(),
            () => Document.RemoveDefinition(Document.SelectedDefinition), Document.RenameDefinition, RefreshSelectedDefinition,
            "gspr", (path, select) => Document.ImportDefinitions(path, select));
        Controls.Add(definitionBar);
        Document.Changed += definitionBar.RefreshMembers;
        definitionBar.Disposed += (_, _) => Document.Changed -= definitionBar.RefreshMembers;
        NavigationSizing.Attach(_scenes);
        NavigationSizing.Attach(_frames);
        ToolingAppearance.Apply(this);
    }

    private Control SourceToolbar(string title, string filter, Action<IEnumerable<string>> add,
        AssetTypes type, Action<PackedResourceSelection> addPacked)
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
        var packed = new Button { Text = "Add from GAF…", AutoSize = true };
        packed.Click += (_, _) => PackedResourcePicker.PickAndApply(this, _packages, Document.BaseDirectory, type, addPacked);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        button.Dock = DockStyle.None;
        button.AutoSize = true;
        bar.Controls.AddRange([button, packed]);
        return bar;
    }

    /// <summary>Clears the package cache, releases loaded previews, and reloads the document's declared sources.</summary>
    /// <remarks>Also clears a cache supplied by the host. Does not save or change the authoring document.</remarks>
    public void ReloadSources()
    {
        _packages.Clear();
        foreach (var source in _sources) source.Dispose();
        ClearAnimationTilesheets();
        _sources.Clear(); _sceneDefinitions.Clear(); _animationDefinitions.Clear(); _sourceWarnings.Clear();
        _loadedScenePaths.Clear(); _loadedTilesheetPaths.Clear(); _loadedAnimationPaths.Clear();
        _scenes.Nodes.Clear(); _frames.Nodes.Clear(); _thumbnails.Images.Clear();
        NavigationSizing.RefreshImages(_frames);
        LoadSources(); RefreshEntries();
    }

    /// <summary>Selects an owned definition and refreshes its source previews and controls without reloading or saving the document.</summary>
    public void SelectDefinition(SpriteDefinition definition)
    {
        if (ReferenceEquals(Document.SelectedDefinition, definition)) return;
        Document.SelectDefinition(definition);
        RefreshSelectedDefinition();
    }

    private void RefreshSelectedDefinition()
    {
        foreach (var source in _sources) source.Dispose();
        ClearAnimationTilesheets();
        _sources.Clear(); _sceneDefinitions.Clear(); _animationDefinitions.Clear(); _sourceWarnings.Clear();
        _loadedScenePaths.Clear(); _loadedTilesheetPaths.Clear(); _loadedAnimationPaths.Clear();
        _scenes.Nodes.Clear(); _frames.Nodes.Clear();
        LoadSources(); RefreshEntries();
    }

    private void LoadSources()
    {
        foreach (var source in Document.Definition.AnimationSources)
        {
            if (source.Kind == SpriteAnimationSourceKind.LooseDefinitionFile && source.GaniPath is { } path)
                TryLoad(() => LoadAnimation(Document.ResolveReferencePath(path), source.AnimationKey));
            else if (source.Kind == SpriteAnimationSourceKind.PackedDefinitionFile && source.AssetsFilePath is { } archivePath && source.AssetEntryName is { } entry)
                TryLoad(() =>
                {
                    var resource = new AuthoringResource(Document.ResolveReferencePath(archivePath), AssetTypes.AnimationDefinition, entry);
                    AddAnimationDefinition(new AuthoringResourceResolver(_packages).LoadAnimation(resource, source.AnimationKey), resource);
                });
        }
        foreach (var source in Document.Definition.SceneSources)
        {
            if (source.Kind == SpriteSceneSourceKind.LooseDefinitionFile && source.GscnPath is { } path)
                TryLoad(() => LoadScene(Document.ResolveReferencePath(path), source.SceneId));
            else if (source.Kind == SpriteSceneSourceKind.PackedDefinitionFile && source.AssetsFilePath is { } archivePath && source.AssetEntryName is { } entry)
                TryLoad(() =>
                {
                    var resource = new AuthoringResource(Document.ResolveReferencePath(archivePath), AssetTypes.SceneDefinition, entry);
                    AddSceneDefinition(new AuthoringResourceResolver(_packages).LoadScene(resource, source.SceneId));
                });
        }
        foreach (var source in Document.Definition.TilesheetSources)
        {
            if (source.Kind == SpriteTilesheetSourceKind.LooseDefinitionFile && source.GtsPath is { } path)
                TryLoad(() => LoadTilesheet(Document.ResolveReferencePath(path), source.Tilesheet));
            else if (source.Kind == SpriteTilesheetSourceKind.PackedDefinitionFile && source.AssetsFilePath is { } archivePath && source.AssetEntryName is { } entry)
                TryLoad(() => AddTilesheetDefinition(SpriteTilesheetSource.LoadPacked(Document.ResolveReferencePath(archivePath), entry, source.Tilesheet, _packages)));
        }
    }

    private void ClearAnimationTilesheets()
    {
        foreach (var source in _animationTilesheets.Values.SelectMany(sources => sources)) source.Dispose();
        _animationTilesheets.Clear();
    }

    /// <summary>Validates and adds one packed tilesheet member and its frame thumbnails.</summary>
    public void AddPackedTilesheetSource(PackedResourceSelection selection)
    {
        selection.Validate(_packages, AssetTypes.TilesheetDefinition);
        var resource = selection.Resource;
        var source = SpriteTilesheetSource.LoadPacked(resource.Path, resource.EntryName!, selection.Member!, _packages);
        try { Document.AddPackedTilesheetSource(selection.Member!, resource.Path, resource.EntryName!); }
        catch { source.Dispose(); throw; }
        AddTilesheetDefinition(source);
        RefreshSelection();
    }

    /// <summary>Validates and adds one packed animation member using its own dependencies for previews.</summary>
    public void AddPackedAnimationSource(PackedResourceSelection selection)
    {
        selection.Validate(_packages, AssetTypes.AnimationDefinition);
        var resource = selection.Resource;
        var definition = new AuthoringResourceResolver(_packages).LoadAnimation(resource, selection.Member);
        Document.AddPackedAnimationSource(selection.Member!, resource.Path, resource.EntryName!);
        AddAnimationDefinition(definition, resource);
        RefreshSelection();
    }

    /// <summary>Validates and adds one packed scene member to the scene/layer tree.</summary>
    public void AddPackedSceneSource(PackedResourceSelection selection)
    {
        selection.Validate(_packages, AssetTypes.SceneDefinition);
        var resource = selection.Resource;
        var definition = new AuthoringResourceResolver(_packages).LoadScene(resource, selection.Member);
        Document.AddPackedSceneSource(selection.Member!, resource.Path, resource.EntryName!);
        AddSceneDefinition(definition);
        RefreshSelection();
    }

    private void TryLoad(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { _sourceWarnings.Add(ex.Message); }
    }

    /// <summary>Loads the members of loose animation files for authoring and records their source paths in the document.</summary>
    /// <param name="paths">Definition files to load; relative paths use the process's current directory.</param>
    /// <remarks>Earlier additions remain if a later file fails to load.</remarks>
    public void AddAnimationSources(IEnumerable<string> paths)
    {
        using var transaction = Document.History.BeginTransaction("Add sources");
        try
        {
            foreach (var path in paths)
                foreach (var member in AnimationDefinitionSerializer.LoadAll(path))
                {
                    LoadAnimation(path, member.Key);
                    Document.SetLooseAnimationSource(member.Key, path);
                }
            RefreshSelection();

        }
        finally { transaction.Commit(); }
    }

    private void LoadAnimation(string path, string key)
    {
        var fullPath = Path.GetFullPath(path);
        var memberPath = fullPath + " :: " + key;
        if (_loadedAnimationPaths.Contains(memberPath)) return;
        var definition = AnimationDefinitionSerializer.Load(path, key);
        _loadedAnimationPaths.Add(memberPath);
        AddAnimationDefinition(definition, new AuthoringResource(fullPath, AssetTypes.AnimationDefinition));
    }

    private void AddAnimationDefinition(AnimationDefinition definition, AuthoringResource resource)
    {
        if (_animationDefinitions.Any(existing => string.Equals(existing.Key, definition.Key, StringComparison.OrdinalIgnoreCase)))
        {
            _sourceWarnings.Add($"Duplicate GANI source for '{definition.Key}'.");
            return;
        }
        _animationDefinitions.Add(definition);
        var sheets = new List<SpriteTilesheetSource>();
        _animationTilesheets[definition.Key] = sheets;
        foreach (var reference in definition.TilesheetSources)
            TryLoad(() => sheets.Add(SpriteTilesheetSource.LoadResource(
                AuthoringResourceResolver.TilesheetSource(reference, resource.BaseDirectory), reference.Tilesheet, _packages)));
    }

    /// <summary>Loads the members of loose scene files for authoring and records their source paths in the document.</summary>
    /// <param name="paths">Definition files to load; relative paths use the process's current directory.</param>
    /// <remarks>Earlier additions remain if a later file fails to load.</remarks>
    public void AddSceneSources(IEnumerable<string> paths)
    {
        using var transaction = Document.History.BeginTransaction("Add sources");
        try
        {
            foreach (var path in paths) foreach (var member in SceneDefinitionSerializer.LoadAll(path)) { var scene = LoadScene(path, member.ID); Document.SetLooseSceneSource(scene.ID, path); }
            RefreshSelection();

        }
        finally { transaction.Commit(); }
    }
    private SceneDefinition LoadScene(string path, string? identity = null)
    {
        var scene = SceneDefinitionSerializer.Load(path, identity);
        if (!_loadedScenePaths.Add(Path.GetFullPath(path) + " :: " + scene.ID)) return scene;
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

    /// <summary>Loads the members of loose tilesheet files for authoring and records their source paths in the document.</summary>
    /// <param name="paths">Definition files to load; relative paths use the process's current directory.</param>
    /// <remarks>Earlier additions remain if a later file fails to load.</remarks>
    public void AddTilesheetSources(IEnumerable<string> paths)
    {
        using var transaction = Document.History.BeginTransaction("Add sources");
        try
        {
            foreach (var path in paths) foreach (var member in TilesheetDefinitionSerializer.LoadAll(path)) { var source = LoadTilesheet(path, member.Name); Document.SetLooseTilesheetSource(source.Definition.Name, path); }
            RefreshSelection();

        }
        finally { transaction.Commit(); }
    }
    private SpriteTilesheetSource LoadTilesheet(string path, string? identity = null)
    {
        var fullPath = Path.GetFullPath(path);
        var memberPath = fullPath + " :: " + identity;
        if (_loadedTilesheetPaths.Contains(memberPath)) return _sources.First(source => source.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase) && source.Definition.Name == identity);
        var source = SpriteTilesheetSource.LoadMember(path, identity, _packages);
        _loadedTilesheetPaths.Add(memberPath);
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
                        _ = _thumbnails.Handle;
                        _thumbnails.Images.Add(thumbnail);
                        frameNode.ImageIndex = frameNode.SelectedImageIndex = _thumbnails.Images.Count - 1;
                    }
                    node.Nodes.Add(frameNode);
                }
        }
        NavigationSizing.RefreshImages(_frames);
        root.Expand();
    }

    /// <summary>Refreshes the sprite list and selects the supplied document instance.</summary>
    /// <param name="sprite">The sprite instance to select in the refreshed list.</param>
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
        _syncingAnimationPicker = true;
        try
        {
            _animationPicker.Items.Clear();
            var noAnimation = new AnimationChoice(null);
            _animationPicker.Items.Add(noAnimation);
            foreach (var key in _animationDefinitions.Select(a => a.Key).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                _animationPicker.Items.Add(new AnimationChoice(key));
            var current = SelectedSprite?.AnimationKey;
            var selectedChoice = string.IsNullOrWhiteSpace(current)
                ? noAnimation
                : _animationPicker.Items.Cast<AnimationChoice>().FirstOrDefault(choice =>
                    choice.Key is not null && string.Equals(choice.Key, current, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(current) && selectedChoice is null)
            {
                selectedChoice = new AnimationChoice(current);
                _animationPicker.Items.Add(selectedChoice);
            }
            _animationPicker.SelectedItem = selectedChoice;
        }
        finally { _syncingAnimationPicker = false; }
        _properties.SelectedObject = SelectedSprite is { } selected ? new SpriteProperties(selected, Document) : null;
        var entry = SelectedSprite;
        var matches = _sources.Where(source => source.Definition.Name == entry?.Frame?.Tilesheet).ToList();
        var scenes = _sceneDefinitions.Where(scene => scene.ID == entry?.SceneId).ToList();
        var layer = scenes.Count == 1 ? scenes[0].Layers.FirstOrDefault(layer => layer.ID == entry?.SceneLayerId) : null;
        SpriteFrameDefinition? previewFrame = entry?.Frame;
        if (entry?.StartAnimation == true && entry.AnimationKey is { } animationKey)
        {
            var animation = _animationDefinitions.FirstOrDefault(item => string.Equals(item.Key, animationKey, StringComparison.OrdinalIgnoreCase));
            if (animation?.Frames.FirstOrDefault() is { } frame)
            {
                previewFrame = new() { Tilesheet = frame.Tilesheet, RegionName = frame.RegionName, XTile = frame.XTile, YTile = frame.YTile };
                matches = animation.TilesheetSources.Any(source => source.Tilesheet == frame.Tilesheet)
                    ? _animationTilesheets[animationKey].Where(source => source.Definition.Name == frame.Tilesheet).ToList()
                    : _sources.Where(source => source.Definition.Name == frame.Tilesheet).ToList();
            }
        }
        _preview.SetSelection(entry, matches.Count == 1 ? matches[0] : null, layer, previewFrame);
        UpdateValidation();
    }
    private bool _historyRefreshPending;
    private object[] _historySources = [];
    private SpriteDefinition? _historyDefinition;

    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (_historyRefreshPending || !IsHandleCreated) return;
        _historyRefreshPending = true;
        BeginInvoke(() =>
        {
            _historyRefreshPending = false;
            if (IsDisposed) return;
            var sources = Document.Definition.TilesheetSources.Cast<object>().Concat(Document.Definition.AnimationSources).Concat(Document.Definition.SceneSources).ToArray();
            if (!ReferenceEquals(_historyDefinition, Document.Definition) || !_historySources.SequenceEqual(sources))
                RefreshSelectedDefinition();
            else RefreshEntries();
            _historyDefinition = Document.Definition;
            _historySources = sources;
        });
    }
    /// <summary>Moves focus out of the property editor and validates pending control input without saving the document.</summary>
    /// <returns><see langword="true"/> if control validation succeeds and the property editor releases focus; otherwise, <see langword="false"/>.</returns>
    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus && ValidateChildren();
    }
    /// <summary>Validates the document and refreshes the diagnostics pane.</summary>
    /// <returns>The document's structural validation errors. Source and preview warnings shown in the pane are not included.</returns>
    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        var lines = errors.Select(error => "ERROR: " + error).ToList();
        if (errors.Count == 0) lines.Add("VALID: GSPR structural validation passed.");
        lines.AddRange(_sourceWarnings.Select(warning => "WARNING: " + warning));
        foreach (var path in _loadedScenePaths.Concat(_loadedTilesheetPaths))
            if (!File.Exists(path)) lines.Add($"WARNING: Loaded authoring source no longer exists: {path}");
        lines.AddRange(_sources.Concat(_animationTilesheets.Values.SelectMany(sources => sources)).Where(source => source.PreviewWarning is not null)
            .Select(source => $"WARNING: GTS '{source.Definition.Name}': {source.PreviewWarning}"));
        foreach (var entry in Document.Definition.Sprites)
        {
            if (!string.IsNullOrWhiteSpace(entry.AnimationKey) &&
                !_animationDefinitions.Any(animation => string.Equals(animation.Key, entry.AnimationKey, StringComparison.OrdinalIgnoreCase)))
                lines.Add($"WARNING: Sprite '{entry.Nickname}': GANI animation '{entry.AnimationKey}' has no loaded authoring source.");
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
    /// <summary>Shows and activates the named pane.</summary>
    /// <param name="name">Case-insensitive pane title from <see cref="PaneNames"/>.</param>
    /// <returns>True if the pane exists and has not been disposed; otherwise false.</returns>
    public bool ShowPane(string name) => _workspace.ShowPane(name);
    /// <summary>Checks whether the named pane is currently visible.</summary>
    /// <param name="name">Case-insensitive pane title from <see cref="PaneNames"/>.</param>
    /// <returns>True if the pane exists, is not disposed, and is not hidden.</returns>
    public bool IsPaneVisible(string name) => _workspace.IsPaneVisible(name);
    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes() => _workspace.ShowAllPanes();
    /// <summary>
    /// Restores the default editor pane layout.
    /// </summary>
    public void ResetLayout() => _workspace.ResetLayout();
    /// <summary>Releases previews, unsubscribes document events, and disposes the package cache if this control created it.</summary>
    /// <param name="disposing">True to release managed resources as well as the base control.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Document.Changed -= DocumentChanged; foreach (var source in _sources) source.Dispose(); _thumbnails.Dispose(); }
        if (disposing) ClearAnimationTilesheets();
        if (disposing && _ownsPackages) _packages.Dispose();
        base.Dispose(disposing);
    }
    /// <inheritdoc/>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        UndoRedoMenu.TryShortcut(this, keyData, Document.History, CommitEdits) || base.ProcessCmdKey(ref msg, keyData);
}
