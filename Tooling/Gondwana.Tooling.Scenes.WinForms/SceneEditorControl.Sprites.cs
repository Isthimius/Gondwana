using Gondwana.Assets;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Scenes.WinForms;

public sealed partial class SceneEditorControl
{
    private sealed record SpriteCandidate(ResourceAddress Address, SpriteDefinition Member,
        SpriteInstanceDefinition Sprite, ISceneSpriteOwner? Owner);
    private sealed record PlacementGesture(ResourceDragData<SpriteInstanceDrag> Data,
        AuthoringResourceReadSnapshot Reads, SceneDefinition Scene, ResourceAddress Target, SceneLayerDefinition Layer,
        LayerPlacementGeometry Geometry);
    private readonly record struct LayerPlacementGeometry(string Id, Point Origin, int Width, int Height, int Columns, int Rows,
        CoordinateSystemTypes Projection)
    {
        internal LayerPlacementGeometry(SceneLayerDefinition layer) : this(layer.ID, layer.OriginPx, layer.TileWidth,
            layer.TileHeight, layer.Columns, layer.Rows, layer.CoordinateSystemType)
        { }
    }

    private readonly AssetPackageCatalog _spriteDiscoveryPackages = new();
    private readonly SceneSpriteArtworkCache _spriteArtwork = new();
    private readonly Dictionary<AssetLocation, IReadOnlyList<SpriteDefinition>> _savedSprites = [];
    private readonly HashSet<string> _explicitSpriteSources = new(StringComparer.OrdinalIgnoreCase);
    private Func<IReadOnlyList<ISceneSpriteOwner>>? _getSpriteOwners;
    private IReadOnlyList<SceneSpriteVisual> _spriteVisuals = [];
    private SceneSpriteVisual? _selectedSprite;
    private SceneSpriteVisual? _spriteSelection;
    private PlacementGesture? _spriteMove;
    private PlacementGesture? _spriteDrop;
    private SceneDefinition? _spriteScene;
    private string? _spriteContentRoot;
    private string? _spriteDiscoveryWarning;
    private bool _spriteSourcesScanned;
    private string[] _neighborFiles = [];
    private readonly Dictionary<AssetTypes, List<ResourceAddress>> _neighborArtwork = [];
    private bool _refreshSpritesPending;

    /// <summary>Supplies live GSPR editing sessions from the host's own document registry.</summary>
    public void SetSpriteOwners(Func<IReadOnlyList<ISceneSpriteOwner>> getOwners)
    {
        _getSpriteOwners = getOwners;
        InvalidateSpriteOwners(reloadSaved: true);
    }

    /// <summary>Refreshes sprite preview data after the host changes a live source or its registry.</summary>
    public void InvalidateSpriteOwners(bool reloadSaved = false)
    {
        if (reloadSaved) _spriteSourcesScanned = false;
        if (IsDisposed || Disposing || _refreshSpritesPending || !IsHandleCreated) return;
        _refreshSpritesPending = true;
        BeginInvoke(() =>
        {
            _refreshSpritesPending = false;
            if (!IsDisposed && !Disposing && Visible) RefreshSpriteVisuals();
        });
    }

    /// <summary>Cancels an active sprite drag when the host switches or closes a document.</summary>
    public void CancelSpritePlacement()
    {
        _preview.CancelSpriteGesture();
        _spriteMove = null;
        ClearSpriteDrop();
    }

    /// <summary>The GSPR history selected in the preview, when the sprite is editable.</summary>
    public EditHistory? SelectedSpriteHistory => SelectedSpriteOwner?.History;

    /// <summary>Commits pending edits in the document selected for scene-preview sprite history.</summary>
    public bool CommitSpriteHistoryEdits() => SelectedSpriteOwner?.CommitEdits() == true;

    /// <summary>Notifies the host that Undo and Redo may need to use another document.</summary>
    public event Action? SpriteHistorySelectionChanged;

    private bool CommitPreviewHistoryEdits() =>
        SelectedSpriteHistory is not null ? CommitSpriteHistoryEdits() : CommitEdits();

    private ISceneSpriteOwner? SelectedSpriteOwner =>
        _spriteSelection is { Restriction: null, Owner: { CanPlace: true } owner } ? owner : null;

    private void WireSpritePlacement()
    {
        _preview.SpriteSelected += SelectPreviewSprite;
        _preview.HandleCreated += (_, _) => InvalidateSpriteOwners();
        _preview.SpriteMoved += (_, position) =>
        {
            var gesture = _spriteMove;
            _spriteMove = null;
            if (gesture is not null) CommitSpritePlacement(gesture, position);
        };
        _preview.SpriteMoveCanceled += () => _spriteMove = null;
        VisibleChanged += (_, _) =>
        {
            if (Visible) InvalidateSpriteOwners(reloadSaved: true);
            else CancelSpritePlacement();
        };
    }

    private void AddSpritePreviewButtons(ToolStrip bar)
    {
        var show = new ToolStripButton("Show Sprites") { CheckOnClick = true, Checked = true };
        show.CheckedChanged += (_, _) => _preview.ShowSprites(show.Checked);
        bar.Items.Add(show);
        bar.Items.Add("Sprite Sources…", null, (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = Document.BaseDirectory,
                Filter = "Sprite sources (*.gspr;*.gaf)|*.gspr;*.gaf",
                Multiselect = true,
                Title = "Load saved sprite previews"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            foreach (var path in dialog.FileNames) _explicitSpriteSources.Add(path);
            _spriteSourcesScanned = false;
            RefreshSpriteVisuals();
        });
    }

    private ResourceAddress? SceneAddress()
    {
        var location = Document.PackedLocation;
        if (location is null && Document.FilePath is { } file)
            location = new AssetLocation(AssetTypes.SceneDefinition, file);
        return location is null || string.IsNullOrWhiteSpace(Definition.ID) ? null : new(location, Definition.ID);
    }

    private static bool ViewerDiscovers(AssetLocation sprites, ResourceAddress scene) =>
        string.Equals(Path.GetDirectoryName(sprites.Path), Path.GetDirectoryName(scene.Location.Path),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Path.GetExtension(sprites.Path), sprites.AssetEntryName is null ? ".gspr" : ".gaf",
            StringComparison.OrdinalIgnoreCase);

    private void ScanSavedSprites(string? root, IReadOnlyList<ISceneSpriteOwner> owners)
    {
        _spriteContentRoot = root;
        _spriteSourcesScanned = true;
        _spriteDiscoveryWarning = null;
        _savedSprites.Clear();
        _neighborArtwork.Clear();
        _neighborFiles = [];
        _spriteDiscoveryPackages.Clear();

        IEnumerable<string> paths = _explicitSpriteSources;
        if (root is not null && Directory.Exists(root))
        {
            try
            {
                _neighborFiles = Directory.GetFiles(root);
                paths = paths.Concat(_neighborFiles);
            }
            catch (Exception ex) when (IsAuthoringException(ex)) { _spriteDiscoveryWarning = ex.Message; }
        }
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal))
        {
            bool explicitSource = _explicitSpriteSources.Contains(path);
            var packages = explicitSource ? _packages : _spriteDiscoveryPackages;
            try
            {
                var resources = Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".gspr" => new[] { new AuthoringResource(path, AssetTypes.SpriteDefinition) },
                    ".gaf" => packages.GetResources(path, AssetTypes.SpriteDefinition),
                    _ => []
                };
                foreach (var resource in resources)
                {
                    var location = new AssetLocation(resource.AssetType, resource.Path, resource.EntryName);
                    if (owners.Any(owner => owner.Location == location)) continue;
                    try { _savedSprites.Add(location, new AuthoringResourceResolver(packages).LoadSprites(resource)); }
                    catch (Exception ex) when (IsAuthoringException(ex)) { _spriteDiscoveryWarning = ex.Message; }
                }
            }
            catch (Exception ex) when (IsAuthoringException(ex) || ex is OperationCanceledException)
            {
                _spriteDiscoveryWarning = ex.Message;
            }
        }
    }

    private ResourceAddress? FindNeighborArtwork(AssetTypes type, string member)
    {
        if (!_neighborArtwork.TryGetValue(type, out var available))
        {
            available = [];
            _neighborArtwork.Add(type, available);
            string extension = type == AssetTypes.TilesheetDefinition ? ".gts" : ".gani";
            var resolver = new AuthoringResourceResolver(_spriteDiscoveryPackages);
            foreach (string path in _neighborFiles)
            {
                try
                {
                    var resources = Path.GetExtension(path).ToLowerInvariant() switch
                    {
                        ".gaf" => _spriteDiscoveryPackages.GetResources(path, type),
                        var candidate when candidate == extension => new[] { new AuthoringResource(path, type) },
                        _ => []
                    };
                    foreach (var resource in resources)
                        foreach (string identity in resolver.GetMembers(resource))
                            available.Add(new(resource, identity));
                }
                catch (Exception ex) when (IsAuthoringException(ex)) { }
            }
        }

        var matches = available.Where(address => DefinitionReferenceQuery.MembersEqual(type, address.Member!, member)).ToArray();
        var loose = matches.Where(address => address.Location.AssetEntryName is null).ToArray();
        var preferred = loose.Length > 0 ? loose : matches;
        return preferred.Length == 1 ? preferred[0] : null;
    }

    private IReadOnlyList<ISceneSpriteOwner> LiveOwners() => _getSpriteOwners?.Invoke() ?? [];

    private IEnumerable<SpriteCandidate> Candidates(IReadOnlyList<ISceneSpriteOwner> owners)
    {
        foreach (var (location, members) in _savedSprites)
            if (!owners.Any(owner => owner.Location == location))
                foreach (var candidate in FromMembers(location, members, null))
                    yield return candidate;

        foreach (var owner in owners)
            if (owner.Location is { } location)
                foreach (var candidate in FromMembers(location, owner.Definitions, owner))
                    yield return candidate;

        static IEnumerable<SpriteCandidate> FromMembers(AssetLocation location,
            IReadOnlyList<SpriteDefinition> definitions, ISceneSpriteOwner? owner) =>
            definitions.SelectMany(member => member.Sprites.Select(sprite =>
                new SpriteCandidate(new ResourceAddress(location, member.Key), member, sprite, owner)));
    }

    private static bool ConflictingInstance(SpriteInstanceDefinition left, SpriteInstanceDefinition right) =>
        (left.Id != Guid.Empty && left.Id == right.Id) ||
        (!string.IsNullOrWhiteSpace(left.Nickname) && left.Nickname == right.Nickname);

    private static bool ConflictsWithPlacement(SpriteCandidate sprite, SpriteCandidate other, ResourceAddress target) =>
        !(sprite.Address == other.Address && ReferenceEquals(sprite.Sprite, other.Sprite)) &&
        ViewerDiscovers(other.Address.Location, target) &&
        (sprite.Address.Location.AssetEntryName is not null || other.Address.Location.AssetEntryName is null) &&
        ConflictingInstance(sprite.Sprite, other.Sprite);

    private static string? SceneReferenceRestriction(SpriteCandidate sprite, ResourceAddress target)
    {
        var references = sprite.Member.SceneSources.Where(source => source.SceneId == target.Member).Take(2).ToArray();
        if (references.Length > 1) return "Multiple scene sources use this ID. Resolve them in GSPR before moving the sprite.";
        if (references.Length == 0) return null;
        try
        {
            var address = AuthoringResourceResolver.SpriteSceneSource(references[0], Path.GetDirectoryName(sprite.Address.Location.Path)!);
            return address == target ? null : "This sprite references a different scene source with the same ID.";
        }
        catch (Exception ex) when (IsAuthoringException(ex)) { return ex.Message; }
    }

    private void RefreshSpriteVisuals()
    {
        var scene = SceneAddress();
        string? root = scene is null ? null : Path.GetDirectoryName(scene.Location.Path);
        var owners = LiveOwners();
        if (!_spriteSourcesScanned || !string.Equals(root, _spriteContentRoot, StringComparison.OrdinalIgnoreCase))
            ScanSavedSprites(root, owners);

        if (!ReferenceEquals(_spriteScene, Definition))
        {
            CancelSpritePlacement();
            ClearPreviewSpriteSelection();
            _spriteScene = Definition;
        }

        var matching = Candidates(owners).Where(item => item.Sprite.SceneId == Definition.ID).ToArray();
        var loose = matching.Where(item => item.Address.Location.AssetEntryName is null &&
            scene is not null && ViewerDiscovers(item.Address.Location, scene)).ToArray();
        var visible = matching.Where(item => item.Sprite.Visible &&
            (item.Address.Location.AssetEntryName is null || scene is null || !ViewerDiscovers(item.Address.Location, scene) ||
             !loose.Any(other => ConflictingInstance(item.Sprite, other.Sprite)))).ToArray();

        var visuals = new List<SceneSpriteVisual>();
        foreach (var item in visible)
        {
            var layers = Definition.Layers.Where(layer => layer.ID == item.Sprite.SceneLayerId).Take(2).ToArray();
            if (layers.Length != 1 || !float.IsFinite(item.Sprite.Position.X) || !float.IsFinite(item.Sprite.Position.Y) ||
                !float.IsFinite(item.Sprite.Rotation) || item.Sprite.RenderSize.Width < 0 || item.Sprite.RenderSize.Height < 0)
                continue;

            string? restriction = scene is null ? "Save this scene before placing sprites." :
                !ViewerDiscovers(item.Address.Location, scene) ? OutsideViewerScope() :
                item.Owner is not { CanPlace: true } ? "Read-only sprite. Open its GSPR document in Studio to edit it." :
                item.Sprite.Id == Guid.Empty || matching.Count(other => other.Address == item.Address && other.Sprite.Id == item.Sprite.Id) != 1
                    ? "This GSPR member needs a unique sprite ID." : SceneReferenceRestriction(item, scene);
            if (restriction is null && matching.Any(other => ConflictsWithPlacement(item, other, scene!)))
                restriction = "Multiple neighboring sprites use this ID or nickname. Resolve the conflict in GSPR before placing it.";

            visuals.Add(CreateVisual(item, layers[0], restriction));
        }

        _spriteVisuals = visuals;
        var previousHistory = SelectedSpriteHistory;
        if (_spriteSelection is { Owner: { } previousOwner } &&
            !owners.Any(owner => ReferenceEquals(owner.Session, previousOwner.Session)))
            _spriteSelection = null;
        _selectedSprite = _spriteSelection is { } previous ? visuals.FirstOrDefault(previous.SameInstance) : null;
        if (_selectedSprite is not null) _spriteSelection = _selectedSprite;
        _preview.SetSprites(visuals);
        _preview.SelectSprite(_selectedSprite);
        if (_spriteMove is { } gesture && (!PlacementTargetIsCurrent(gesture, requireSelection: false) || ResolveLiveSprite(gesture.Data.Payload!) is null))
            CancelSpritePlacement();
        UpdateSpriteStatus();
        if (previousHistory != SelectedSpriteHistory) SpriteHistorySelectionChanged?.Invoke();
    }

    private SceneSpriteVisual CreateVisual(SpriteCandidate candidate, SceneLayerDefinition layer, string? restriction)
    {
        SceneSpriteArtwork? art = null;
        try
        {
            // Discovery uses a non-interactive catalog. Explicit sources and live
            // editors use the host's normal archive authentication and cache.
            var packages = candidate.Owner is not null || _explicitSpriteSources.Contains(candidate.Address.Location.Path)
                ? _packages : _spriteDiscoveryPackages;
            art = _spriteArtwork.Find(candidate.Address.Location, candidate.Member, candidate.Sprite, packages, FindTilesheet, FindAnimation, FindNeighborArtwork);
        }
        catch (Exception ex) when (IsAuthoringException(ex) || ex is OperationCanceledException or OverflowException) { }
        return new(candidate.Address, candidate.Sprite, layer, art, candidate.Owner, restriction);
    }

    private bool SelectPreviewSprite(SceneSpriteVisual? visual)
    {
        if (visual is null)
        {
            ClearPreviewSpriteSelection();
            return false;
        }
        _selectedSprite = _spriteSelection = visual;
        _spriteMove = null;
        bool canMove = false;
        if (visual is { Restriction: null, Owner: { CanPlace: true } owner, Address.Member: { } member } && owner.CommitEdits())
        {
            owner.Select(member, visual.Sprite.Id);
            var payload = new SpriteInstanceDrag(visual.Address, visual.Sprite.Id, owner.Session, owner.Revision);
            if (ResolveLiveSprite(payload) is not null && SceneAddress() is { } target)
            {
                try
                {
                    _spriteMove = NewPlacement(new ResourceDragData<SpriteInstanceDrag>(payload), target, visual.Layer);
                    canMove = true;
                }
                catch (Exception ex) when (IsAuthoringException(ex))
                {
                    UpdateSpriteStatus(ex.Message);
                    SpriteHistorySelectionChanged?.Invoke();
                    return false;
                }
            }
        }
        UpdateSpriteStatus();
        SpriteHistorySelectionChanged?.Invoke();
        return canMove;
    }

    private void ClearPreviewSpriteSelection()
    {
        if (_spriteSelection is null) return;
        _selectedSprite = _spriteSelection = null;
        CancelSpritePlacement();
        _preview.SelectSprite(null);
        UpdateSpriteStatus();
        SpriteHistorySelectionChanged?.Invoke();
    }

    private void UpdateSpriteStatus(string? message = null)
    {
        _preview.SpriteStatus = message ?? (_selectedSprite is { } selected
            ? $"Sprite: {selected.Sprite.Nickname ?? selected.Sprite.Id.ToString()}  {selected.Sprite.Position.X:0.###}, {selected.Sprite.Position.Y:0.###}" +
                (selected.Restriction is { } restriction ? $"  {restriction}" : "  Hold Alt for precision.") +
                (selected.Artwork is null ? "  Artwork unresolved." : string.Empty)
            : _spriteDiscoveryWarning ?? (_spriteVisuals.Count > 0 ? "Click a sprite to select it. Drag to move. Hold Alt for precision." : null));
        _preview.Invalidate();
    }

    private PlacementGesture NewPlacement(ResourceDragData<SpriteInstanceDrag> data, ResourceAddress target, SceneLayerDefinition layer)
    {
        var reads = data.BeginRead();
        reads.Observe(target.Location.Path);
        return new(data, reads, Definition, target, layer, new(layer));
    }

    private bool PlacementTargetIsCurrent(PlacementGesture gesture, bool requireSelection) =>
        !IsDisposed && !Disposing && Enabled && _preview.Visible && !_preview.HasGesture &&
        !Document.History.InTransaction && !Document.History.IsExecuting &&
        ReferenceEquals(Definition, gesture.Scene) && SceneAddress() == gesture.Target &&
        Definition.Layers.Contains(gesture.Layer) && IsLayerVisibleInPreview(gesture.Layer) &&
        new LayerPlacementGeometry(gesture.Layer) == gesture.Geometry &&
        (!requireSelection || ReferenceEquals(_selectedLayer, gesture.Layer) && ReferenceEquals(_structure.SelectedNode?.Tag, gesture.Layer));

    private SpriteCandidate? ResolveLiveSprite(SpriteInstanceDrag drag)
    {
        if (drag.SpriteId == Guid.Empty || drag.Address.Location.AssetType != AssetTypes.SpriteDefinition ||
            string.IsNullOrWhiteSpace(drag.Address.Member)) return null;
        var owners = LiveOwners().Where(owner => owner.Location == drag.Address.Location).Take(2).ToArray();
        if (owners.Length != 1 || !owners[0].CanPlace || !ReferenceEquals(owners[0].Session, drag.Session) ||
            owners[0].Revision != drag.Revision || owners[0].History.InTransaction || owners[0].History.IsExecuting)
            return null;
        var members = owners[0].Definitions.Where(member => member.Key == drag.Address.Member).Take(2).ToArray();
        if (members.Length != 1) return null;
        var instances = members[0].Sprites.Where(sprite => sprite.Id == drag.SpriteId).Take(2).ToArray();
        return instances.Length == 1 ? new(drag.Address, members[0], instances[0], owners[0]) : null;
    }

    private bool CommitSpritePlacement(PlacementGesture gesture, PointF position, bool requireSelection = false)
    {
        try
        {
            if (!PlacementTargetIsCurrent(gesture, requireSelection) || gesture.Data.Payload is not { } payload)
                return false;
            if (!ViewerDiscovers(payload.Address.Location, gesture.Target))
                throw new InvalidDataException(OutsideViewerScope());
            if (ResolveLiveSprite(payload) is not { } sprite)
                throw new InvalidDataException("The owning GSPR document or sprite changed. Start a new placement.");
            gesture.Reads.Verify();
            var resolver = new AuthoringResourceResolver(_packages, gesture.Reads.Observe);
            var saved = resolver.ResolveSceneLayer(new(gesture.Target, gesture.Layer.ID));
            if (new LayerPlacementGeometry(saved.Layers.Single(layer => layer.ID == gesture.Layer.ID)) != gesture.Geometry)
                throw new InvalidDataException("Save the changed scene layer before placing sprites on it.");
            gesture.Reads.Verify();
            if (!PlacementTargetIsCurrent(gesture, requireSelection) || ResolveLiveSprite(payload) is null)
                return false;

            if (Candidates(LiveOwners()).Any(other => other.Sprite.SceneId == Definition.ID &&
                ConflictsWithPlacement(sprite, other, gesture.Target)))
                throw new InvalidDataException("A neighboring GSPR already assigns this sprite ID or nickname to the scene. Resolve the conflict before placing it.");

            bool changed = sprite.Owner!.Place(payload, gesture.Target, gesture.Layer.ID, position);
            if (payload.Address.Member is { } member) sprite.Owner.Select(member, payload.SpriteId);
            _spriteSelection = new(sprite.Address, sprite.Sprite, gesture.Layer, null, sprite.Owner, null);
            RefreshSpriteVisuals();
            SpriteHistorySelectionChanged?.Invoke();
            _preview.Focus();
            return changed;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) when (IsAuthoringException(ex))
        {
            UpdateSpriteStatus(ex.Message);
            return false;
        }
    }

    private static string OutsideViewerScope() =>
        "Scene Viewer finds GSPR files and GAF archives next to the scene only. Save the GSPR beside this scene to place it.";

    private void SpriteDragEnter(ResourceDragData<SpriteInstanceDrag> data, DragEventArgs e)
    {
        if (_preview.HasGesture || _preview.HasSpriteGesture || (e.AllowedEffect & DragDropEffects.Move) == 0) return;
        if (SceneAddress() is not { } target) { UpdateSpriteStatus("Save this scene before placing sprites."); return; }
        if (!ViewerDiscovers(data.Address.Location, target)) { UpdateSpriteStatus(OutsideViewerScope()); return; }
        if (data.Payload is not { } drag || ResolveLiveSprite(drag) is not { } sprite)
        {
            UpdateSpriteStatus("Open the owning GSPR document and drag a sprite with a unique ID.");
            return;
        }
        if (_selectedLayer is not { } layer || !ReferenceEquals(_structure.SelectedNode?.Tag, layer) || !IsLayerVisibleInPreview(layer))
        {
            UpdateSpriteStatus("Select a visible scene layer before placing the sprite.");
            return;
        }

        try
        {
            _spriteDrop = NewPlacement(data, target, layer);
            _dropSpriteVisual = CreateVisual(sprite, layer, null);
            UpdateSpriteStatus("Release to place the sprite. Hold Alt for precision.");
            SpriteDragOver(e);
        }
        catch (Exception ex) when (IsAuthoringException(ex)) { UpdateSpriteStatus(ex.Message); }
    }

    private SceneSpriteVisual? _dropSpriteVisual;

    private bool SpriteDropPosition(PlacementGesture? gesture, DragEventArgs e, out PointF position)
    {
        const int AltKey = 32;
        position = default;
        return gesture is not null && ReferenceEquals(gesture.Data, e.Data) &&
            (e.AllowedEffect & DragDropEffects.Move) != 0 && PlacementTargetIsCurrent(gesture, requireSelection: true) &&
            ResolveLiveSprite(gesture.Data.Payload!) is not null &&
            _preview.TryGetSpriteDropPosition(gesture.Layer, new Point(e.X, e.Y), (e.KeyState & AltKey) == 0, out position);
    }

    private void SpriteDragOver(DragEventArgs e)
    {
        bool valid = SpriteDropPosition(_spriteDrop, e, out var position);
        _preview.SetSpriteGhost(valid ? _dropSpriteVisual : null, position);
        e.Effect = valid ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void SpriteDragDrop(DragEventArgs e)
    {
        var gesture = _spriteDrop;
        bool valid = SpriteDropPosition(gesture, e, out var position);
        ClearSpriteDrop();
        e.Effect = valid && CommitSpritePlacement(gesture!, position, requireSelection: true) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void ClearSpriteDrop()
    {
        if (_spriteDrop is null) return;
        _spriteDrop = null;
        _dropSpriteVisual = null;
        _preview.SetSpriteGhost(null);
        UpdateSpriteStatus();
    }

    private void DisposeSpritePreview()
    {
        CancelSpritePlacement();
        _spriteArtwork.Dispose();
        _spriteDiscoveryPackages.Dispose();
    }
}
