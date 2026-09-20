using System.Runtime.ExceptionServices;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed class SceneEditorTests
{
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
