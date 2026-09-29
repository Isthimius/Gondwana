using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Scenes.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed class SceneEditorTests
{
    [Fact]
    public void TransformPropertiesAndToolbarComposeAndClearSparseTile() => RunSta(() =>
    {
        var document = SceneDocument.Create(Path.GetTempPath());
        var layer = document.AddLayer();
        using var editor = new SceneEditorControl(document);
        using var host = new Form { Size = new(1500, 950), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(editor);
        host.Show();
        Application.DoEvents();
        var adapter = Descendants(editor).OfType<PropertyGrid>().Select(grid => grid.SelectedObject).OfType<TilePropertyAdapter>().Single();
        Assert.Equal(TileTransform.Identity, adapter.Transform);
        Assert.Empty(layer.Tiles);
        var buttons = Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>()).ToArray();
        var right = buttons.Single(item => item.Text == "Rotate right 90°");
        right.PerformClick();
        Assert.Equal(TileTransform.Rotate90, layer.Tiles.Single().Transform);
        right.PerformClick();
        Assert.Equal(TileTransform.Rotate180, layer.Tiles.Single().Transform);
        buttons.Single(item => item.Text == "Flip horizontal").PerformClick();
        Assert.Equal(TileTransform.FlipVertical, layer.Tiles.Single().Transform);
        var property = System.ComponentModel.TypeDescriptor.GetProperties(adapter)["Transform"]!;
        Assert.Equal("Appearance", property.Category);
        Assert.Equal("Rotate 90°", property.Converter.ConvertToString(TileTransform.Rotate90));
        Assert.Equal(TileTransform.Rotate90, property.Converter.ConvertFromString("Rotate 90°"));
        buttons.Single(item => item.Text == "Clear tile").PerformClick();
        Assert.Empty(layer.Tiles);
    });

    [Fact]
    public void EffectivePropertiesFollowFrameOverridesWithoutChangingSource()
    {
        var document = SceneDocument.Create(Path.GetTempPath());
        var layer = document.AddLayer();
        var region = new TilesheetRegionDefinition
        {
            TileSize = new(40, 20), TilePadding = new(1, 2, 3, 4), Overhang = new(5, 6, 7, 8),
            CollisionAdjust = new(1, 2, 3, 4),
            Frames = [new() { XTile = 1, YTile = 0, CollisionAdjust = new(2, 4, 6, 8) }]
        };
        var frame = new SceneFrameDefinition { XTile = 1 };
        var adapter = new TilePropertyAdapter(document, layer, 0, 0, () => { }, () => (region, frame));
        adapter.Transform = TileTransform.Rotate90;
        adapter.AdjustCollisionAreaByFrame = true;
        Assert.Equal(new Size(20, 40), adapter.EffectiveTileSize);
        Assert.Equal(new Spacing(4, 1, 2, 3), adapter.EffectiveTilePadding);
        Assert.Equal(new Spacing(8, 5, 6, 7), adapter.EffectiveOverhang);
        Assert.Equal(new Gondwana.Physics.Collisions.CollisionAdjust(6, 8, 4, 2), adapter.EffectiveCollisionAdjust);
        Assert.Equal(new Spacing(1, 2, 3, 4), region.TilePadding);
    }
    [Theory]
    [InlineData(TileTransform.Identity, 0, 1, 2, 3)]
    [InlineData(TileTransform.Rotate90, 2, 0, 3, 1)]
    [InlineData(TileTransform.Rotate180, 3, 2, 1, 0)]
    [InlineData(TileTransform.Rotate270, 1, 3, 0, 2)]
    [InlineData(TileTransform.FlipHorizontal, 1, 0, 3, 2)]
    [InlineData(TileTransform.FlipVertical, 2, 3, 0, 1)]
    [InlineData(TileTransform.FlipDiagonal, 0, 2, 1, 3)]
    [InlineData(TileTransform.FlipAntiDiagonal, 3, 1, 2, 0)]
    public void PreviewOrientsArtworkWithoutChangingSource(TileTransform transform, int tl, int tr, int bl, int br) => RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), "TilePreview-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            Color[] colors = [Color.Red, Color.Lime, Color.Blue, Color.Yellow];
            using (var bitmap = new Bitmap(40, 20))
            {
                for (int y = 0; y < 20; y++) for (int x = 0; x < 40; x++) bitmap.SetPixel(x, y, colors[(x >= 20 ? 1 : 0) + (y >= 10 ? 2 : 0)]);
                bitmap.Save(Path.Combine(root, "atlas.png"));
            }
            var gts = new TilesheetDefinition
            {
                Name = "preview", Image = new() { FilePath = "atlas.png" },
                Regions = [new() { Name = "default", Area = new(0, 0, 40, 20), TileSize = new(40, 20) }]
            };
            string path = Path.Combine(root, "atlas.gts");
            TilesheetDefinitionSerializer.Save(path, gts);
            using var source = SceneTilesheetSource.Load(path);
            var definition = new SceneDefinition
            {
                Layers = [new() { Columns = 1, Rows = 1, TileWidth = 40, TileHeight = 20,
                    Tiles = [new() { Transform = transform, Frame = new() { Tilesheet = "preview", RegionName = "default" } }] }]
            };
            using var preview = new ScenePreviewControl { Size = new(240, 240), ShowGridLines = false };
            preview.Configure(definition, _ => source, _ => null);
            preview.SetZoom(2);
            using var output = new Bitmap(240, 240);
            preview.DrawToBitmap(output, new(0, 0, 240, 240));
            var hits = new List<Point>();
            var argb = colors.Select(color => color.ToArgb()).ToHashSet();
            for (int y = 0; y < 240; y++) for (int x = 0; x < 240; x++) if (argb.Contains(output.GetPixel(x, y).ToArgb())) hits.Add(new(x, y));
            Assert.NotEmpty(hits);
            int minX = hits.Min(p => p.X), maxX = hits.Max(p => p.X), minY = hits.Min(p => p.Y), maxY = hits.Max(p => p.Y);
            int x1 = minX + (maxX - minX) / 4, x2 = minX + 3 * (maxX - minX) / 4;
            int y1 = minY + (maxY - minY) / 4, y2 = minY + 3 * (maxY - minY) / 4;
            Assert.Equal(colors[tl].ToArgb(), output.GetPixel(x1, y1).ToArgb());
            Assert.Equal(colors[tr].ToArgb(), output.GetPixel(x2, y1).ToArgb());
            Assert.Equal(colors[bl].ToArgb(), output.GetPixel(x1, y2).ToArgb());
            Assert.Equal(colors[br].ToArgb(), output.GetPixel(x2, y2).ToArgb());
            Assert.Equal(Color.Red.ToArgb(), source.Image!.GetPixel(5, 5).ToArgb());
            Assert.Equal(Color.Yellow.ToArgb(), source.Image.GetPixel(35, 15).ToArgb());
        }
        finally { Directory.Delete(root, true); }
    });
    [Fact]
    public void DocumentSaveAsRebasesLooseGtsAndGaniReferences()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "GondwanaGscnEditor-" + Guid.NewGuid());
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        string dependencies = Path.Combine(root, "deps");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Directory.CreateDirectory(dependencies);

        try
        {
            string gts = Path.Combine(dependencies, "terrain.gts");
            string gani = Path.Combine(dependencies, "water.gani");
            File.WriteAllText(gts, "{}");
            File.WriteAllText(gani, "{}");

            var document = SceneDocument.Create(first);
            document.SetLooseTilesheetSource("terrain", gts);
            document.SetLooseAnimationSource("water", gani);

            string firstPath = Path.Combine(first, "scene.gscn");
            document.Save(firstPath);

            var firstGts = Assert.Single(document.Definition.TilesheetSources).GtsPath!;
            var firstGani = Assert.Single(document.Definition.AnimationSources).GaniPath!;
            Assert.False(Path.IsPathRooted(firstGts));
            Assert.False(Path.IsPathRooted(firstGani));
            Assert.Equal(Path.GetFullPath(gts), Path.GetFullPath(firstGts, first));
            Assert.Equal(Path.GetFullPath(gani), Path.GetFullPath(firstGani, first));

            string secondPath = Path.Combine(second, "copy.gscn");
            document.Save(secondPath);

            var secondGts = Assert.Single(document.Definition.TilesheetSources).GtsPath!;
            var secondGani = Assert.Single(document.Definition.AnimationSources).GaniPath!;
            Assert.False(Path.IsPathRooted(secondGts));
            Assert.False(Path.IsPathRooted(secondGani));
            Assert.Equal(Path.GetFullPath(gts), Path.GetFullPath(secondGts, second));
            Assert.Equal(Path.GetFullPath(gani), Path.GetFullPath(secondGani, second));
            Assert.Equal(Path.GetFullPath(secondPath), document.FilePath);
            Assert.False(document.IsDirty);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EditorUsesLocalDockingAndCreatesSparseTileOnlyWhenEdited() =>
        RunSta(() =>
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "GondwanaGscnEditor-" + Guid.NewGuid());
            Directory.CreateDirectory(root);

            try
            {
                var document = SceneDocument.Create(root);
                var layer = document.AddLayer();

                using var editor = new SceneEditorControl(document);
                using var host = new Form
                {
                    Size = new Size(1500, 950),
                    Opacity = 0,
                    ShowInTaskbar = false
                };
                host.Controls.Add(editor);
                host.Show();
                Application.DoEvents();

                var dock = Assert.Single(
                    Descendants(editor).OfType<DockPanel>());
                var panes = dock.Contents.Cast<DockContent>().ToArray();

                Assert.Equal(
                    [
                        "GANI animations",
                        "GTS frame sources",
                        "Properties",
                        "Scene preview",
                        "Scene structure",
                        "Tile properties",
                        "Validation"
                    ],
                    panes.Select(pane => pane.Text).Order().ToArray());

                Assert.All(panes, pane =>
                {
                    Assert.Same(dock, pane.DockPanel);
                    Assert.True(pane.HideOnClose);
                    Assert.False(
                        pane.DockHandler.IsDockStateValid(
                            DockState.Float));
                });

                Assert.Equal(
                    panes.Select(pane => pane.Text).Order(),
                    SceneEditorControl.PaneNames.Order());

                var previewPane = panes.Single(
                    pane => pane.Text == "Scene preview");
                var previewPaneBeforeClose = previewPane.Pane;

                var previewControl = Descendants(previewPane)
                    .OfType<ScenePreviewControl>()
                    .Single();

                var previewToolbar = Descendants(previewPane)
                    .OfType<ToolStrip>()
                    .Single();

                Assert.Contains(
                    previewToolbar.Items.Cast<ToolStripItem>(),
                    item => item.Text == "−");
                Assert.Contains(
                    previewToolbar.Items.Cast<ToolStripItem>(),
                    item => item.Text == "+");

                var gridToggle = Assert.IsType<ToolStripButton>(
                    previewToolbar.Items
                        .Cast<ToolStripItem>()
                        .Single(item => item.Text == "Grid"));

                Assert.True(gridToggle.Checked);
                Assert.True(previewControl.ShowGridLines);

                gridToggle.PerformClick();
                Assert.False(gridToggle.Checked);
                Assert.False(previewControl.ShowGridLines);

                previewControl.SetZoom(1f);
                Assert.Equal(1f, previewControl.Zoom, 3);

                previewControl.ZoomIn();
                Assert.Equal(1.25f, previewControl.Zoom, 3);

                previewControl.ZoomOut();
                Assert.Equal(1f, previewControl.Zoom, 3);

                Assert.True(
                    previewControl.ZoomWithMouseWheel(
                        new Point(10, 10),
                        120,
                        controlPressed: true));
                Assert.Equal(1.25f, previewControl.Zoom, 3);

                previewControl.SetZoom(1f);

                previewPane.Activate();
                previewPane.Pane.CloseActiveContent();
                Application.DoEvents();

                Assert.True(previewPane.IsHidden);
                Assert.False(editor.IsPaneVisible("Scene preview"));

                Assert.True(editor.ShowPane("Scene preview"));
                Application.DoEvents();

                Assert.False(previewPane.IsHidden);
                Assert.True(editor.IsPaneVisible("Scene preview"));
                Assert.Same(previewPaneBeforeClose, previewPane.Pane);
                Assert.False(editor.ShowPane("Not a real pane"));

                var structurePane = panes.Single(
                    pane => pane.Text == "Scene structure");
                structurePane.Activate();
                structurePane.Pane.CloseActiveContent();
                Application.DoEvents();
                Assert.True(structurePane.IsHidden);

                editor.ShowAllPanes();
                Application.DoEvents();
                Assert.All(panes, pane => Assert.False(pane.IsHidden));

                layer.Columns = 100;
                layer.Rows = 100;
                editor.UpdateValidation();
                previewControl.Configure(
                    document.Definition,
                    _ => null,
                    _ => null);
                Application.DoEvents();

                Assert.True(
                    previewControl.AutoScrollMinSize.Width >
                    previewControl.ClientSize.Width);
                Assert.True(
                    previewControl.AutoScrollMinSize.Height >
                    previewControl.ClientSize.Height);

                Assert.Empty(layer.Tiles);

                var tileProperties = Descendants(editor)
                    .OfType<PropertyGrid>()
                    .First(grid =>
                        grid.SelectedObject is not null &&
                        grid.SelectedObject.GetType().Name ==
                        "TilePropertyAdapter");

                var visible = tileProperties.SelectedObject!
                    .GetType()
                    .GetProperty("Visible")!;

                visible.SetValue(tileProperties.SelectedObject, false);
                Application.DoEvents();

                var tile = Assert.Single(layer.Tiles);
                Assert.Equal(0, tile.X);
                Assert.Equal(0, tile.Y);
                Assert.False(tile.Visible);

                var validation = panes.Single(
                    pane => pane.Text == "Validation");
                var preview = panes.Single(
                    pane => pane.Text == "Scene preview");

                validation.Show(
                    preview.Pane,
                    DockAlignment.Right,
                    .25);
                Application.DoEvents();

                Assert.Same(dock, validation.DockPanel);
                Assert.Empty(editor.UpdateValidation());
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        });

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void RunSta(Action test)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(
                    UnhandledExceptionMode.ThrowException,
                    threadScope: true);
                test();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(60)),
            "GSCN WinForms test timed out.");

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
