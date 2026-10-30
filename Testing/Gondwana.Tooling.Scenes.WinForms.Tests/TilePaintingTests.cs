using System.Reflection;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;
using Newtonsoft.Json;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    [Fact]
    public void PaintAndEraseUseSparseHistoryAndPreserveMetadata() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        Assert.Equal(SceneEditorTool.Select, editor.ActiveTool);
        SelectPaintFrame(source);
        document.Save(Path.Combine(root, "scene.gscn"));
        string before = SceneDefinitionSerializer.ToJson(document.Definition);
        foreach (var mode in new[] { "Paint", "Erase", "Select", "Select" })
        {
            Tool(editor, mode).PerformClick();
            Assert.Single(new[] { "Select", "Paint", "Erase" }, name => Tool(editor, name).Checked);
            Assert.False(document.IsDirty);
        }
        ClickCell(preview, layer, 2, 3);
        Assert.Empty(layer.Tiles);
        Tool(editor, "Paint").PerformClick();
        ClickCell(preview, layer, 2, 3);
        var tile = Assert.Single(layer.Tiles);
        Assert.Equal((2, 3), (tile.X, tile.Y));
        Assert.Equal(("terrain", "detail", 1, 1), (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile));
        ClickCell(preview, layer, 2, 3); // No additional operation.
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Z));
        Application.DoEvents();
        Assert.Empty(layer.Tiles);
        Assert.False(document.IsDirty);
        Assert.Equal(before, SceneDefinitionSerializer.ToJson(document.Definition));
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Y));
        Application.DoEvents();
        tile = Assert.Single(layer.Tiles);
        tile.Id = Guid.NewGuid(); tile.Nickname = "authored"; tile.Transform = TileTransform.Rotate90;
        tile.EnableFog = true; tile.Visible = false; tile.CollisionProfileName = "solid";
        tile.CollisionTypeByFrame = true; tile.AdjustCollisionAreaByFrame = true;
        string metadata = JsonConvert.SerializeObject(tile);
        SelectPaintFrame(source, 0, 0);
        ClickCell(preview, layer, 2, 3);
        Assert.Equal(TileTransform.Rotate90, tile.Transform);
        Assert.Equal("solid", tile.CollisionProfileName);
        document.History.Undo(); Application.DoEvents();
        Assert.Equal(metadata, JsonConvert.SerializeObject(tile));
        Tool(editor, "Erase").PerformClick();
        ClickCell(preview, layer, 2, 3);
        Assert.Empty(layer.Tiles);
        ClickCell(preview, layer, 2, 3);
        document.History.Undo(); Application.DoEvents();
        Assert.Same(tile, Assert.Single(layer.Tiles));
        Assert.Equal(metadata, JsonConvert.SerializeObject(tile));
        document.History.Redo(); Application.DoEvents(); Assert.Empty(layer.Tiles);
        document.History.Undo(); Application.DoEvents();
        document.Save(Path.Combine(root, "scene.gscn"), allowInvalid: true);
        using var reloaded = SceneDocument.Open(document.FilePath!);
        Assert.Equal(metadata, JsonConvert.SerializeObject(Assert.Single(reloaded.Definition.Layers[0].Tiles)));
    });

    [Fact]
    public void PaintRejectsMissingSourcesAndUsesLastSelectedPane() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        document.Save(Path.Combine(root, "scene.gscn"));
        Tool(editor, "Paint").PerformClick();
        ClickCell(preview, layer, 1, 1);
        Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
        SelectPaintFrame(source);
        source.SelectedNode = source.Nodes[0]; // A root is not a frame.
        ClickCell(preview, layer, 1, 1);
        Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
        string gani = Path.Combine(root, "flow.gani");
        AnimationDefinitionSerializer.Save(gani, new AnimationDefinition
        {
            Key = "water.flow",
            ThrottleTime = .1,
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("terrain", "terrain.gts")],
            Frames = [new() { Tilesheet = "terrain", RegionName = "detail", XTile = 0, YTile = 1 }]
        });
        editor.AddAnimationSources([gani]); Application.DoEvents();
        SelectPaintFrame(source);
        var animations = Descendants(editor).OfType<ListView>().Single();
        animations.Items[0].Selected = true;
        animations.Focus();
        document.Save(Path.Combine(root, "scene.gscn"));
        Assert.False(document.IsDirty);
        ClickCell(preview, layer, 1, 1);
        var tile = Assert.Single(layer.Tiles);
        Assert.Equal("water.flow", tile.AnimationKey);
        Assert.True(tile.EnableAnimator); Assert.True(tile.StartAnimation);
        Assert.Equal(("terrain", "detail", 0, 1), (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile));
        ClickCell(preview, layer, 1, 1);
        document.History.Undo(); Application.DoEvents();
        Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
        source.Focus(); // Reusing the still-selected frame explicitly changes the source kind.
        ClickCell(preview, layer, 1, 1);
        tile = Assert.Single(layer.Tiles);
        Assert.Null(tile.AnimationKey); Assert.Equal(1, tile.Frame!.XTile);
        Assert.Single(document.Definition.TilesheetSources);
        Assert.Single(document.Definition.AnimationSources);
    });

    [Fact]
    public void EditingRequiresVisibleOwnedSelectedLayerAndLeftClick() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        SelectPaintFrame(source);
        document.Save(Path.Combine(root, "scene.gscn"));
        Tool(editor, "Paint").PerformClick();
        foreach (var selected in new SceneLayerDefinition?[] { null, new() })
        {
            preview.SetSelection(selected, 0, 0);
            ClickPreview(preview, new(100, 100));
            Assert.Empty(layer.Tiles);
            Assert.False(document.IsDirty);
        }
        preview.SetSelection(layer, 0, 0);
        typeof(ScenePreviewControl).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(preview, [new MouseEventArgs(MouseButtons.Right, 1, 100, 100, 0)]);
        typeof(ScenePreviewControl).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, 100, 100, 0)]);
        Assert.Empty(layer.Tiles); Assert.False(document.IsDirty);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveAsDoesNotSwitchPaintSourceDuringSelectionRestoration(bool animationLast) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            string gani = Path.Combine(root, "flow.gani");
            AnimationDefinitionSerializer.Save(gani, new AnimationDefinition
            {
                Key = "water.flow",
                ThrottleTime = .1,
                Frames = [new() { Tilesheet = "terrain", RegionName = "detail" }]
            });
            editor.AddAnimationSources([gani]); Application.DoEvents();
            var animations = Descendants(editor).OfType<ListView>().Single();
            animations.Items[0].Selected = true; animations.Focus();
            SelectPaintFrame(source);
            if (animationLast) animations.Focus();
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            string directory = Path.Combine(root, "saved");
            Directory.CreateDirectory(directory);
            document.Save(Path.Combine(directory, "scene.gscn"));
            Application.DoEvents();
            Assert.Single(animations.SelectedItems);
            Assert.False(document.IsDirty);
            Tool(editor, "Paint").PerformClick();
            ClickCell(preview, layer, 1, 1);
            if (animationLast)
                Assert.Equal("water.flow", Assert.Single(layer.Tiles).AnimationKey);
            else
            {
                // Frame selection was reduced to its root during rebuild, so it must be unavailable.
                Assert.Empty(layer.Tiles);
                Assert.False(document.IsDirty);
                Assert.Contains(Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.OfType<ToolStripLabel>()),
                    label => label.Text?.StartsWith("Paint unavailable", StringComparison.Ordinal) == true);
            }
        });

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PaintingRejectsUnnamedGtsRegions(string regionName) => WithPaintingEditor((editor, preview, source, root) =>
    {
        string path = Path.Combine(root, "terrain.gts");
        var definition = TilesheetDefinitionSerializer.Load(path);
        definition.Regions[0].Name = regionName;
        // Imported files can deserialize without meeting the serializer's save validation rules.
        File.WriteAllText(path, JsonConvert.SerializeObject(definition));
        editor.ReloadSources();
        SelectPaintFrame(source);
        var document = editor.Document;
        document.Save(Path.Combine(root, "scene.gscn")); Application.DoEvents();
        string? undo = document.History.UndoDescription;
        Tool(editor, "Paint").PerformClick();
        ClickCell(preview, document.Definition.Layers[0], 1, 1);
        Assert.Empty(document.Definition.Layers[0].Tiles);
        Assert.False(document.IsDirty);
        Assert.Equal(undo, document.History.UndoDescription);
    });

    [Theory]
    [InlineData(-1, .1, "detail")]
    [InlineData(0, -1, "detail")]
    [InlineData(0, .1, "")]
    public void PaintingRejectsStructurallyInvalidGani(int firstFrameX, double throttle, string secondRegion) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            string path = Path.Combine(root, "invalid.gani");
            File.WriteAllText(path, JsonConvert.SerializeObject(new AnimationDefinition
            {
                Key = "invalid",
                ThrottleTime = throttle,
                Frames = [new() { Tilesheet = "terrain", RegionName = "detail", XTile = firstFrameX },
                    new() { Tilesheet = "terrain", RegionName = secondRegion }]
            }));
            editor.AddAnimationSources([path]); Application.DoEvents();
            var animations = Descendants(editor).OfType<ListView>().Single();
            animations.Items[0].Selected = true; animations.Focus();
            var document = editor.Document;
            document.Save(Path.Combine(root, "scene.gscn")); Application.DoEvents();
            string? undo = document.History.UndoDescription;
            Tool(editor, "Paint").PerformClick();
            ClickCell(preview, document.Definition.Layers[0], 1, 1);
            Assert.Empty(document.Definition.Layers[0].Tiles);
            Assert.False(document.IsDirty);
            Assert.Equal(undo, document.History.UndoDescription);
        });

    [Fact]
    public void GtsPaintingPreservesExistingAnimationAssignmentSemantics() => WithPaintingEditor((editor, preview, source, root) =>
    {
        var document = editor.Document;
        var layer = document.Definition.Layers[0];
        document.EditTile(layer, 1, 1, "Seed animated tile", tile =>
        {
            tile.AnimationKey = "water.flow"; tile.EnableAnimator = true; tile.StartAnimation = true;
            tile.Transform = TileTransform.Rotate90; tile.Nickname = "authored";
        });
        var original = Assert.Single(layer.Tiles);
        string before = JsonConvert.SerializeObject(original);
        SelectPaintFrame(source);
        Tool(editor, "Paint").PerformClick();
        ClickCell(preview, layer, 1, 1);
        Assert.Equal("water.flow", original.AnimationKey);
        Assert.True(original.EnableAnimator); Assert.True(original.StartAnimation);
        Assert.Equal(TileTransform.Rotate90, original.Transform); Assert.Equal("authored", original.Nickname);
        Assert.Equal(("terrain", "detail", 1, 1),
            (original.Frame!.Tilesheet, original.Frame.RegionName, original.Frame.XTile, original.Frame.YTile));
        document.History.Undo(); Application.DoEvents();
        Assert.Equal(before, JsonConvert.SerializeObject(original));
    });

    public static IEnumerable<object[]> PaintingProjections =>
        from projection in Enum.GetValues<CoordinateSystemTypes>()
        from zoom in new[] { .5f, 1f, 2f }
        select new object[] { projection, zoom };

    [Theory]
    [MemberData(nameof(PaintingProjections))]
    public void PointerPaintingUsesSelectedProjectionZoomAndScroll(CoordinateSystemTypes projection, float zoom) =>
        WithPaintingEditor((editor, preview, source, root) =>
        {
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            layer.CoordinateSystemType = projection;
            layer.Columns = 80; layer.Rows = 80; layer.OriginPx = new(37, -19);
            var upper = document.AddLayer(); upper.ZOrder = 100;
            var other = document.AddDefinition(); var otherLayer = document.AddLayer();
            editor.SelectDefinition(document.Definitions[0]); Application.DoEvents();
            var structure = Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes[0].Tag is SceneDefinition);
            structure.SelectedNode = structure.Nodes[0].Nodes.Cast<TreeNode>().Single(node => ReferenceEquals(node.Tag, layer));
            SelectPaintFrame(source);
            document.Save(Path.Combine(root, "scene.gscn"));
            Tool(editor, "Paint").PerformClick();
            preview.SetZoom(zoom);
            preview.AutoScrollPosition = new(100, 75);
            ClickCell(preview, layer, 5, 6);
            var tile = Assert.Single(layer.Tiles);
            Assert.Equal((5, 6), (tile.X, tile.Y));
            Assert.Empty(upper.Tiles); Assert.Empty(otherLayer.Tiles);
            string content = SceneDefinitionSerializer.ToJson(document.Definition);
            ClickPreview(preview, new(-10000, -10000));
            Assert.Equal(content, SceneDefinitionSerializer.ToJson(document.Definition));
            structure.SelectedNode!.Checked = false;
            ClickPreview(preview, new(100, 100));
            Assert.Equal(content, SceneDefinitionSerializer.ToJson(document.Definition));
            Assert.Empty(upper.Tiles);
            editor.SelectDefinition(other); Application.DoEvents();
            ClickCell(preview, otherLayer, 1, 1); // Source selection was cleared on member change.
            Assert.Empty(otherLayer.Tiles);
            Assert.Single(layer.Tiles);
        });

    private static ToolStripButton Tool(SceneEditorControl editor, string name) =>
        Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.OfType<ToolStripButton>()).Single(item => item.Text == name);

    private static void SelectPaintFrame(TreeView tree, int x = 1, int y = 1)
    {
        var region = tree.Nodes[0].Nodes[0]; region.Expand();
        var row = region.Nodes[y]; row.Expand();
        tree.SelectedNode = row.Nodes[x];
        tree.Focus();
    }

    private static void ClickCell(ScenePreviewControl preview, SceneLayerDefinition layer, int x, int y) =>
        ClickPreview(preview, PreviewCellPoint(preview, layer, x, y));

    private static Point PreviewCellPoint(ScenePreviewControl preview, SceneLayerDefinition layer, int x, int y)
    {
        var projection = (SceneLayer)typeof(ScenePreviewControl).GetMethod("GetProjection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(preview, [layer])!;
        var outline = projection[x, y]!.OutlinePointsWorld;
        // Affine projections address continuous grid cells; hex projections use polygon picking.
        var world = layer.CoordinateSystemType is CoordinateSystemTypes.HexAxialFlatTop or CoordinateSystemTypes.HexAxialPointedTop
            ? new PointF((float)outline.Average(p => p.X), (float)outline.Average(p => p.Y))
            : projection.GridToWorldPx(new PointF(x + .5f, y + .5f));
        // Repaint updates the viewport offset exactly as in the displayed control.
        using var bitmap = new Bitmap(preview.Width, preview.Height);
        preview.DrawToBitmap(bitmap, preview.ClientRectangle);
        var offset = (PointF)typeof(ScenePreviewControl).GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(preview)!;
        return new((int)Math.Round(world.X * preview.Zoom + offset.X), (int)Math.Round(world.Y * preview.Zoom + offset.Y));
    }

    private static void ClickPreview(ScenePreviewControl preview, Point point)
    {
        foreach (var method in new[] { "OnMouseDown", "OnMouseUp" })
            typeof(ScenePreviewControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
    }

    private static void WithPaintingEditor(Action<SceneEditorControl, ScenePreviewControl, TreeView, string> test) => RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "GscnPainting-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using (var bitmap = new Bitmap(32, 32)) bitmap.Save(Path.Combine(root, "atlas.png"));
            TilesheetDefinitionSerializer.Save(Path.Combine(root, "terrain.gts"), new TilesheetDefinition
            {
                Name = "terrain",
                Image = new() { FilePath = "atlas.png" },
                Regions = [new() { Name = "detail", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
            });
            using var document = SceneDocument.Create(root);
            document.AddLayer();
            using var editor = new SceneEditorControl(document);
            using var host = new Form { Size = new(1500, 950), Opacity = 0, ShowInTaskbar = false };
            host.Controls.Add(editor); host.Show(); Application.DoEvents();
            editor.AddTilesheetSources([Path.Combine(root, "terrain.gts")]); Application.DoEvents();
            var preview = Descendants(editor).OfType<ScenePreviewControl>().Single();
            var source = Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes.Count > 0 && tree.Nodes[0].Tag is SceneTilesheetSource);
            test(editor, preview, source, root);
        }
        finally { Directory.Delete(root, recursive: true); }
    });
}
