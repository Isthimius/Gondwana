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
    private sealed record ResourceHover(object Payload, ResourceAddress Address, SceneDefinition Scene,
        SceneLayerDefinition Layer, string? FilePath, AssetLocation? PackedLocation, (long Length, DateTime Modified) Stamp);

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

        _preview.AllowDrop = true;
        _preview.DragEnter += ResourceDragEnter;
        _preview.DragOver += ResourceDragOver;
        _preview.DragLeave += (_, _) => ClearResourceDrop();
        _preview.DragDrop += ResourceDragDrop;
    }

    private static object? GetResourcePayload(DragEventArgs e) =>
        (e.AllowedEffect & DragDropEffects.Copy) == 0 ? null : e.Data switch
        {
            ResourceDragData<TilesheetFrameDrag> frame => frame.Payload,
            ResourceDragData<AnimationDrag> animation => animation.Payload,
            _ => null
        };

    private void ResourceDragEnter(object? sender, DragEventArgs e)
    {
        ClearResourceDrop();
        e.Effect = DragDropEffects.None;
        if (_preview.HasGesture || _selectedLayer is not { } layer || GetResourcePayload(e) is not { } payload)
            return;

        try
        {
            var address = payload is TilesheetFrameDrag frame ? frame.Address : ((AnimationDrag)payload).Address;
            var type = payload is TilesheetFrameDrag ? AssetTypes.TilesheetDefinition : AssetTypes.AnimationDefinition;
            if (address.Location.AssetType != type || string.IsNullOrWhiteSpace(address.Member))
                throw new InvalidDataException("Drag a specific saved tilesheet frame or animation member.");

            // Hover checks identity and placement. Reading content on release keeps
            // image decoding and any archive authentication out of the OLE hover loop.
            NeedsSource(address);
            _resourceHover = new(payload, address, Definition, layer, Document.FilePath, Document.PackedLocation,
                SourceStamp(address.Location.Path));
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
        return hover is not null && ReferenceEquals(hover.Payload, GetResourcePayload(e)) &&
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
            CheckSourceStamp(hover);
            var assignment = ResolveDropAssignment(hover);
            bool needsSource = NeedsSource(hover.Address);
            if (!TryGetDropCell(hover, e, out var currentCell) || currentCell != cell || SourceSignature() != beforeSources)
                return;

            if (!needsSource && assignment.Matches(Document.FindTile(hover.Layer, cell.X, cell.Y)))
                return;

            if (previewsCurrent)
                newPreview = PrepareDropPreview(hover.Address.Resource, assignment);

            // Resolving an archive can ask for credentials and pump messages.
            // Check context and source declarations again before recording anything.
            CheckSourceStamp(hover);
            if (!TryGetDropCell(hover, e, out currentCell) || currentCell != cell || SourceSignature() != beforeSources)
                return;

            Action<SceneLayerTileDefinition> apply = assignment.Animation is { } animation
                ? AnimationAssignment(animation.Key, assignment.Frame)
                : tile => SetFrame(tile, assignment.Frame);
            const string description = "Drop scene resource";
            using (var transaction = Document.History.BeginTransaction(description))
            {
                if (needsSource)
                    RegisterDropSource(hover.Address.Resource, assignment);
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
        catch (Exception ex) when (!committed && IsAuthoringException(ex))
        {
            SetResourceDropError(ex.Message);
        }
        finally
        {
            newPreview?.Dispose();
        }
    }

    private DropAssignment ResolveDropAssignment(ResourceHover hover)
    {
        var resolver = new AuthoringResourceResolver(_packages);
        if (hover.Payload is TilesheetFrameDrag drag)
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

        var resource = hover.Address.Resource;
        if (resource.EntryName is null && !string.Equals(Path.GetExtension(resource.Path), ".gani", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A loose animation source must be a .gani file.");

        var animation = resolver.LoadAnimation(resource, hover.Address.Member);
        var errors = AnimationDefinitionValidator.Validate(animation);
        if (errors.Count != 0)
            throw new InvalidDataException($"Animation '{animation.Key}': {errors[0]}");

        foreach (var frames in animation.Frames.GroupBy(frame => frame.Tilesheet, StringComparer.Ordinal))
        {
            var declared = animation.TilesheetSources.FirstOrDefault(source => source.Tilesheet == frames.Key);
            var sheet = declared is null
                ? DeclaredTilesheet(frames.Key) ?? throw new InvalidDataException($"Animation '{animation.Key}' needs a saved GTS source for '{frames.Key}'.")
                : new ResourceAddress(AuthoringResourceResolver.TilesheetSource(declared, resource.BaseDirectory), frames.Key);
            var definition = resolver.LoadValidatedTilesheet(sheet);
            foreach (var frame in frames)
                AuthoringResourceResolver.ResolveTilesheetFrame(definition, frame.RegionName, frame.XTile, frame.YTile);
        }

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
            if (cached is null ||
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
            }
        }
        finally
        {
            _restoringSourceSelection = false;
            UpdatePaintSourceLabel();
        }
    }

    private static (long Length, DateTime Modified) SourceStamp(string path)
    {
        var info = new FileInfo(path);
        return (info.Length, info.LastWriteTimeUtc);
    }

    private static void CheckSourceStamp(ResourceHover hover)
    {
        if (SourceStamp(hover.Address.Location.Path) != hover.Stamp)
            throw new InvalidDataException("The saved source changed during the drag. Start a new drag.");
    }

    private void SetResourceDropError(string? error)
    {
        if (IsDisposed || Disposing || _resourceDropError == error)
            return;

        _resourceDropError = error;
        UpdateValidation();
    }
}
