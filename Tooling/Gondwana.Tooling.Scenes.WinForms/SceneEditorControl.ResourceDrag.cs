using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Scenes.WinForms;

public sealed partial class SceneEditorControl
{
    private sealed record ResourceHover(ResourceDragData Data, SceneDefinition Scene,
        SceneLayerDefinition Layer, string? FilePath, AssetLocation? PackedLocation);

    private sealed record DropAssignment(SceneFrameDefinition Frame, TilesheetDefinition? Tilesheet, AnimationDefinition? Animation)
    {
        internal bool Matches(SceneLayerTileDefinition? tile) =>
            tile is not null && SceneDocument.FramesMatch(tile.Frame, Frame) &&
            (Animation is null || tile.AnimationKey == Animation.Key && tile.EnableAnimator && tile.StartAnimation);
    }

    private ResourceHover? _resourceHover;
    private string? _resourceDropError;

    private void WireResourceDragDrop()
    {
        _sourceTree.ItemDrag += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || e.Item is not TreeNode { Tag: FrameTag frame })
                return;

            var address = new ResourceAddress(frame.Source.Resource, frame.Source.Definition.Name);
            var payload = new TilesheetFrameDrag(address, frame.Region.Name, frame.X, frame.Y);
            _sourceTree.DoDragDrop(new ResourceDragData<TilesheetFrameDrag>(payload), DragDropEffects.Copy);
        };
        _animationList.ItemDrag += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || e.Item is not ListViewItem { Tag: SceneAnimationSource source })
                return;

            var payload = new AnimationDrag(new ResourceAddress(source.Resource, source.Definition.Key));
            _animationList.DoDragDrop(new ResourceDragData<AnimationDrag>(payload), DragDropEffects.Copy);
        };

        _structure.ItemDrag += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Item is TreeNode { Tag: SceneLayerDefinition layer } &&
                CreateSceneLayerDrag(layer) is { } payload)
                _structure.DoDragDrop(new ResourceDragData<SceneLayerDrag>(payload), DragDropEffects.Copy);
        };

        _preview.AllowDrop = true;
        _preview.DragEnter += ResourceDragEnter;
        _preview.DragOver += ResourceDragOver;
        _preview.DragLeave += (_, _) => ClearResourceDrop();
        _preview.DragDrop += ResourceDragDrop;
    }

    internal SceneLayerDrag? CreateSceneLayerDrag(SceneLayerDefinition layer)
    {
        if (Document.IsDirty || !Definition.Layers.Contains(layer) ||
            string.IsNullOrWhiteSpace(Definition.ID) || string.IsNullOrWhiteSpace(layer.ID))
            return null;

        var location = Document.PackedLocation;
        if (location is null && Document.FilePath is { } path)
            location = new AssetLocation(AssetTypes.SceneDefinition, path);

        return location is null ? null : new(new ResourceAddress(location, Definition.ID), layer.ID);
    }

    private static ResourceDragData? GetResourceData(DragEventArgs e) =>
        (e.AllowedEffect & DragDropEffects.Copy) != 0 &&
        e.Data is ResourceDragData<TilesheetFrameDrag> or ResourceDragData<AnimationDrag>
            ? (ResourceDragData)e.Data : null;

    private void ResourceDragEnter(object? sender, DragEventArgs e)
    {
        ClearResourceDrop();
        e.Effect = DragDropEffects.None;
        if (_preview.HasGesture || _selectedLayer is not { } layer || GetResourceData(e) is not { } data)
            return;

        try
        {
            var address = data.Address;
            var type = data is ResourceDragData<TilesheetFrameDrag> ? AssetTypes.TilesheetDefinition : AssetTypes.AnimationDefinition;
            if (address.Location.AssetType != type)
                throw new InvalidDataException("Drag a specific saved tilesheet frame or animation member.");

            // Authentication and member selection happen on release.
            if (address.Member is not null)
                NeedsSource(address);
            data.BeginRead();
            _resourceHover = new(data, Definition, layer, Document.FilePath, Document.PackedLocation);
            SetResourceDropError(null);
            ResourceDragOver(sender, e);
        }
        catch (Exception ex) when (IsAuthoringException(ex))
        {
            SetResourceDropError(ex.Message);
        }
    }

    private void ResourceDragOver(object? sender, DragEventArgs e)
    {
        bool valid = TryGetDropCell(_resourceHover, e, out var cell);
        _preview.SetResourceDropCell(valid ? _resourceHover!.Layer : null, cell);
        e.Effect = valid ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private bool TryGetDropCell(ResourceHover? hover, DragEventArgs e, out Point cell)
    {
        cell = default;
        return hover is not null && ReferenceEquals(hover.Data, GetResourceData(e)) &&
            !IsDisposed && !Disposing && Enabled && !_preview.HasGesture &&
            !Document.History.InTransaction && !Document.History.IsExecuting &&
            ReferenceEquals(Definition, hover.Scene) && ReferenceEquals(_selectedLayer, hover.Layer) &&
            ReferenceEquals(_structure.SelectedNode?.Tag, hover.Layer) && Definition.Layers.Contains(hover.Layer) &&
            Document.FilePath == hover.FilePath && ReferenceEquals(Document.PackedLocation, hover.PackedLocation) &&
            _preview.TryGetResourceDropCell(hover.Layer, new Point(e.X, e.Y), out cell);
    }

    private void ClearResourceDrop()
    {
        _resourceHover = null;
        _preview.SetResourceDropCell(null);
    }

    private void ResourceDragDrop(object? sender, DragEventArgs e)
    {
        var hover = _resourceHover;
        bool valid = TryGetDropCell(hover, e, out var cell);
        ClearResourceDrop();
        e.Effect = DragDropEffects.None;
        if (!valid || hover is null)
            return;

        string beforeSources = SourceSignature();
        bool previewsCurrent = ReferenceEquals(_displayedDefinition, Definition) && _displayedSources == beforeSources;
        IDisposable? newPreview = null;
        bool committed = false;
        try
        {
            var reads = hover.Data.BeginRead();
            var resolver = new AuthoringResourceResolver(_packages, reads.Observe);
            if (hover.Data.Resolve(this, resolver) is not { } payload ||
                !TryGetDropCell(hover, e, out var selectedCell) || selectedCell != cell || SourceSignature() != beforeSources)
                return;
            var address = payload.Address;
            var assignment = ResolveDropAssignment(payload, resolver);
            bool needsSource = NeedsSource(address);
            if (!TryGetDropCell(hover, e, out var currentCell) || currentCell != cell || SourceSignature() != beforeSources)
                return;

            if (!needsSource && assignment.Matches(Document.FindTile(hover.Layer, cell.X, cell.Y)))
                return;

            if (previewsCurrent)
                newPreview = PrepareDropPreview(address.Resource, assignment);

            // Resolving an archive can ask for credentials and pump messages.
            // Recheck every dependency used by validation, along with the drop target.
            reads.Verify();
            if (!TryGetDropCell(hover, e, out currentCell) || currentCell != cell || SourceSignature() != beforeSources)
                return;

            Action<SceneLayerTileDefinition> apply = assignment.Animation is { } animation
                ? AnimationAssignment(animation.Key, assignment.Frame)
                : tile => SetFrame(tile, assignment.Frame);
            const string description = "Drop scene resource";
            using (var transaction = Document.History.BeginTransaction(description))
            {
                if (needsSource)
                    RegisterDropSource(address.Resource, assignment);
                Document.EditTile(hover.Layer, cell.X, cell.Y, description, apply);
                transaction.Commit();
            }

            committed = true;
            e.Effect = DragDropEffects.Copy;

            // The scheduled history refresh can reuse the previews that match this
            // source list. Only the dropped member needed a new preview.
            if (previewsCurrent && ReferenceEquals(Definition, hover.Scene))
            {
                var prepared = newPreview;
                newPreview = null;
                InstallDropPreview(prepared);
                _displayedSources = SourceSignature();
            }

            SetResourceDropError(null);
            SetSelectedTile(hover.Layer, cell.X, cell.Y);
            RefreshPreview();
            UpdateValidation();
            _preview.Focus();
        }
        catch (OperationCanceledException) when (!committed) { }
        catch (Exception ex) when (!committed && IsAuthoringException(ex))
        {
            SetResourceDropError(ex.Message);
        }
        finally
        {
            newPreview?.Dispose();
        }
    }

    private DropAssignment ResolveDropAssignment(IAuthoringResourceDrag payload, AuthoringResourceResolver resolver)
    {
        if (payload is TilesheetFrameDrag drag)
        {
            var region = resolver.ResolveTilesheetFrame(drag, out var definition);
            return new(new SceneFrameDefinition
            {
                Tilesheet = definition.Name,
                RegionName = region.Name,
                XTile = drag.X,
                YTile = drag.Y
            }, definition, null);
        }

        var animation = resolver.ResolveAnimation((AnimationDrag)payload, DeclaredTilesheet);
        return new(SceneAnimationSource.FirstPreviewFrame(animation)!, null, animation);
    }

    private bool NeedsSource(ResourceAddress address)
    {
        var existing = address.Location.AssetType == AssetTypes.TilesheetDefinition
            ? DeclaredTilesheet(address.Member!)
            : DeclaredAnimation(address.Member!);
        if (existing is not null && existing != address with { FieldPath = null })
            throw new InvalidDataException($"'{address.Member}' is already registered from a different source.");
        return existing is null;
    }

    private ResourceAddress? DeclaredTilesheet(string name) => DeclaredAddress(
        Definition.TilesheetSources.Where(source => source.Tilesheet == name), name,
        source => source.Kind switch
        {
            SceneTilesheetSourceKind.LooseDefinitionFile =>
                new AssetLocation(AssetTypes.TilesheetDefinition, Document.ResolveReferencePath(source.GtsPath!)),
            SceneTilesheetSourceKind.PackedDefinitionFile when !string.IsNullOrWhiteSpace(source.AssetEntryName) =>
                new AssetLocation(AssetTypes.TilesheetDefinition, Document.ResolveReferencePath(source.AssetsFilePath!), source.AssetEntryName),
            _ => throw new InvalidDataException($"'{name}' has an incomplete GTS source reference.")
        });

    private ResourceAddress? DeclaredAnimation(string key) => DeclaredAddress(
        Definition.AnimationSources.Where(source => string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase)), key,
        source => source.Kind switch
        {
            SceneAnimationSourceKind.LooseDefinitionFile =>
                new AssetLocation(AssetTypes.AnimationDefinition, Document.ResolveReferencePath(source.GaniPath!)),
            SceneAnimationSourceKind.PackedDefinitionFile when !string.IsNullOrWhiteSpace(source.AssetEntryName) =>
                new AssetLocation(AssetTypes.AnimationDefinition, Document.ResolveReferencePath(source.AssetsFilePath!), source.AssetEntryName),
            _ => throw new InvalidDataException($"'{key}' has an incomplete GANI source reference.")
        });

    private static ResourceAddress? DeclaredAddress<T>(IEnumerable<T> matches, string member, Func<T, AssetLocation> location)
    {
        using var sources = matches.GetEnumerator();
        if (!sources.MoveNext()) return null;
        var first = sources.Current;
        if (sources.MoveNext())
            throw new InvalidDataException($"Multiple sources are registered for '{member}'.");
        return new(location(first), member);
    }

    private void RegisterDropSource(AuthoringResource resource, DropAssignment assignment)
    {
        if (assignment.Animation is { } animation)
        {
            if (resource.EntryName is { } entry) Document.AddPackedAnimationSource(animation.Key, resource.Path, entry);
            else Document.SetLooseAnimationSource(animation.Key, resource.Path);
        }
        else if (resource.EntryName is { } entry)
            Document.AddPackedTilesheetSource(assignment.Frame.Tilesheet, resource.Path, entry);
        else
            Document.SetLooseTilesheetSource(assignment.Frame.Tilesheet, resource.Path);
    }

    private IDisposable? PrepareDropPreview(AuthoringResource resource, DropAssignment assignment)
    {
        if (assignment.Animation is { } animation)
        {
            var cached = FindAnimation(animation.Key);
            if (cached is null || cached.Diagnostics.Count != 0 ||
                AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [cached.Definition] }) !=
                AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [animation] }))
                return new SceneAnimationSource(resource, animation, _packages);
        }

        if (assignment.Tilesheet is { } sheet)
        {
            var cached = FindTilesheet(sheet.Name);
            if (cached?.Image is null ||
                TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [cached.Definition] }) !=
                TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [sheet] }))
                return new SceneTilesheetSource(resource, sheet, _packages);
        }

        return null;
    }

    private void InstallDropPreview(IDisposable? preview)
    {
        _restoringSourceSelection = true;
        try
        {
            if (preview is SceneTilesheetSource sheet)
            {
                var cached = FindTilesheet(sheet.Definition.Name);
                var selectedFrame = _sourceTree.SelectedNode?.Tag is FrameTag frame && ReferenceEquals(frame.Source, cached)
                    ? frame : (FrameTag?)null;
                if (cached is not null)
                {
                    _tilesheetSources.Remove(cached);
                    var oldNode = _sourceTree.Nodes.Cast<TreeNode>().FirstOrDefault(node => ReferenceEquals(node.Tag, cached));
                    oldNode?.Remove();
                    cached.Dispose();
                }

                _tilesheetSources.Add(sheet);
                var node = CreateTilesheetSourceNode(sheet);
                int index = _sourceTree.Nodes.Cast<TreeNode>().TakeWhile(existing =>
                    StringComparer.OrdinalIgnoreCase.Compare(((SceneTilesheetSource)existing.Tag!).Definition.Name, sheet.Definition.Name) <= 0).Count();
                _sourceTree.Nodes.Insert(index, node);
                node.Expand();
                if (selectedFrame is { } selection)
                    RestoreDropFrameSelection(node, selection);
                var declaration = Definition.TilesheetSources.FirstOrDefault(source => source.Tilesheet == sheet.Definition.Name);
                if (declaration is not null)
                    _declaredSourceDiagnostics.Remove(declaration);
            }
            else if (preview is SceneAnimationSource animation)
            {
                var cached = FindAnimation(animation.Definition.Key);
                bool wasSelected = _animationList.SelectedItems.Count == 1 &&
                    ReferenceEquals(_animationList.SelectedItems[0].Tag, cached);
                if (cached is not null)
                {
                    _animationSources.Remove(cached);
                    cached.Dispose();
                }

                _animationSources.Add(animation);
                RefreshAnimationList();
                if (wasSelected)
                    SelectAnimation(animation);
                var declaration = Definition.AnimationSources.FirstOrDefault(source =>
                    string.Equals(source.AnimationKey, animation.Definition.Key, StringComparison.OrdinalIgnoreCase));
                if (declaration is not null)
                    _declaredSourceDiagnostics.Remove(declaration);
            }
        }
        finally
        {
            _restoringSourceSelection = false;
            UpdatePaintSourceLabel();
        }
    }

    private void RestoreDropFrameSelection(TreeNode root, FrameTag selection)
    {
        var regionNode = root.Nodes.Cast<TreeNode>().FirstOrDefault(node => node.Tag is RegionTag tag &&
            string.Equals(tag.Region.Name, selection.Region.Name, StringComparison.OrdinalIgnoreCase));
        if (regionNode?.Tag is not RegionTag region)
            return;

        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region.Region);
        if (selection.X >= columns || selection.Y >= rows)
            return;

        regionNode.Expand();
        var rowNode = regionNode.Nodes[selection.Y];
        rowNode.Expand();
        _sourceTree.SelectedNode = rowNode.Nodes[selection.X];
        _sourceTree.SelectedNode.EnsureVisible();
    }

    private void SetResourceDropError(string? error)
    {
        if (IsDisposed || Disposing || _resourceDropError == error)
            return;

        _resourceDropError = error;
        UpdateValidation();
    }
}
