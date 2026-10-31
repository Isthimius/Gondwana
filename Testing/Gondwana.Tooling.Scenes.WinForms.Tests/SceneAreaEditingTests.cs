using System.ComponentModel;
using System.Reflection;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;
using Gondwana.Tooling.Scenes.Editing;
using Newtonsoft.Json;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    public static IEnumerable<object[]> RectangleCases => Enum.GetValues<CoordinateSystemTypes>()
        .SelectMany(p => new[] { false, true }.SelectMany(reverse => new[] { false, true }.Select(erase => new object[] { p, reverse, erase })));

    [Theory, MemberData(nameof(RectangleCases))]
    public void RectangleCommitsOnlyOnReleaseAndRoundTrips(CoordinateSystemTypes projection, bool reverse, bool erase) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = document.Definition.Layers[0];
            layer.CoordinateSystemType = projection; layer.OriginPx = new(17, -23);
            document.EditTile(layer, 2, 2, "Seed", t => { t.Nickname = "keep"; t.Transform = TileTransform.Rotate90; t.EnableFog = true; });
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            string before = JsonConvert.SerializeObject(layer.Tiles);
            Tool(editor, erase ? "Rectangle Erase" : "Rectangle Paint").PerformClick();
            preview.SetZoom(reverse ? 1.5f : .75f);
            var start = PreviewCellPoint(preview, layer, reverse ? 4 : 1, reverse ? 4 : 1);
            var end = PreviewCellPoint(preview, layer, reverse ? 1 : 4, reverse ? 1 : 4);
            Pointer(preview, "OnMouseDown", start); Pointer(preview, "OnMouseMove", end);
            Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
            Assert.False(document.History.InTransaction);
            Assert.Equal(new Rectangle(1, 1, 4, 4), editor.SelectedCells);
            Pointer(preview, "OnMouseUp", end);
            if (erase) Assert.Empty(layer.Tiles);
            else
            {
                Assert.Equal(16, layer.Tiles.Count);
                Assert.Equal("keep", document.FindTile(layer, 2, 2)!.Nickname);
                Assert.Equal(TileTransform.Rotate90, document.FindTile(layer, 2, 2)!.Transform);
            }
            string after = JsonConvert.SerializeObject(layer.Tiles);
            document.History.Undo(); Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
            Assert.False(document.History.CanUndo); Assert.False(document.IsDirty);
            document.History.Redo(); Assert.Equal(after, JsonConvert.SerializeObject(layer.Tiles));
            document.Save(Path.Combine(root, "area.gscn"), allowInvalid: true);
            using var reopened = SceneDocument.Open(document.FilePath!);
            Assert.Equal(after, JsonConvert.SerializeObject(reopened.Definition.Layers[0].Tiles));
        });

    [Theory]
    [InlineData("Rectangle Paint", false)]
    [InlineData("Rectangle Paint", true)]
    [InlineData("Rectangle Erase", false)]
    [InlineData("Rectangle Erase", true)]
    [InlineData("Fill", false)]
    [InlineData("Fill", true)]
    public void AreaGestureCancellationLeavesSparseContentUnchanged(string tool, bool captureLoss) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Tool(editor, tool).PerformClick();
            var point = PreviewCellPoint(preview, editor.Definition.Layers[0], 1, 1);
            Pointer(preview, "OnMouseDown", point);
            if (captureLoss) preview.Capture = false;
            else Assert.True(InvokeHistoryShortcut(editor, Keys.Escape));
            Assert.Empty(editor.Definition.Layers[0].Tiles);
            Assert.False(editor.Document.History.InTransaction); Assert.False(editor.Document.IsDirty);
        });

    [Theory]
    [InlineData(CoordinateSystemTypes.Orthogonal)]
    [InlineData(CoordinateSystemTypes.IsometricRhombic)]
    [InlineData(CoordinateSystemTypes.IsometricAxial)]
    [InlineData(CoordinateSystemTypes.ObliqueLeft)]
    [InlineData(CoordinateSystemTypes.ObliqueRight)]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop)]
    public void FillEmptyRegionIsBoundedSparseGroupedAndNoOpOnRepeat(CoordinateSystemTypes projection) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = document.Definition.Layers[0];
            layer.Columns = layer.Rows = 8; layer.CoordinateSystemType = projection;
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Tool(editor, "Fill").PerformClick();
            var point = PreviewCellPoint(preview, layer, 2, 2);
            ClickPreview(preview, point); Assert.Equal(64, layer.Tiles.Count);
            Assert.All(layer.Tiles, t => Assert.Equal(("terrain", "detail", 1, 1),
                (t.Frame!.Tilesheet, t.Frame.RegionName, t.Frame.XTile, t.Frame.YTile)));
            ClickPreview(preview, point);
            document.History.Undo(); Assert.Empty(layer.Tiles); Assert.False(document.History.CanUndo);
            Assert.False(document.IsDirty); document.History.Redo(); Assert.Equal(64, layer.Tiles.Count);
        });

    [Fact]
    public void FillStopsAtDifferentAssignmentAndPreservesIndependentMetadata() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = document.Definition.Layers[0];
            layer.Columns = layer.Rows = 5;
            for (int y = 0; y < 5; y++) document.EditTile(layer, 2, y, "Barrier", t => t.Frame = new() { Tilesheet = "barrier" });
            document.EditTile(layer, 1, 1, "Metadata", t => { t.Nickname = "kept"; t.Transform = TileTransform.Rotate90; t.EnableFog = true; });
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            string before = JsonConvert.SerializeObject(layer.Tiles);
            Tool(editor, "Fill").PerformClick(); ClickPreview(preview, PreviewCellPoint(preview, layer, 0, 0));
            Assert.Equal(15, layer.Tiles.Count); Assert.Null(document.FindTile(layer, 3, 1));
            Assert.Equal("kept", document.FindTile(layer, 1, 1)!.Nickname);
            Assert.True(document.FindTile(layer, 1, 1)!.EnableFog);
            document.History.Undo(); Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
        });

    [Theory]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop, 2, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop, 3, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop, 2, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop, 2, 3)]
    public void HexFillNeighborsAreSixDistinctReciprocalEdgeNeighbors(CoordinateSystemTypes projection, int x, int y)
    {
        var cell = new Point(x, y);
        var neighbors = SceneEditorControl.FillNeighbors(projection, cell).ToArray();
        Assert.Equal(6, neighbors.Distinct().Count());
        Assert.DoesNotContain(cell, neighbors);
        Assert.All(neighbors, n => Assert.Contains(cell, SceneEditorControl.FillNeighbors(projection, n)));
        if (projection == CoordinateSystemTypes.HexAxialFlatTop)
            Assert.Contains(new Point(x + 1, y + ((x & 1) == 0 ? -1 : 1)), neighbors);
        else Assert.Contains(new Point(x + ((y & 1) == 0 ? -1 : 1), y + 1), neighbors);
    }

    [Fact]
    public void BulkPropertiesUseExplicitChoicesAndRestoreMixedValueTypes() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = document.Definition.Layers[0];
            document.EditTile(layer, 1, 1, "Seed", t => { t.Visible = false; t.Nickname = "one"; t.Transform = TileTransform.Rotate90; });
            document.EditTile(layer, 2, 1, "Seed", t => { t.EnableFog = true; t.Nickname = "two"; });
            var tiles = layer.Tiles.ToArray();
            SaveGestureBaseline(editor, root);
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 1));
            var values = new BulkTilePropertiesDialog.Values(tiles);
            Assert.Contains("Visible", values.Mixed);
            Assert.Null(TypeDescriptor.GetProperties(values)["Visible"]!.GetValue(values));
            Assert.DoesNotContain(BulkTilePropertiesDialog.EditableProperties, p => p.Name is "Id" or "Nickname");
            string before = JsonConvert.SerializeObject(layer.Tiles);
            editor.ApplyBulkProperties(layer, tiles, new Dictionary<string, object?> { ["Visible"] = true, ["AdjustCollisionArea"] = new CollisionAdjust(1, 2, 3, 4) });
            Assert.Equal(2, layer.Tiles.Count); Assert.All(tiles, t => Assert.True(t.Visible));
            Assert.All(tiles, t => Assert.Equal(new CollisionAdjust(1, 2, 3, 4), t.AdjustCollisionArea));
            Assert.Equal("one", tiles[0].Nickname); Assert.Equal(TileTransform.Rotate90, tiles[0].Transform);
            Assert.True(tiles[1].EnableFog);
            editor.ApplyBulkProperties(layer, tiles, new Dictionary<string, object?> { ["Visible"] = true });
            document.History.Undo(); Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
            Assert.False(document.History.CanUndo); Assert.False(document.IsDirty);
        });

    [Fact]
    public void KeyboardSelectionStaysBoundedAndDoesNotEdit() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var layer = editor.Definition.Layers[0];
            SaveGestureBaseline(editor, root);
            ClickPreview(preview, PreviewCellPoint(preview, layer, 0, 0));
            bool Navigate(Keys keys) => (bool)typeof(SceneEditorControl).GetMethod("NavigateSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, [keys])!;
            Assert.True(Navigate(Keys.Left)); Assert.Equal(new Rectangle(0, 0, 1, 1), editor.SelectedCells);
            Assert.True(Navigate(Keys.Shift | Keys.Right)); Assert.True(Navigate(Keys.Shift | Keys.Down));
            Assert.Equal(new Rectangle(0, 0, 2, 2), editor.SelectedCells);
            Assert.True(Navigate(Keys.Escape)); Assert.Equal(new Rectangle(1, 1, 1, 1), editor.SelectedCells);
            Assert.False(Navigate(Keys.Control | Keys.Z));
            Assert.Empty(layer.Tiles); Assert.False(editor.Document.IsDirty);
        });

    [Theory]
    [InlineData("Fill")]
    [InlineData("Rectangle Paint")]
    [InlineData("Rectangle Erase")]
    public void LargeAreaHasExplicitConfirmationAndSingleUndo(string tool) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            layer.Columns = layer.Rows = 101;
            SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Tool(editor, tool).PerformClick();
            int confirmations = 0;
            editor.ConfirmLargeEdit = count => { Assert.Equal(10201, count); confirmations++; return false; };
            void Run()
            {
                // Invoke the same gesture handlers with grid endpoints; no huge desktop canvas is needed.
                typeof(SceneEditorControl).GetMethod("BeginPreviewGesture", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, [layer, 0, 0]);
                typeof(SceneEditorControl).GetMethod("PreviewCellClicked", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, [layer, 100, 100]);
                typeof(SceneEditorControl).GetMethod("EndPreviewGesture", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, [false]);
            }
            Run(); Assert.Equal(1, confirmations); Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
            editor.ConfirmLargeEdit = _ => true;
            Run();
            if (tool == "Rectangle Erase") { Assert.Empty(layer.Tiles); Assert.False(document.History.CanUndo); return; }
            Assert.Equal(10201, layer.Tiles.Count); Assert.Equal(tool == "Fill" ? "Fill region" : "Rectangle edit", document.History.UndoDescription);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                document.History.Undo(); Assert.Empty(layer.Tiles); Assert.False(document.History.CanUndo);
                document.History.Redo(); Assert.Equal(10201, layer.Tiles.Count);
            }
        });

    [Theory]
    [InlineData("Fill")]
    [InlineData("Rectangle Paint")]
    public void AreaRejectsMissingSourceAndAnimationAssignmentIsExact(string tool) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            layer.Columns = layer.Rows = 3;
            SaveGestureBaseline(editor, root); Tool(editor, tool).PerformClick();
            ClickPreview(preview, PreviewCellPoint(preview, layer, 1, 1));
            Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
            string gani = Path.Combine(root, "area.gani");
            Gondwana.Drawing.Animation.GANI.AnimationDefinitionSerializer.Save(gani, new Gondwana.Drawing.Animation.GANI.AnimationDefinition
            {
                Key = "exact.animation",
                ThrottleTime = .1,
                TilesheetSources = [Gondwana.Drawing.Animation.GANI.AnimationTilesheetSourceDefinition.Loose("terrain", "terrain.gts")],
                Frames = [new() { Tilesheet = "terrain", RegionName = "detail", XTile = 0, YTile = 1 }]
            });
            editor.AddAnimationSources([gani]); Application.DoEvents();
            var animations = Descendants(editor).OfType<ListView>().Single();
            animations.Items[0].Selected = true; animations.Focus();
            SaveGestureBaseline(editor, root);
            ClickPreview(preview, PreviewCellPoint(preview, layer, 1, 1));
            Assert.NotEmpty(layer.Tiles);
            Assert.All(layer.Tiles, t =>
            {
                Assert.Equal("exact.animation", t.AnimationKey); Assert.True(t.EnableAnimator); Assert.True(t.StartAnimation);
                Assert.Equal(("terrain", "detail", 0, 1), (t.Frame!.Tilesheet, t.Frame.RegionName, t.Frame.XTile, t.Frame.YTile));
            });
            ClickPreview(preview, PreviewCellPoint(preview, layer, 1, 1));
            document.History.Undo(); Assert.Empty(layer.Tiles); Assert.False(document.History.CanUndo);
        });

    [Fact]
    public void BulkFailureRollsBackAllPreviouslyChangedValues() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            document.EditTile(layer, 1, 1, "Seed", t => t.EnableFog = true);
            document.EditTile(layer, 2, 1, "Seed", t => t.Nickname = "two");
            SaveGestureBaseline(editor, root);
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 2, 1));
            string before = JsonConvert.SerializeObject(layer.Tiles);
            Assert.ThrowsAny<Exception>(() => editor.ApplyBulkProperties(layer, layer.Tiles.ToArray(),
                new Dictionary<string, object?> { ["Visible"] = false, ["Id"] = Guid.NewGuid() }));
            Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
            Assert.False(document.History.InTransaction); Assert.False(document.IsDirty);
        });

    [Fact]
    public void PreviewNavigationDoesNotInterceptOtherControlFocus() =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            SaveGestureBaseline(editor, root);
            ClickPreview(preview, PreviewCellPoint(preview, editor.Definition.Layers[0], 1, 1));
            var method = typeof(ScenePreviewControl).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
            preview.Focus(); Assert.True(preview.Focused);
            Assert.True((bool)method.Invoke(preview, [new Message(), Keys.Shift | Keys.Right])!);
            Assert.Equal(new Rectangle(1, 1, 2, 1), editor.SelectedCells);
            source.Focus(); Assert.False(preview.Focused);
            method.Invoke(preview, [new Message(), Keys.Right]);
            Assert.Equal(new Rectangle(1, 1, 2, 1), editor.SelectedCells);
            Assert.False(editor.Document.IsDirty);
        });

    [Theory]
    [InlineData("Rectangle Paint")]
    [InlineData("Rectangle Erase")]
    [InlineData("Fill")]
    public void NewToolsCannotRedirectToAnotherSceneMember(string tool) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var original = document.Definition;
            var layer = original.Layers[0]; var other = document.AddDefinition(); document.AddLayer();
            editor.SelectDefinition(original); Application.DoEvents(); SelectPaintFrame(source); SaveGestureBaseline(editor, root);
            Tool(editor, tool).PerformClick();
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            editor.SelectDefinition(other); Application.DoEvents();
            Assert.False(preview.HasGesture); Assert.False(document.History.InTransaction);
            Assert.Empty(layer.Tiles); Assert.Empty(other.Layers[0].Tiles); Assert.False(document.IsDirty);
        });

    [Fact]
    public void CollisionInsetConverterCreatesWholeValueAndRejectsInvalidInput()
    {
        var converter = TypeDescriptor.GetProperties(typeof(TilePropertyAdapter))["AdjustCollisionArea"]!.Converter;
        var value = Assert.IsType<CollisionAdjust>(converter.ConvertFromInvariantString("1, -2, 3, 4"));
        Assert.Equal(new CollisionAdjust(1, -2, 3, 4), value);
        Assert.Equal("1, -2, 3, 4", converter.ConvertToInvariantString(value));
        Assert.Throws<FormatException>(() => converter.ConvertFromInvariantString("1, 2"));
    }

    [Theory]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop, 2, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop, 3, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop, 2, 2)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop, 2, 3)]
    public void FillNeighborTopologyMatchesExistingProjectedEdges(CoordinateSystemTypes coordinates, int x, int y) => RunSta(() =>
    {
        using var preview = new ScenePreviewControl();
        var layer = new Gondwana.Scenes.GSCN.SceneLayerDefinition { Columns = 8, Rows = 8, TileWidth = 32, TileHeight = 32, CoordinateSystemType = coordinates };
        var projection = typeof(ScenePreviewControl).GetMethod("GetProjection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(preview, [layer]);
        var outline = typeof(ScenePreviewControl).GetMethod("TileOutline", BindingFlags.NonPublic | BindingFlags.Static)!;
        PointF[] Polygon(Point p) => (PointF[])outline.Invoke(null, [projection, layer, p.X, p.Y])!;
        var center = Polygon(new(x, y));
        Assert.All(SceneEditorControl.FillNeighbors(coordinates, new(x, y)), p => Assert.Equal(2, center.Intersect(Polygon(p)).Count()));
    });

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Unchanged")]
    [InlineData("Apply")]
    public void BulkDialogCommitsOnlyCheckedConfirmedProperties(string action) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document; var layer = editor.Definition.Layers[0];
            document.EditTile(layer, 1, 1, "Seed", t => t.EnableFog = true);
            document.EditTile(layer, 2, 1, "Seed", t => t.Visible = false);
            SaveGestureBaseline(editor, root);
            Pointer(preview, "OnMouseDown", PreviewCellPoint(preview, layer, 1, 1));
            Pointer(preview, "OnMouseUp", PreviewCellPoint(preview, layer, 3, 1));
            string before = JsonConvert.SerializeObject(layer.Tiles);
            Exception? failure = null;
            editor.BeginInvoke(() =>
            {
                var dialog = Application.OpenForms.OfType<BulkTilePropertiesDialog>().Single();
                try
                {
                    var grid = dialog.Controls.OfType<PropertyGrid>().Single();
                    var choices = dialog.Controls.OfType<CheckedListBox>().Single();
                    TypeDescriptor.GetProperties(grid.SelectedObject)["Visible"]!.SetValue(grid.SelectedObject, true);
                    TypeDescriptor.GetProperties(grid.SelectedObject)["Transform"]!.SetValue(grid.SelectedObject, TileTransform.Rotate90);
                    if (action != "Unchanged") choices.SetItemChecked(choices.Items.IndexOf("Visible"), true);
                    Descendants(dialog).OfType<Button>().Single(b => b.Text == (action == "Cancel" ? "Cancel" : "Apply")).PerformClick();
                }
                catch (Exception error) { failure = error; dialog.Close(); }
            });
            Descendants(editor).OfType<ToolStrip>().SelectMany(b => b.Items.Cast<ToolStripItem>())
                .Single(item => item.Text == "Edit selected properties").PerformClick();
            Assert.Null(failure);
            if (action == "Apply")
            {
                Assert.All(layer.Tiles, t => { Assert.True(t.Visible); Assert.Equal(TileTransform.Identity, t.Transform); });
                Assert.Equal(2, layer.Tiles.Count); document.History.Undo();
            }
            Assert.Equal(before, JsonConvert.SerializeObject(layer.Tiles));
            Assert.False(document.History.CanUndo); Assert.False(document.IsDirty);
        });
}
