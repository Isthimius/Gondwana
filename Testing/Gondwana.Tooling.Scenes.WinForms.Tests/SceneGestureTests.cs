using System.Reflection;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;
using Newtonsoft.Json;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    private static void Pointer(ScenePreviewControl preview, string method, Point location) =>
        typeof(ScenePreviewControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0)]);

    private static void SaveGestureBaseline(SceneEditorControl editor, string root)
    {
        editor.Document.Save(Path.Combine(root, "scene.gscn"));
        Application.DoEvents();
        editor.Document.History.Clear();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragStrokeGroupsMixedSparseCellsAndDeduplicatesRevisits(bool erase) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            document.Definition.CollisionGroups.Add("walls");
            document.Definition.CollisionProfiles.Add(new() { Name = "solid", CollisionGroup = "walls" });
            document.EditTile(layer, 2, 2, "Seed tile", tile =>
            {
                tile.Id = Guid.NewGuid(); tile.Nickname = "original"; tile.Transform = TileTransform.Rotate90;
                tile.CollisionProfileName = "solid"; tile.EnableFog = true;
            });
            var original = Assert.Single(layer.Tiles);
            string metadata = JsonConvert.SerializeObject(original);
            SelectPaintFrame(source);
            SaveGestureBaseline(editor, root);
            Tool(editor, erase ? "Erase" : "Paint").PerformClick();
            var start = PreviewCellPoint(preview, layer, 1, 2);
            var end = PreviewCellPoint(preview, layer, 7, 2);
            Pointer(preview, "OnMouseDown", start);
            Assert.True(document.History.InTransaction);
            Pointer(preview, "OnMouseMove", end);
            int changed = layer.Tiles.Count;
            Pointer(preview, "OnMouseMove", start);
            Assert.Equal(changed, layer.Tiles.Count);
            Pointer(preview, "OnMouseUp", end);
            Assert.False(document.History.InTransaction); Assert.False(preview.Capture);
            if (erase) Assert.Empty(layer.Tiles);
            else
            {
                Assert.Equal(7, layer.Tiles.Count);
                Assert.Equal(Enumerable.Range(1, 7), layer.Tiles.Select(tile => tile.X).Order());
                Assert.Same(original, document.FindTile(layer, 2, 2));
                Assert.Equal(TileTransform.Rotate90, original.Transform); Assert.Equal("solid", original.CollisionProfileName);
                Assert.All(layer.Tiles, tile => Assert.Equal(("terrain", "detail", 1, 1),
                    (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile)));
                Assert.NotSame(layer.Tiles[0].Frame, layer.Tiles[1].Frame);
            }
            string after = SceneDefinitionSerializer.ToJson(document.Definition);
            document.History.Undo(); Application.DoEvents();
            Assert.Same(original, Assert.Single(layer.Tiles));
            Assert.Equal(metadata, JsonConvert.SerializeObject(original));
            Assert.False(document.IsDirty); Assert.False(document.History.CanUndo);
            document.History.Redo(); Application.DoEvents();
            Assert.Equal(after, SceneDefinitionSerializer.ToJson(document.Definition));
            document.Save(Path.Combine(root, "completed.gscn"), allowInvalid: true);
            using var reopened = SceneDocument.Open(document.FilePath!);
            Assert.Equal(JsonConvert.SerializeObject(layer.Tiles), JsonConvert.SerializeObject(reopened.Definition.Layers[0].Tiles));
        });

    [Theory]
    [InlineData("Escape")]
    [InlineData("Capture")]
    [InlineData("Tool")]
    [InlineData("Layer")]
    [InlineData("Member")]
    [InlineData("RemoveLayer")]
    [InlineData("Reload")]
    [InlineData("EditorDispose")]
    [InlineData("DocumentDispose")]
    [InlineData("CommitEdits")]
    public void InterruptedStrokeRollsBackAndClosesTransaction(string interruption) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            var second = document.AddLayer();
            var other = document.AddDefinition();
            editor.SelectDefinition(document.Definitions[0]); Application.DoEvents();
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Tool(editor, "Paint").PerformClick();
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseMove", PreviewCellPoint(preview, layer, 5, 1));
            Assert.NotEmpty(layer.Tiles);
            switch (interruption)
            {
                case "Escape": Assert.True(InvokeHistoryShortcut(editor, Keys.Escape)); break;
                case "Capture": preview.Capture = false; break;
                case "Tool": Tool(editor, "Select").PerformClick(); break;
                case "Layer":
                    var tree = Descendants(editor).OfType<TreeView>().Single(t => t.Nodes[0].Tag is SceneDefinition);
                    tree.SelectedNode = tree.Nodes[0].Nodes.Cast<TreeNode>().Single(n => ReferenceEquals(n.Tag, second));
                    break;
                case "Member": editor.SelectDefinition(other); break;
                case "RemoveLayer": document.RemoveLayer(layer); break;
                case "Reload": editor.ReloadSources(); break;
                case "EditorDispose": editor.Dispose(); break;
                case "DocumentDispose": document.Dispose(); break;
                case "CommitEdits": Assert.True(editor.CommitEdits()); break;
            }
            Assert.Empty(layer.Tiles); Assert.Empty(second.Tiles);
            Assert.False(document.History.InTransaction);
            if (!editor.IsDisposed) Assert.False(preview.HasGesture);
            if (interruption == "RemoveLayer") Assert.DoesNotContain(layer, document.Definition.Layers);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoOpStrokeLeavesHistoryAndDirtyStateUnchanged(bool erase) => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        SelectPaintFrame(source); Tool(editor, "Paint").PerformClick();
        if (!erase)
        {
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 5, 1));
        }
        SaveGestureBaseline(editor, root);
        Tool(editor, erase ? "Erase" : "Paint").PerformClick();
        Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
        Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 5, 1));
        Assert.False(document.IsDirty); Assert.False(document.History.CanUndo); Assert.False(document.History.InTransaction);
    });

    [Fact]
    public void MouseUpOutsideCommitsOnlyValidCellsAndSaveAndUndoCannotReplayOpenStroke() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        SelectPaintFrame(source); SaveGestureBaseline(editor, root); Tool(editor, "Paint").PerformClick();
        Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
        Assert.Throws<InvalidOperationException>(() => document.Save(Path.Combine(root, "unfinished.gscn")));
        Assert.False(File.Exists(Path.Combine(root, "unfinished.gscn")));
        Assert.Throws<InvalidOperationException>(() => document.History.Undo());
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Z)); // UI routing rejects unfinished history.
        Assert.Single(layer.Tiles);
        var outside = PreviewCellPoint(preview, layer, 9, 1); outside.X += (int)(layer.TileWidth * preview.Zoom * 5);
        Pointer(preview, "OnMouseUp", outside);
        Assert.False(document.History.InTransaction); Assert.False(preview.HasGesture);
        Assert.Equal(9, layer.Tiles.Count);
        Assert.All(layer.Tiles, tile => Assert.InRange(tile.X, 1, 9));
        document.History.Undo(); Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
    });

    [Fact]
    public void StrokeCapturesGaniSourceAndIgnoresLaterSourceSelection() => WithPaintingEditor((editor, preview, source, root) =>
    {
        string path = Path.Combine(root, "flow.gani");
        AnimationDefinitionSerializer.Save(path, new AnimationDefinition
        {
            Key = "water.flow",
            ThrottleTime = .1,
            Frames = [new() { Tilesheet = "terrain", RegionName = "detail", XTile = 0, YTile = 1 }]
        });
        editor.AddAnimationSources([path]); Application.DoEvents();
        var list = Descendants(editor).OfType<ListView>().Single(); list.Items[0].Selected = true; list.Focus();
        SaveGestureBaseline(editor, root);
        Tool(editor, "Paint").PerformClick();
        var layer = editor.Definition.Layers[0];
        Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
        // Change selection without moving keyboard focus or losing mouse capture.
        var region = source.Nodes[0].Nodes[0]; region.Expand(); var row = region.Nodes[1]; row.Expand();
        source.SelectedNode = row.Nodes[1];
        Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 5, 1));
        Assert.Equal(5, layer.Tiles.Count);
        Assert.All(layer.Tiles, tile =>
        {
            Assert.Equal("water.flow", tile.AnimationKey); Assert.True(tile.EnableAnimator); Assert.True(tile.StartAnimation);
            Assert.Equal(("terrain", "detail", 0, 1), (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile));
        });
        editor.Document.History.Undo(); Application.DoEvents(); Assert.Empty(layer.Tiles);
        list.Focus();
        Tool(editor, "Select").PerformClick();
        Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
        Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 2));
        var apply = Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>())
            .Single(item => item.Text == "Apply to selection");
        apply.PerformClick(); apply.PerformClick();
        Assert.Equal(6, layer.Tiles.Count);
        Assert.All(layer.Tiles, tile =>
        {
            Assert.Equal("water.flow", tile.AnimationKey); Assert.True(tile.EnableAnimator); Assert.True(tile.StartAnimation);
            Assert.Equal(("terrain", "detail", 0, 1), (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile));
        });
        editor.Document.History.Undo(); Assert.Empty(layer.Tiles);
    });

    [Theory]
    [MemberData(nameof(PaintingProjections))]
    public void RapidDragAndRectangleSelectionRespectProjectionZoomScrollAndOrigin(CoordinateSystemTypes coordinateSystem, float zoom) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            layer.Columns = 30; layer.Rows = 30; layer.CoordinateSystemType = coordinateSystem; layer.OriginPx = new(37, -19);
            var upper = document.AddLayer(); upper.ZOrder = 100;
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            preview.Configure(editor.Definition, _ => null, _ => null); preview.SetZoom(zoom); preview.AutoScrollPosition = new(20, 30);
            var start = PreviewCellPoint(preview, layer, 2, 2); var end = PreviewCellPoint(preview, layer, 8, 7);
            Tool(editor, "Paint").PerformClick();
            Pointer(preview, "OnMouseDown", start); Pointer(preview, "OnMouseUp", end);
            Assert.Contains(layer.Tiles, tile => tile.X == 2 && tile.Y == 2);
            Assert.Contains(layer.Tiles, tile => tile.X == 8 && tile.Y == 7);
            Assert.True(layer.Tiles.Count >= 7); Assert.Empty(upper.Tiles);
            // Dense world-space reference uses the exact straight segment, without rounding each
            // intermediate position to a different screen pixel and changing the pointer path.
            var projection = (Gondwana.Scenes.SceneLayer)typeof(ScenePreviewControl)
                .GetMethod("GetProjection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(preview, [layer])!;
            var offset = (PointF)typeof(ScenePreviewControl).GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(preview)!;
            var expected = new HashSet<Point>();
            for (int i = 0; i <= 10000; i++)
            {
                var world = new PointF((start.X + (end.X - start.X) * i / 10000f - offset.X) / zoom,
                    (start.Y + (end.Y - start.Y) * i / 10000f - offset.Y) / zoom);
                var grid = projection.WorldPxToGrid(world);
                var cell = new Point((int)Math.Floor(grid.X + .0001f), (int)Math.Floor(grid.Y + .0001f));
                if (cell.X >= 0 && cell.Y >= 0 && cell.X < layer.Columns && cell.Y < layer.Rows) expected.Add(cell);
            }
            var rapid = layer.Tiles.Select(tile => new Point(tile.X, tile.Y)).ToHashSet();
            Assert.True(rapid.SetEquals(expected), $"Rapid: {string.Join(';', rapid)} Expected: {string.Join(';', expected)}");
            document.History.Undo(); Application.DoEvents(); SaveGestureBaseline(editor, root);
            Tool(editor, "Select").PerformClick();
            start = PreviewCellPoint(preview, layer, 2, 2); end = PreviewCellPoint(preview, layer, 5, 4);
            Pointer(preview, "OnMouseDown", start); Pointer(preview, "OnMouseMove", end);
            Assert.Equal(new Rectangle(2, 2, 4, 3), editor.SelectedCells);
            Pointer(preview, "OnMouseUp", end);
            Assert.Equal(new Rectangle(2, 2, 4, 3), editor.SelectedCells);
            Assert.Empty(layer.Tiles); Assert.False(document.IsDirty); Assert.False(document.History.CanUndo);
            var adapter = Descendants(editor).OfType<PropertyGrid>().Select(grid => grid.SelectedObject).OfType<TilePropertyAdapter>().Single();
            Assert.Equal((5, 4), (adapter.X, adapter.Y));
            Assert.Contains(Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.OfType<ToolStripLabel>()),
                label => label.Text?.Contains("12 cells selected", StringComparison.Ordinal) == true);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionCommandsGroupMixedSparseEditsAndKeepPropertiesSingleCell(bool erase) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            document.EditTile(layer, 2, 2, "Seed tile", tile => { tile.Nickname = "keep"; tile.Transform = TileTransform.FlipHorizontal; });
            var original = Assert.Single(layer.Tiles); string before = JsonConvert.SerializeObject(original);
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 2));
            var selection = new Rectangle(1, 1, 3, 2); Assert.Equal(selection, editor.SelectedCells);
            Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>())
                .Single(item => item.Text == (erase ? "Erase selection" : "Apply to selection")).PerformClick();
            if (erase) Assert.Empty(layer.Tiles);
            else { Assert.Equal(6, layer.Tiles.Count); Assert.Equal(TileTransform.FlipHorizontal, original.Transform); }
            document.History.Undo(); Application.DoEvents();
            Assert.Same(original, Assert.Single(layer.Tiles)); Assert.Equal(before, JsonConvert.SerializeObject(original));
            Assert.False(document.IsDirty); Assert.False(document.History.CanUndo); Assert.Equal(selection, editor.SelectedCells);
            document.History.Redo(); Application.DoEvents();
            Assert.Equal(erase ? 0 : 6, layer.Tiles.Count);
            var adapter = Descendants(editor).OfType<PropertyGrid>().Select(grid => grid.SelectedObject).OfType<TilePropertyAdapter>().Single();
            adapter.EnableFog = true;
            Assert.Single(layer.Tiles, tile => tile.EnableFog);
            Assert.Equal((3, 2), (layer.Tiles.Single(tile => tile.EnableFog).X, layer.Tiles.Single(tile => tile.EnableFog).Y));
        });
    [Fact]
    public void GestureExceptionsRollBackAndSelectionCancellationRestoresPreviousRectangle() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            SelectPaintFrame(source); SaveGestureBaseline(editor, root); Tool(editor, "Paint").PerformClick();
            void Fail(SceneLayerDefinition target, int x, int y) { if (x == 3) throw new InvalidOperationException("Injected gesture failure"); }
            preview.TileSelected += Fail;
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            var error = Assert.Throws<TargetInvocationException>(() => Pointer(preview, "OnMouseMove", PreviewCellPoint(preview, layer, 5, 1)));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            preview.TileSelected -= Fail;
            Assert.Empty(layer.Tiles); Assert.False(document.History.InTransaction); Assert.False(document.IsDirty); Assert.False(preview.HasGesture);
            Tool(editor, "Select").PerformClick();
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 2, 2));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 3));
            var previous = editor.SelectedCells;
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 5, 5));
            Pointer(preview, "OnMouseMove", PreviewCellPoint(preview, layer, 8, 8));
            Assert.True(InvokeHistoryShortcut(editor, Keys.Escape));
            Assert.Equal(previous, editor.SelectedCells); Assert.False(document.IsDirty); Assert.Empty(layer.Tiles);
        });

    [Theory]
    [InlineData("Tool")]
    [InlineData("Layer")]
    [InlineData("Member")]
    [InlineData("RemoveLayer")]
    [InlineData("Reload")]
    public void RectangleSelectionCannotLeakAcrossEditingContextChanges(string change) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            var second = document.AddLayer(); var other = document.AddDefinition(); document.AddLayer();
            editor.SelectDefinition(document.Definitions[0]); Application.DoEvents();
            SaveGestureBaseline(editor, root);
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 2, 2));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 4, 4));
            Assert.Equal(new Rectangle(2, 2, 3, 3), editor.SelectedCells);
            switch (change)
            {
                case "Tool": Tool(editor, "Paint").PerformClick(); break;
                case "Layer":
                    var tree = Descendants(editor).OfType<TreeView>().Single(t => t.Nodes[0].Tag is SceneDefinition);
                    tree.SelectedNode = tree.Nodes[0].Nodes.Cast<TreeNode>().Single(n => ReferenceEquals(n.Tag, second));
                    break;
                case "Member": editor.SelectDefinition(other); break;
                case "RemoveLayer": document.RemoveLayer(layer); break;
                case "Reload": editor.ReloadSources(); break;
            }
            Application.DoEvents();
            Assert.True(editor.SelectedCells.Width * editor.SelectedCells.Height <= 1);
            Assert.All(document.Definitions.SelectMany(d => d.Layers), candidate => Assert.Empty(candidate.Tiles));
        });

    public static IEnumerable<object[]> SelectionOutlineProjections =>
        Enum.GetValues<CoordinateSystemTypes>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(SelectionOutlineProjections))]
    public void SelectionHighlightsEachProjectedCellOutline(CoordinateSystemTypes coordinateSystem) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var layer = editor.Definition.Layers[0];
            layer.Columns = 6; layer.Rows = 6; layer.CoordinateSystemType = coordinateSystem;
            preview.Configure(editor.Definition, _ => null, _ => null);
            preview.Fit();
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 2, 2));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 3));
            preview.ShowGridLines = false;
            using var image = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(image, preview.ClientRectangle);
            var projection = (Gondwana.Scenes.SceneLayer)typeof(ScenePreviewControl)
                .GetMethod("GetProjection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(preview, [layer])!;
            var offset = (PointF)typeof(ScenePreviewControl).GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(preview)!;
            for (int y = 2; y <= 3; y++) for (int x = 2; x <= 3; x++)
                {
                    var points = projection[x, y]!.OutlinePointsWorld;
                    bool highlighted = false;
                    for (int edge = 0; edge < points.Length; edge++)
                    {
                        var first = points[edge]; var second = points[(edge + 1) % points.Length];
                        int sx = (int)Math.Round((first.X + second.X) / 2f * preview.Zoom + offset.X);
                        int sy = (int)Math.Round((first.Y + second.Y) / 2f * preview.Zoom + offset.Y);
                        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                                if (sx + dx >= 0 && sx + dx < image.Width && sy + dy >= 35 && sy + dy < image.Height &&
                                    image.GetPixel(sx + dx, sy + dy).ToArgb() == Color.Gold.ToArgb()) highlighted = true;
                    }
                    Assert.True(highlighted, $"Missing projected selection outline for {coordinateSystem} ({x},{y})");
                }
            Assert.Empty(layer.Tiles);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DocumentTransactionsRestoreCompleteSparseState(bool cancel)
    {
        using var document = SceneDocument.Create(Path.GetTempPath());
        var layer = document.AddLayer();
        document.EditTile(layer, 1, 1, "Original", tile => { tile.Nickname = "original"; tile.EnableFog = true; tile.Transform = TileTransform.Rotate270; });
        var original = Assert.Single(layer.Tiles);
        string before = JsonConvert.SerializeObject(layer.Tiles);
        using (var transaction = document.History.BeginTransaction("Grouped cells"))
        {
            document.EditTile(layer, 0, 0, "Assign frame", tile => tile.Frame = new() { Tilesheet = "terrain", RegionName = "detail" });
            document.RemoveTile(layer, 1, 1);
            document.EditTile(layer, 2, 2, "Assign animation", tile => { tile.AnimationKey = "flow"; tile.EnableAnimator = true; });
            if (!cancel) transaction.Commit();
        }
        Assert.False(document.History.InTransaction);
        if (!cancel) document.History.Undo();
        Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
        Assert.Same(original, Assert.Single(layer.Tiles));
        if (!cancel) { document.History.Redo(); Assert.Equal(2, layer.Tiles.Count); }
    }

    [Fact]
    public void SelectionCommandsRejectInvalidSourcesAndHiddenLayers() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document; var layer = editor.Definition.Layers[0];
        SaveGestureBaseline(editor, root);
        Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
        Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 2));
        var apply = Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>())
            .Single(item => item.Text == "Apply to selection");
        apply.PerformClick(); Assert.Empty(layer.Tiles); Assert.False(document.IsDirty); Assert.False(document.History.CanUndo);
        SelectPaintFrame(source);
        var tree = Descendants(editor).OfType<TreeView>().Single(t => t.Nodes[0].Tag is SceneDefinition);
        tree.Nodes[0].Nodes[0].Checked = false;
        apply.PerformClick(); Assert.Empty(layer.Tiles); Assert.False(document.IsDirty); Assert.False(document.History.InTransaction);
    });

}
