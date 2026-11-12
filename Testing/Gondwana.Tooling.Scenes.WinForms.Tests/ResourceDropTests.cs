using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    public static IEnumerable<object[]> DropProjections =>
        Enum.GetValues<CoordinateSystemTypes>().Select(projection => new object[] { projection });

    [Theory]
    [MemberData(nameof(DropProjections))]
    public void ResourceDropUsesSelectedProjectionLayerOriginZoomAndScroll(CoordinateSystemTypes projection) =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            layer.CoordinateSystemType = projection;
            layer.Columns = 80;
            layer.Rows = 80;
            layer.OriginPx = new(37, -19);
            var upper = document.AddLayer();
            upper.ZOrder = 100;
            Application.DoEvents();
            SelectDropLayer(editor, layer);
            document.History.ResetSaved();
            var (frame, _) = CreateDropResources(root, packed: false);
            Tool(editor, "Fill").PerformClick();
            preview.SetZoom(2f);
            preview.AutoScrollPosition = new(100, 75);

            var data = new ResourceDragData<TilesheetFrameDrag>(frame);
            var point = PreviewCellPoint(preview, layer, 5, 6);
            if (!preview.ClientRectangle.Contains(point))
            {
                var scroll = preview.AutoScrollPosition;
                preview.AutoScrollPosition = new(
                    Math.Max(0, point.X - scroll.X - preview.ClientSize.Width / 2),
                    Math.Max(0, point.Y - scroll.Y - preview.ClientSize.Height / 2));
                point = PreviewCellPoint(preview, layer, 5, 6);
            }
            Assert.True(preview.ClientRectangle.Contains(point));
            Assert.NotEqual(Point.Empty, preview.AutoScrollPosition);
            var args = SceneDragAt(preview, data, point);
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal(new Point(5, 6), preview.ResourceDropCell);
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);
            Assert.False(preview.HasGesture);
            var outside = SceneDragAt(preview, data, new Point(-1, -1));
            RaiseSceneDrag(preview, "Over", outside);
            Assert.Equal(DragDropEffects.None, outside.Effect);
            Assert.Null(preview.ResourceDropCell);
            RaiseSceneDrag(preview, "Over", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Null(preview.ResourceDropCell);
            Assert.Equal(SceneEditorTool.Fill, editor.ActiveTool);
            Assert.Equal(new Rectangle(5, 6, 1, 1), editor.SelectedCells);
            var tile = Assert.Single(layer.Tiles);
            Assert.Equal((5, 6), (tile.X, tile.Y));
            Assert.Equal(("terrain", "detail", 1, 1), FrameValues(tile));
            Assert.Empty(upper.Tiles);
            Assert.False(preview.HasGesture);
            document.History.Undo();
            Assert.Empty(layer.Tiles);
            Assert.Empty(document.Definition.TilesheetSources);
            Assert.False(document.History.CanUndo);
            Assert.False(document.IsDirty);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DropPreservesTileMetadataAndReplaysExactSourceAcrossSaveAs(bool packed, bool animation) =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var (frame, cycle) = CreateDropResources(root, packed);
            var document = editor.Document;
            var original = document.Definition;
            var other = document.AddDefinition();
            var layer = document.AddLayer();
            editor.SelectDefinition(original);
            editor.SelectDefinition(other);
            Application.DoEvents();
            SelectDropLayer(editor, layer);

            var tile = new SceneLayerTileDefinition
            {
                X = 1,
                Y = 2,
                Id = Guid.NewGuid(),
                Nickname = "torch",
                Visible = false,
                CollisionType = TileCollisionType.Blocking,
                EnableFog = true,
                AdjustCollisionArea = new CollisionAdjust(1, 2, 3, 4),
                Transform = TileTransform.FlipHorizontal
            };
            layer.Tiles.Add(tile);
            document.History.ResetSaved();
            var data = animation ? (IDataObject)new ResourceDragData<AnimationDrag>(cycle) : new ResourceDragData<TilesheetFrameDrag>(frame);
            var args = SceneDragAt(preview, data, PreviewCellPoint(preview, layer, 1, 2));
            if (packed)
            {
                var address = animation ? cycle.Address : frame.Address;
                var basename = new ResourceAddress(new AssetLocation(address.Location.AssetType, address.Location.Path,
                    Path.GetFileName(address.Location.AssetEntryName!)), address.Member);
                var incomplete = animation
                    ? (IDataObject)new ResourceDragData<AnimationDrag>(new(basename))
                    : new ResourceDragData<TilesheetFrameDrag>(frame with { Address = basename });
                var rejected = SceneDragAt(preview, incomplete, PreviewCellPoint(preview, layer, 1, 2));
                RaiseSceneDrag(preview, "Enter", rejected);
                RaiseSceneDrag(preview, "Drop", rejected);
                Assert.Equal(DragDropEffects.None, rejected.Effect);
                Assert.False(document.IsDirty);
            }

            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Null(tile.Frame);
            Assert.False(document.IsDirty);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Same(tile, Assert.Single(layer.Tiles));
            Assert.Equal(("terrain", "detail", 1, 1), FrameValues(tile));
            Assert.Equal(animation ? "water.flow" : null, tile.AnimationKey);
            Assert.Equal(animation, tile.EnableAnimator);
            Assert.Equal(animation, tile.StartAnimation);
            Assert.Equal("torch", tile.Nickname);
            Assert.NotEqual(Guid.Empty, tile.Id);
            Assert.False(tile.Visible);
            Assert.True(tile.EnableFog);
            Assert.Equal(TileTransform.FlipHorizontal, tile.Transform);
            Assert.Equal(TileCollisionType.Blocking, tile.CollisionType);
            Assert.Equal(new CollisionAdjust(1, 2, 3, 4), tile.AdjustCollisionArea);
            Assert.Empty(original.Layers[0].Tiles);
            Assert.Empty(original.AnimationSources);
            Assert.Empty(original.TilesheetSources);
            ResourceAddress expected = animation ? cycle.Address : frame.Address;
            Assert.Equal(expected, DropSourceAddress(document, animation));
            Assert.True(document.IsDirty);

            var shortcut = typeof(SceneEditorControl).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)shortcut.Invoke(editor, [new Message(), Keys.Control | Keys.Z])!);
            Assert.Null(tile.Frame);
            Assert.Null(tile.AnimationKey);
            Assert.Empty(other.TilesheetSources);
            Assert.Empty(other.AnimationSources);
            Assert.False(document.History.CanUndo);
            Assert.False(document.IsDirty);

            string saveAs = Path.Combine(root, "exports", "dropped.gscn");
            document.Save(saveAs);
            Assert.True((bool)shortcut.Invoke(editor, [new Message(), Keys.Control | Keys.Y])!);
            Assert.Equal(expected, DropSourceAddress(document, animation));
            Assert.True(document.IsDirty);
            document.Save(saveAs);
            Application.DoEvents();

            // Repeating an identical assignment retains its source, preview and save point.
            var sourcePane = animation
                ? (Control)Descendants(editor).OfType<ListView>().Single(list => list.Columns.Count > 0 && list.Columns[0].Text == "Animation key")
                : Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes.Count > 0 && tree.Nodes[0].Tag is SceneTilesheetSource);
            object? previewSource = sourcePane is ListView animations ? animations.Items[0].Tag : ((TreeView)sourcePane).Nodes[0].Tag;
            args = SceneDragAt(preview, data, PreviewCellPoint(preview, layer, 1, 2));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.False(document.IsDirty);
            Assert.Same(previewSource, sourcePane is ListView list ? list.Items[0].Tag : ((TreeView)sourcePane).Nodes[0].Tag);

            var next = SceneDragAt(preview, data, PreviewCellPoint(preview, layer, 2, 2));
            RaiseSceneDrag(preview, "Enter", next);
            RaiseSceneDrag(preview, "Drop", next);
            Assert.Equal(DragDropEffects.Copy, next.Effect);
            Assert.Equal(expected, DropSourceAddress(document, animation));
            Assert.Same(previewSource, sourcePane is ListView reused ? reused.Items[0].Tag : ((TreeView)sourcePane).Nodes[0].Tag);
            document.History.Undo();
            Assert.Single(layer.Tiles);
            Assert.Equal(expected, DropSourceAddress(document, animation));
            Assert.False(document.IsDirty);

            document.History.Undo();
            Assert.Null(tile.Frame);
            Assert.True(document.IsDirty);
            document.History.Redo();
            Assert.False(document.IsDirty);
            using var reloaded = SceneDocument.Open(saveAs);
            reloaded.SelectDefinition(reloaded.Definitions[1]);
            Assert.Equal(expected, DropSourceAddress(reloaded, animation));
            Assert.Equal(("terrain", "detail", 1, 1), FrameValues(Assert.Single(reloaded.Definition.Layers[0].Tiles)));
        });

    [Fact]
    public void RejectedAndCanceledDragsKeepHistoryAndNeverRetarget() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var (frame, cycle) = CreateDropResources(root, packed: false);
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            var upper = document.AddLayer();
            Application.DoEvents();
            SelectDropLayer(editor, layer);
            document.SetLooseTilesheetSource("terrain", frame.Address.Location.Path);
            document.SetLooseAnimationSource("water.flow", cycle.Address.Location.Path);
            document.History.ResetSaved();
            document.EditTile(layer, 1, 1, "Pending redo", tile => tile.Nickname = "redo");
            document.History.Undo();
            Application.DoEvents();

            var data = new ResourceDragData<TilesheetFrameDrag>(frame);
            var cell = PreviewCellPoint(preview, layer, 1, 1);
            var args = SceneDragAt(preview, data, cell);
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Leave", EventArgs.Empty);
            Assert.Null(preview.ResourceDropCell);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);

            RaiseSceneDrag(preview, "Enter", args);
            SelectDropLayer(editor, upper);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            SelectDropLayer(editor, layer);
            var structure = Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes[0].Tag is SceneDefinition);
            structure.SelectedNode!.Checked = false;
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            structure.SelectedNode.Checked = true;

            var (differentFrame, differentCycle) = CreateDropResources(Path.Combine(root, "another"), packed: false);
            foreach (IDataObject invalid in new IDataObject[]
            {
                new ResourceDragData<TilesheetFrameDrag>(differentFrame),
                new ResourceDragData<AnimationDrag>(differentCycle),
                new ResourceDragData<TilesheetFrameDrag>(frame with { X = 999 }),
                new ResourceDragData<AnimationDrag>(cycle with { Address = cycle.Address with { Member = "missing" } }),
                new ResourceDragData<AnimationDrag>(cycle with { Address = new ResourceAddress(frame.Address.Location, "terrain") }),
                new DataObject(DataFormats.Text, frame.Address.ToString())
            })
            {
                var invalidDrop = SceneDragAt(preview, invalid, cell);
                RaiseSceneDrag(preview, "Enter", invalidDrop);
                RaiseSceneDrag(preview, "Drop", invalidDrop);
                Assert.Equal(DragDropEffects.None, invalidDrop.Effect);
                Assert.Null(preview.ResourceDropCell);
            }

            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            File.AppendAllText(frame.Address.Location.Path, Environment.NewLine);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            args = SceneDragAt(preview, new ResourceDragData<TilesheetFrameDrag>(frame), cell);
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            File.Delete(frame.Address.Location.Path);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);

            Assert.Empty(layer.Tiles);
            Assert.Empty(upper.Tiles);
            Assert.Single(document.Definition.AnimationSources);
            Assert.Single(document.Definition.TilesheetSources);
            Assert.False(document.IsDirty);
            Assert.False(document.History.CanUndo);
            Assert.True(document.History.CanRedo);
        });

    [Fact]
    public void AnimationDropValidatesEveryFrameAndCanUseAnExplicitSceneTilesheetSource() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var (frame, cycle) = CreateDropResources(root, packed: false);
            var document = editor.Document;
            var layer = Assert.Single(document.Definition.Layers);
            string path = cycle.Address.Location.Path;
            var animation = AnimationDefinitionSerializer.Load(path, cycle.Address.Member);
            animation.TilesheetSources.Clear();
            AnimationDefinitionSerializer.SaveAll(path, [animation]);
            document.History.ResetSaved();
            var args = SceneDragAt(preview, new ResourceDragData<AnimationDrag>(cycle), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);

            document.SetLooseTilesheetSource("terrain", frame.Address.Location.Path);
            Application.DoEvents();
            document.History.ResetSaved();
            animation.Frames.Add(new() { Tilesheet = "terrain", RegionName = "detail", XTile = 99 });
            AnimationDefinitionSerializer.SaveAll(path, [animation]);
            args = SceneDragAt(preview, new ResourceDragData<AnimationDrag>(cycle), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Empty(document.Definition.AnimationSources);
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);

            animation.Frames.RemoveAt(1);
            AnimationDefinitionSerializer.SaveAll(path, [animation]);
            args = SceneDragAt(preview, new ResourceDragData<AnimationDrag>(cycle), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal("water.flow", Assert.Single(layer.Tiles).AnimationKey);
            Assert.Single(document.Definition.TilesheetSources);
            Assert.Single(document.Definition.AnimationSources);
            document.History.Undo();
            Assert.Single(document.Definition.TilesheetSources);
            Assert.Empty(document.Definition.AnimationSources);
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public void DropDoesNotCommitAnExistingStrokeOrRetargetToAnotherMember() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document;
            var first = document.Definition;
            var layer = first.Layers[0];
            var second = document.AddDefinition();
            var otherLayer = document.AddLayer();
            editor.SelectDefinition(first);
            Application.DoEvents();
            SelectDropLayer(editor, layer);
            SelectPaintFrame(source);
            Tool(editor, "Paint").PerformClick();
            document.History.ResetSaved();

            var (frame, _) = CreateDropResources(Path.Combine(root, "new-source"), packed: false);
            var args = SceneDragAt(preview, new ResourceDragData<TilesheetFrameDrag>(frame), PreviewCellPoint(preview, layer, 2, 2));
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Assert.True(preview.HasGesture);
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.True(preview.HasGesture);
            preview.CancelGesture();
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);

            var registered = new ResourceDragData<TilesheetFrameDrag>(new(new ResourceAddress(
                new AssetLocation(AssetTypes.TilesheetDefinition, Path.Combine(root, "terrain.gts")), "terrain"), "detail", 1, 1));
            args = SceneDragAt(preview, registered, PreviewCellPoint(preview, layer, 2, 2));
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            editor.SelectDefinition(second);
            Application.DoEvents();
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Empty(layer.Tiles);
            Assert.Empty(otherLayer.Tiles);
            Assert.Empty(second.TilesheetSources);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public void DeclinedArchivePasswordLeavesTheSceneAndHistoryUntouched() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var (frame, _) = CreateDropResources(root, packed: true, password: "locked");
            var document = editor.Document;
            var layer = Assert.Single(document.Definition.Layers);
            var catalog = (AssetPackageCatalog)typeof(SceneEditorControl)
                .GetField("_packages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
            int passwordRequests = 0;
            catalog.PasswordProvider = _ => { passwordRequests++; return null; };
            document.History.ResetSaved();
            var args = SceneDragAt(preview, new ResourceDragData<TilesheetFrameDrag>(frame), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(0, passwordRequests);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(1, passwordRequests);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Null(preview.ResourceDropCell);
            Assert.Empty(layer.Tiles);
            Assert.Empty(document.Definition.TilesheetSources);
            Assert.False(document.IsDirty);
            Assert.False(document.History.CanUndo);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnimationDropRechecksEarlierDependenciesAfterPasswordPrompt(bool removeImage) =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            const string password = "locked";
            var (looseFrame, cycle) = CreateDropResources(root, packed: false);
            var (packedFrame, _) = CreateDropResources(Path.Combine(root, "packed"), packed: true, password: password);
            var tilesheet = TilesheetDefinitionSerializer.Load(looseFrame.Address.Location.Path, "terrain");
            tilesheet.Name = "earlier";
            TilesheetDefinitionSerializer.Save(looseFrame.Address.Location.Path, tilesheet);
            var animation = AnimationDefinitionSerializer.Load(cycle.Address.Location.Path, cycle.Address.Member);
            animation.Frames[0].Tilesheet = "earlier";
            animation.TilesheetSources[0].Tilesheet = "earlier";
            animation.Frames.Add(new() { Tilesheet = "terrain", RegionName = "detail" });
            animation.TilesheetSources.Add(AnimationTilesheetSourceDefinition.Packed("terrain",
                packedFrame.Address.Location.Path, packedFrame.Address.Location.AssetEntryName!));
            AnimationDefinitionSerializer.SaveAll(cycle.Address.Location.Path, [animation]);

            string deleted = removeImage ? Path.Combine(root, "atlas.png") : looseFrame.Address.Location.Path;
            var catalog = (AssetPackageCatalog)typeof(SceneEditorControl)
                .GetField("_packages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
            int prompts = 0;
            catalog.PasswordProvider = _ =>
            {
                prompts++;
                File.Delete(deleted);
                return password;
            };

            var document = editor.Document;
            var layer = Assert.Single(document.Definition.Layers);
            document.History.ResetSaved();
            var args = SceneDragAt(preview, new ResourceDragData<AnimationDrag>(cycle), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(1, prompts);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Empty(layer.Tiles);
            Assert.Empty(document.Definition.AnimationSources);
            Assert.False(document.IsDirty);
            Assert.False(document.History.CanUndo);
        });

    [Fact]
    public void DropClearsOnlyTheRestoredDeclaredSourceWarning() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var document = editor.Document;
            var layer = Assert.Single(document.Definition.Layers);
            document.SetLooseTilesheetSource("terrain", Path.Combine(root, "atlas.gts"));
            document.SetLooseTilesheetSource("unrelated", Path.Combine(root, "unrelated.gts"));
            Application.DoEvents();
            var validation = (TextBox)typeof(SceneEditorControl)
                .GetField("_validation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
            Assert.Contains("Could not load GTS source 'terrain'", validation.Text);
            Assert.Contains("Could not load GTS source 'unrelated'", validation.Text);

            var (frame, _) = CreateDropResources(root, packed: false);
            var args = SceneDragAt(preview, new ResourceDragData<TilesheetFrameDrag>(frame), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Application.DoEvents();
            Assert.DoesNotContain("Could not load GTS source 'terrain'", validation.Text);
            Assert.Contains("Could not load GTS source 'unrelated'", validation.Text);
            Assert.Equal(2, document.Definition.TilesheetSources.Count);
        });

    [Fact]
    public void AnimationDropReplacesAFailedDependencyPreviewAfterRepair() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var (_, cycle) = CreateDropResources(root, packed: false);
            string image = Path.Combine(root, "atlas.png");
            File.Move(image, image + ".bak");
            var document = editor.Document;
            var layer = Assert.Single(document.Definition.Layers);
            document.SetLooseAnimationSource("water.flow", cycle.Address.Location.Path);
            Application.DoEvents();
            var list = Descendants(editor).OfType<ListView>().Single(control => control.Items.Count == 1 &&
                control.Items[0].Tag is SceneAnimationSource);
            var cached = Assert.IsType<SceneAnimationSource>(list.Items[0].Tag);
            Assert.NotEmpty(cached.Diagnostics);
            Assert.Null(cached.FindTilesheet("terrain")?.Image);

            File.Move(image + ".bak", image);
            var args = SceneDragAt(preview, new ResourceDragData<AnimationDrag>(cycle), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Application.DoEvents();
            var repaired = Assert.IsType<SceneAnimationSource>(list.Items[0].Tag);
            Assert.NotSame(cached, repaired);
            Assert.Empty(repaired.Diagnostics);
            Assert.NotNull(repaired.FindTilesheet("terrain")?.Image);
            Assert.Single(document.Definition.AnimationSources);
        });

    [Fact]
    public void DropPreservesThePaintFrameWhenReplacingItsTilesheetPreview() =>
        WithPaintingEditor((editor, preview, sourceTree, root) =>
        {
            var layer = Assert.Single(editor.Definition.Layers);
            SelectPaintFrame(sourceTree, x: 1, y: 1);
            var oldSource = Assert.IsType<SceneTilesheetSource>(sourceTree.Nodes[0].Tag);
            string path = Path.Combine(root, "terrain.gts");
            var saved = TilesheetDefinitionSerializer.Load(path, "terrain");
            saved.Regions.Add(new() { Name = "extra", Area = new(0, 0, 16, 16), TileSize = new(16, 16) });
            TilesheetDefinitionSerializer.Save(path, saved);

            var frame = new TilesheetFrameDrag(new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition, path), "terrain"),
                "detail", 0, 0);
            var args = SceneDragAt(preview, new ResourceDragData<TilesheetFrameDrag>(frame), PreviewCellPoint(preview, layer, 1, 1));
            RaiseSceneDrag(preview, "Enter", args);
            RaiseSceneDrag(preview, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Application.DoEvents();
            Assert.NotSame(oldSource, sourceTree.Nodes[0].Tag);

            Tool(editor, "Paint").PerformClick();
            ClickCell(preview, layer, 2, 2);
            var painted = editor.Document.FindTile(layer, 2, 2);
            Assert.NotNull(painted);
            Assert.Equal(("terrain", "detail", 1, 1), FrameValues(painted));
        });

    private static void WithResourceDropEditor(Action<SceneEditorControl, ScenePreviewControl, string> test) => RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "GscnResourceDrop-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var document = SceneDocument.Create(root);
            document.AddLayer();
            using var host = new SceneEditorDocument(document, (_, _) => false)
            {
                Size = new(1500, 950),
                Opacity = 0,
                ShowInTaskbar = false,
                CloseApproved = true
            };
            host.Show();
            Application.DoEvents();
            test(host.Editor, Descendants(host.Editor).OfType<ScenePreviewControl>().Single(), root);
        }
        finally { Directory.Delete(root, recursive: true); }
    });

    private static void SelectDropLayer(SceneEditorControl editor, SceneLayerDefinition layer)
    {
        var structure = Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes.Count > 0 && tree.Nodes[0].Tag is SceneDefinition);
        structure.SelectedNode = structure.Nodes[0].Nodes.Cast<TreeNode>().Single(node => ReferenceEquals(node.Tag, layer));
        Application.DoEvents();
    }

    private static DragEventArgs SceneDragAt(ScenePreviewControl preview, IDataObject data, Point client)
    {
        var screen = preview.PointToScreen(client);
        return new(data, 0, screen.X, screen.Y, DragDropEffects.Copy, DragDropEffects.None);
    }

    private static void RaiseSceneDrag(Control preview, string phase, EventArgs args) =>
        typeof(Control).GetMethod("OnDrag" + phase, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(preview, [args]);

    private static (string?, string?, int?, int?) FrameValues(SceneLayerTileDefinition tile) =>
        (tile.Frame?.Tilesheet, tile.Frame?.RegionName, tile.Frame?.XTile, tile.Frame?.YTile);

    private static ResourceAddress DropSourceAddress(SceneDocument document, bool animation)
    {
        if (animation)
        {
            Assert.Empty(document.Definition.TilesheetSources);
            var source = Assert.Single(document.Definition.AnimationSources);
            return new(new AssetLocation(AssetTypes.AnimationDefinition, document.ResolveReferencePath(source.GaniPath ?? source.AssetsFilePath!),
                source.AssetEntryName), source.AnimationKey);
        }

        Assert.Empty(document.Definition.AnimationSources);
        var sheet = Assert.Single(document.Definition.TilesheetSources);
        return new(new AssetLocation(AssetTypes.TilesheetDefinition, document.ResolveReferencePath(sheet.GtsPath ?? sheet.AssetsFilePath!),
            sheet.AssetEntryName), sheet.Tilesheet);
    }

    private static (TilesheetFrameDrag Frame, AnimationDrag Animation) CreateDropResources(string root, bool packed, string? password = null)
    {
        Directory.CreateDirectory(root);
        const string imageEntry = "images/atlas.png";
        const string tilesheetEntry = "tiles/nested/atlas.gts";
        const string animationEntry = "animations/nested/motion.gani";
        const string archiveName = "resources.gaf";
        string archivePath = Path.Combine(root, archiveName);
        string tilesheetPath = Path.Combine(root, "atlas.gts");
        string animationPath = Path.Combine(root, "motion.gani");
        var tilesheet = new TilesheetDefinition
        {
            Name = "terrain",
            Image = packed ? new() { AssetEntryName = imageEntry } : new() { FilePath = "atlas.png" },
            Regions = [new() { Name = "detail", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
        };
        var animation = new AnimationDefinition
        {
            Key = "water.flow",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain", RegionName = "detail", XTile = 1, YTile = 1 }],
            TilesheetSources = [packed
                ? AnimationTilesheetSourceDefinition.Packed("terrain", archiveName, tilesheetEntry)
                : AnimationTilesheetSourceDefinition.Loose("terrain", "atlas.gts")]
        };

        using var image = new Bitmap(32, 32);
        if (packed)
        {
            using var archive = AssetsFile.LoadOrCreate(archivePath, password, encrypt: password is not null, register: false);
            using var png = new MemoryStream();
            image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            png.Position = 0;
            archive.Add(AssetTypes.Image, imageEntry, png);
            using var gts = new MemoryStream(Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions = [new() { Name = "other" }, tilesheet]
            })));
            archive.Add(AssetTypes.TilesheetDefinition, tilesheetEntry, gts);
            using var gani = new MemoryStream(Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions = [new() { Key = "other" }, animation]
            })));
            archive.Add(AssetTypes.AnimationDefinition, animationEntry, gani);
            archive.Save();
        }
        else
        {
            image.Save(Path.Combine(root, "atlas.png"), System.Drawing.Imaging.ImageFormat.Png);
            TilesheetDefinitionSerializer.SaveAll(tilesheetPath, [new() { Name = "other" }, tilesheet]);
            AnimationDefinitionSerializer.SaveAll(animationPath, [new() { Key = "other" }, animation]);
        }

        return (
            new(new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition,
                packed ? archivePath : tilesheetPath, packed ? tilesheetEntry : null), "terrain"), "detail", 1, 1),
            new(new ResourceAddress(new AssetLocation(AssetTypes.AnimationDefinition,
                packed ? archivePath : animationPath, packed ? animationEntry : null), "water.flow")));
    }
}
