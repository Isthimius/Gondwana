using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Sprites.WinForms;

public sealed partial class SpriteEditorControl
{
    private sealed record ResourceHover(ResourceDragData Data, Control Surface, SpriteDefinition Definition,
        SpriteInstanceDefinition? PreviewSprite, string? FilePath, AssetLocation? PackedLocation);

    private ResourceHover? _resourceHover;
    private string? _resourceDropError;

    private void WireResourceDragDrop()
    {
        _frames.ItemDrag += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Item is TreeNode { Tag: SpriteFrameDefinition frame } node &&
                node.Parent?.Parent?.Tag is SpriteTilesheetSource source)
            {
                var payload = new TilesheetFrameDrag(new ResourceAddress(source.Resource, source.Definition.Name),
                    frame.RegionName, frame.XTile, frame.YTile);
                _frames.DoDragDrop(new ResourceDragData<TilesheetFrameDrag>(payload), DragDropEffects.Copy);
            }
        };

        _scenes.ItemDrag += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Item is TreeNode { Tag: ValueTuple<SceneDefinition, SceneLayerDefinition> layer } node &&
                node.Parent?.Tag is ResourceAddress address)
                _scenes.DoDragDrop(new ResourceDragData<SceneLayerDrag>(new(address, layer.Item2.ID)), DragDropEffects.Copy);
        };

        foreach (var surface in new Control[] { _sprites, _preview.DropSurface })
        {
            surface.AllowDrop = true;
            surface.DragEnter += ResourceDragEnter;
            surface.DragOver += ResourceDragOver;
            surface.DragLeave += (_, _) => ClearResourceDrop();
            surface.DragDrop += ResourceDragDrop;
        }
    }

    private static ResourceDragData? ResourceData(DragEventArgs e) =>
        (e.AllowedEffect & DragDropEffects.Copy) == 0 ? null : e.Data switch
        {
            ResourceDragData<TilesheetFrameDrag> frame when frame.Address.Location.AssetType == AssetTypes.TilesheetDefinition => frame,
            ResourceDragData<AnimationDrag> animation when animation.Address.Location.AssetType == AssetTypes.AnimationDefinition => animation,
            ResourceDragData<SceneLayerDrag> scene when scene.Address.Location.AssetType == AssetTypes.SceneDefinition => scene,
            _ => null
        };

    private void ResourceDragEnter(object? sender, DragEventArgs e)
    {
        ClearResourceDrop();
        e.Effect = DragDropEffects.None;
        if (sender is not Control surface || ResourceData(e) is not { } data)
            return;

        try
        {
            data.BeginRead();
            if (data.Address.Member is not null)
                NeedsDropSource(data.Address);
            SetResourceDropError(null);
            _resourceHover = new(data, surface, Document.Definition, SelectedSprite, Document.FilePath, Document.PackedLocation);
            ResourceDragOver(sender, e);
        }
        catch (Exception ex) when (IsDropException(ex))
        {
            SetResourceDropError(ex.Message);
        }
    }

    private SpriteInstanceDefinition? DropTarget(ResourceHover? hover, object? sender, DragEventArgs e)
    {
        if (hover is null || !ReferenceEquals(hover.Data, ResourceData(e)) || !ReferenceEquals(sender, hover.Surface) ||
            IsDisposed || Disposing || !Enabled || !hover.Surface.Visible || Document.History.InTransaction || Document.History.IsExecuting ||
            !ReferenceEquals(Document.Definition, hover.Definition) || Document.FilePath != hover.FilePath ||
            !ReferenceEquals(Document.PackedLocation, hover.PackedLocation))
            return null;

        var point = hover.Surface.PointToClient(new Point(e.X, e.Y));
        if (!hover.Surface.ClientRectangle.Contains(point))
            return null;

        SpriteInstanceDefinition? sprite;
        if (ReferenceEquals(hover.Surface, _sprites))
        {
            int index = _sprites.DropRow(point);
            sprite = index < 0 ? null : _sprites.Items[index] as SpriteInstanceDefinition;
        }
        else
            sprite = ReferenceEquals(SelectedSprite, hover.PreviewSprite) ? hover.PreviewSprite : null;

        return sprite is not null && hover.Definition.Sprites.Contains(sprite) ? sprite : null;
    }

    private void ResourceDragOver(object? sender, DragEventArgs e)
    {
        var sprite = DropTarget(_resourceHover, sender, e);
        _sprites.DropIndex = sprite is null ? -1 : _sprites.Items.IndexOf(sprite);
        _preview.ShowDropTarget(sprite is not null && ReferenceEquals(sender, _preview.DropSurface)
            ? string.IsNullOrWhiteSpace(sprite.Nickname) ? $"sprite {_sprites.Items.IndexOf(sprite) + 1}" : sprite.Nickname : null);
        e.Effect = sprite is null ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private void ClearResourceDrop()
    {
        _resourceHover = null;
        if (IsDisposed || Disposing)
            return;

        _sprites.DropIndex = -1;
        _preview.ShowDropTarget(null);
    }

    private void ResourceDragDrop(object? sender, DragEventArgs e)
    {
        var hover = _resourceHover;
        var sprite = DropTarget(hover, sender, e);
        ClearResourceDrop();
        e.Effect = DragDropEffects.None;
        if (hover is null || sprite is null)
            return;

        var sourcesBefore = CurrentSources();
        string sourceSignature = DropSourceSignature();
        bool previewsCurrent = ReferenceEquals(_historyDefinition, Document.Definition) && _historySources.SequenceEqual(sourcesBefore);
        var preparedSheets = new List<SpriteTilesheetSource>();
        var animationSheets = new List<SpriteTilesheetSource>();
        bool committed = false;
        try
        {
            var reads = hover.Data.BeginRead();
            if (hover.Data.Resolve(this) is not { } payload || !SameTarget())
                return;

            var address = payload.Address;
            var resolver = new AuthoringResourceResolver(_packages, reads.Observe);
            bool needsSource = NeedsDropSource(address);
            var validatedSheets = new List<(ResourceAddress Address, TilesheetDefinition Sheet)>();
            SpriteFrameDefinition? frame = null;
            AnimationDefinition? animation = null;
            SceneDefinition? scene = null;
            bool changes;
            switch (payload)
            {
                case TilesheetFrameDrag drag:
                    var region = resolver.ResolveTilesheetFrame(drag, out var sheet);
                    validatedSheets.Add((address, sheet));
                    frame = new SpriteFrameDefinition { Tilesheet = sheet.Name, RegionName = region.Name, XTile = drag.X, YTile = drag.Y };
                    changes = !SpriteDocument.FramesMatch(sprite.Frame, frame);
                    break;
                case AnimationDrag drag:
                    animation = resolver.ResolveAnimation(drag, DeclaredTilesheet, (sheetAddress, definition) =>
                        validatedSheets.Add((sheetAddress, definition)));
                    changes = sprite.AnimationKey != animation.Key;
                    break;
                case SceneLayerDrag drag:
                    scene = resolver.ResolveSceneLayer(drag);
                    changes = sprite.SceneId != scene.ID || sprite.SceneLayerId != drag.LayerId;
                    break;
                default:
                    return;
            }

            if (!SameTarget() || (!changes && !needsSource))
                return;

            // Preview construction can read images too. Prepare it before the final
            // validation so callbacks cannot invalidate an already committed edit.
            bool replaceAnimation = false;
            if (previewsCurrent)
            {
                var ownedSheets = animation is null ? [] : validatedSheets.Where(item =>
                    animation.TilesheetSources.Any(source => source.Tilesheet == item.Sheet.Name)).ToArray();
                replaceAnimation = animation is not null && (NeedsAnimationPreview(animation) ||
                    ownedSheets.Any(item => !_animationTilesheets[animation.Key].Any(source => MatchesPreview(source, item.Address, item.Sheet))));
                if (replaceAnimation)
                    foreach (var (sheetAddress, definition) in ownedSheets)
                        animationSheets.Add(new SpriteTilesheetSource(sheetAddress.Resource, definition, _packages));

                foreach (var (sheetAddress, definition) in validatedSheets.Except(ownedSheets))
                    if (!_sources.Any(source => MatchesPreview(source, sheetAddress, definition)))
                        preparedSheets.Add(new SpriteTilesheetSource(sheetAddress.Resource, definition, _packages));
            }

            reads.Verify();
            if (!SameTarget())
                return;
            // Source declarations can be edited in place while a password or selector is open.
            if (NeedsDropSource(address) != needsSource)
                return;

            using (var edit = Document.History.BeginTransaction("Drop sprite resource"))
            {
                if (needsSource)
                    RegisterDropSource(address);
                if (frame is not null)
                    Document.AssignFrame(sprite, frame);
                else if (animation is not null)
                    Document.AssignAnimation(sprite, animation.Key);
                else if (scene is not null && payload is SceneLayerDrag layer)
                    Document.AssignSceneLayer(sprite, scene.ID, layer.LayerId);
                edit.Commit();
            }

            committed = true;
            e.Effect = DragDropEffects.Copy;
            if (previewsCurrent && ReferenceEquals(Document.Definition, hover.Definition))
            {
                foreach (var sheet in preparedSheets)
                    InstallTilesheetPreview(sheet);
                preparedSheets.Clear();
                if (animation is not null && replaceAnimation)
                {
                    InstallAnimationPreview(animation, address.Resource, animationSheets);
                    animationSheets = [];
                }
                if (scene is not null)
                    InstallScenePreview(scene, address.Resource);
                RememberDisplayedSources();
            }

            _sprites.SelectedItem = sprite;
            RefreshSelection();
            _sprites.Focus();
        }
        catch (OperationCanceledException) when (!committed) { }
        catch (Exception ex) when (!committed && IsDropException(ex))
        {
            SetResourceDropError(ex.Message);
        }
        finally
        {
            foreach (var source in preparedSheets.Concat(animationSheets))
                source.Dispose();
        }

        bool SameTarget() => ReferenceEquals(DropTarget(hover, sender, e), sprite) &&
            CurrentSources().SequenceEqual(sourcesBefore) && DropSourceSignature() == sourceSignature;
    }

    private string DropSourceSignature()
    {
        var definition = Document.Definition;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { definition.TilesheetSources, definition.AnimationSources, definition.SceneSources });
    }

    private bool NeedsDropSource(ResourceAddress address)
    {
        var member = address.Member!;
        var existing = address.Location.AssetType switch
        {
            AssetTypes.TilesheetDefinition => DeclaredTilesheet(member),
            AssetTypes.AnimationDefinition => SingleDeclaredAddress(member, AssetTypes.AnimationDefinition,
                Document.Definition.AnimationSources.Where(source => string.Equals(source.AnimationKey, member, StringComparison.OrdinalIgnoreCase))
                    .Select(source => (source.Kind == SpriteAnimationSourceKind.LooseDefinitionFile, source.GaniPath, source.AssetsFilePath, source.AssetEntryName))),
            AssetTypes.SceneDefinition => SingleDeclaredAddress(member, AssetTypes.SceneDefinition,
                Document.Definition.SceneSources.Where(source => source.SceneId == member)
                    .Select(source => (source.Kind == SpriteSceneSourceKind.LooseDefinitionFile, source.GscnPath, source.AssetsFilePath, source.AssetEntryName))),
            _ => throw new InvalidDataException("This resource cannot be assigned to a sprite.")
        };
        if (existing is not null && existing != address with { FieldPath = null })
            throw new InvalidDataException($"'{member}' is already registered from a different source.");

        return existing is null;
    }

    private ResourceAddress? DeclaredTilesheet(string name) => SingleDeclaredAddress(name, AssetTypes.TilesheetDefinition,
        Document.Definition.TilesheetSources.Where(source => source.Tilesheet == name)
            .Select(source => (source.Kind == SpriteTilesheetSourceKind.LooseDefinitionFile, source.GtsPath, source.AssetsFilePath, source.AssetEntryName)));

    private ResourceAddress? SingleDeclaredAddress(string member, AssetTypes type,
        IEnumerable<(bool Loose, string? Path, string? Archive, string? Entry)> matches)
    {
        using var sources = matches.GetEnumerator();
        if (!sources.MoveNext())
            return null;

        var (loose, path, archive, entry) = sources.Current;
        if (sources.MoveNext())
            throw new InvalidDataException($"Multiple sources are registered for '{member}'.");
        if (string.IsNullOrWhiteSpace(loose ? path : archive) || (!loose && string.IsNullOrWhiteSpace(entry)))
            throw new InvalidDataException($"'{member}' has an incomplete source reference.");

        return new(new AssetLocation(type, Document.ResolveReferencePath((loose ? path : archive)!), loose ? null : entry), member);
    }

    private void RegisterDropSource(ResourceAddress address)
    {
        string member = address.Member!;
        var resource = address.Resource;
        switch (resource.AssetType)
        {
            case AssetTypes.TilesheetDefinition:
                if (resource.EntryName is { } gts) Document.AddPackedTilesheetSource(member, resource.Path, gts);
                else Document.SetLooseTilesheetSource(member, resource.Path);
                break;
            case AssetTypes.AnimationDefinition:
                if (resource.EntryName is { } gani) Document.AddPackedAnimationSource(member, resource.Path, gani);
                else Document.SetLooseAnimationSource(member, resource.Path);
                break;
            case AssetTypes.SceneDefinition:
                if (resource.EntryName is { } gscn) Document.AddPackedSceneSource(member, resource.Path, gscn);
                else Document.SetLooseSceneSource(member, resource.Path);
                break;
        }
    }

    private bool NeedsAnimationPreview(AnimationDefinition animation)
    {
        var cached = _animationDefinitions.FirstOrDefault(item => string.Equals(item.Key, animation.Key, StringComparison.OrdinalIgnoreCase));
        return cached is null || _sourceLoadWarnings.ContainsKey(cached) || !_animationTilesheets.ContainsKey(animation.Key) ||
            AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [cached] }) !=
            AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [animation] });
    }

    private static bool MatchesPreview(SpriteTilesheetSource cached, ResourceAddress address, TilesheetDefinition sheet) =>
        cached.Image is not null && new ResourceAddress(cached.Resource, cached.Definition.Name) == address with { FieldPath = null } &&
        TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [cached.Definition] }) ==
        TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [sheet] });

    private void InstallTilesheetPreview(SpriteTilesheetSource sheet)
    {
        ClearLoadWarning(Document.Definition.TilesheetSources.FirstOrDefault(item => item.Tilesheet == sheet.Definition.Name));
        if (sheet.Resource.EntryName is null)
            _loadedTilesheetPaths.Add(sheet.FilePath + " :: " + sheet.Definition.Name);

        var previous = _sources.Where(source => source.Definition.Name == sheet.Definition.Name).ToArray();
        if (previous.Length == 0)
        {
            AddTilesheetDefinition(sheet);
            return;
        }

        var selected = _frames.SelectedNode?.Tag as SpriteFrameDefinition;
        foreach (var source in previous)
        {
            _sources.Remove(source);
            source.Dispose();
        }
        _sources.Add(sheet);
        _frames.BeginUpdate();
        try
        {
            _frames.Nodes.Clear();
            _thumbnails.Images.Clear();
            foreach (var source in _sources)
                AddTilesheetNode(source);
            if (selected is not null)
                _frames.SelectedNode = _frames.Nodes.Cast<TreeNode>()
                    .SelectMany(root => root.Nodes.Cast<TreeNode>())
                    .SelectMany(region => region.Nodes.Cast<TreeNode>())
                    .FirstOrDefault(node => node.Tag is SpriteFrameDefinition frame && SpriteDocument.FramesMatch(frame, selected));
        }
        finally { _frames.EndUpdate(); }
    }

    private void InstallAnimationPreview(AnimationDefinition animation, AuthoringResource resource, List<SpriteTilesheetSource> sheets)
    {
        if (resource.EntryName is null)
            _loadedAnimationPaths.Add(resource.Path + " :: " + animation.Key);

        ClearLoadWarning(Document.Definition.AnimationSources.FirstOrDefault(item =>
            string.Equals(item.AnimationKey, animation.Key, StringComparison.OrdinalIgnoreCase)));
        foreach (var cached in _animationDefinitions.Where(item => string.Equals(item.Key, animation.Key, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _sourceLoadWarnings.Remove(cached);
            _animationDefinitions.Remove(cached);
        }
        if (_animationTilesheets.Remove(animation.Key, out var previous))
            foreach (var sheet in previous) sheet.Dispose();
        _animationDefinitions.Add(animation);
        _animationTilesheets.Add(animation.Key, sheets);
    }

    private void InstallScenePreview(SceneDefinition scene, AuthoringResource resource)
    {
        ClearLoadWarning(Document.Definition.SceneSources.FirstOrDefault(item => item.SceneId == scene.ID));
        if (resource.EntryName is null)
            _loadedScenePaths.Add(resource.Path + " :: " + scene.ID);

        var previous = _sceneDefinitions.FirstOrDefault(item => item.ID == scene.ID);
        if (previous is not null &&
            SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = [previous] }) ==
            SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = [scene] }))
            return;

        _sceneDefinitions.RemoveAll(item => item.ID == scene.ID);
        foreach (var node in _scenes.Nodes.Cast<TreeNode>().Where(node =>
            node.Tag is ResourceAddress address && address.Member == scene.ID).ToArray())
            node.Remove();
        AddSceneDefinition(scene, resource);
    }

    private void ClearLoadWarning(object? source)
    {
        if (source is not null)
            _sourceLoadWarnings.Remove(source);
    }

    private void SetResourceDropError(string? message)
    {
        if (IsDisposed || Disposing || _resourceDropError == message)
            return;

        _resourceDropError = message;
        UpdateValidation();
    }

    private static bool IsDropException(Exception ex) => ex is IOException or InvalidDataException or ArgumentException
        or UnauthorizedAccessException or InvalidOperationException or NotSupportedException;
}
