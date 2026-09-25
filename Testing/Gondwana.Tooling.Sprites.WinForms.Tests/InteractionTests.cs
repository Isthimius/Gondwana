using System.Reflection;
using System.Runtime.ExceptionServices;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Sprites.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

public sealed class InteractionTests
{
    [Fact]
    public void SourceTreesAssignSelectedEntryAndPreviewRespondsToProperties() => Sta(directory =>
    {
        var imagePath = Path.Combine(directory, "actors.png");
        using (var bitmap = new Bitmap(32, 16))
        {
            using var graphics = Graphics.FromImage(bitmap); graphics.Clear(Color.Red);
            bitmap.Save(imagePath);
        }
        var gts = Path.Combine(directory, "actors.gts");
        TilesheetDefinitionSerializer.Save(gts, new TilesheetDefinition
        {
            Name = "actors",
            Image = new() { FilePath = "actors.png" },
            Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 32, 16), TileSize = new Size(16, 16) }]
        });
        var gscn = Path.Combine(directory, "level.gscn");
        SceneDefinitionSerializer.Save(gscn, new SceneDefinition() { ID = "level", Layers = [new() { ID = "actors-layer", Columns = 1, Rows = 1, TileWidth = 16, TileHeight = 24 }] });
        var document = SpriteDocument.Create(directory);
        var entry = document.AddSprite();
        using var host = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(1200, 800) };
        using var editor = new SpriteEditorControl(document);
        host.Controls.Add(editor); host.Show(); Application.DoEvents();
        editor.AddTilesheetSources([gts]); editor.AddSceneSources([gscn]); editor.SelectSprite(entry);
        var trees = Descendants(editor).OfType<TreeView>().ToArray();
        void DoubleClick(TreeView tree, TreeNode node) => typeof(TreeView).GetMethod("OnNodeMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(tree, [new TreeNodeMouseClickEventArgs(node, MouseButtons.Left, 2, 0, 0)]);
        var sceneTree = trees.Single(tree => tree.Nodes[0].Text == "level");
        DoubleClick(sceneTree, sceneTree.Nodes[0].Nodes[0]);
        Assert.Equal("level", entry.SceneId); Assert.Equal("actors-layer", entry.SceneLayerId);
        var frameTree = trees.Single(tree => tree.Nodes[0].Text == "actors");
        DoubleClick(frameTree, frameTree.Nodes[0].Nodes[0].Nodes[1]);
        Assert.Equal(1, entry.Frame!.XTile);
        Assert.Equal("actors", entry.Frame.Tilesheet);
        var grid = Descendants(editor).OfType<PropertyGrid>().Single();
        var properties = Assert.IsType<SpriteProperties>(grid.SelectedObject);
        properties.NudgeX = 9; properties.CollisionLeft = 2;
        Assert.Equal(9, entry.NudgeX); Assert.Equal(2, entry.AdjustCollisionArea.Left);
        var preview = Descendants(editor).OfType<SpritePreviewControl>().Single();
        var canvas = preview.Controls.OfType<UserControl>().Single();
        using var before = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(before, canvas.ClientRectangle);
        entry.Visible = false; editor.SelectSprite(entry);
        using var after = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(after, canvas.ClientRectangle);
        Point? FirstRedPixel(Bitmap bitmap)
        {
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.R > 200 && pixel.G < 30 && pixel.B < 30) return new Point(x, y);
                }
            return null;
        }
        Assert.NotNull(FirstRedPixel(before));
        Assert.Null(FirstRedPixel(after));
        entry.Visible = true;
        entry.Position = new PointF(3, 2);
        editor.SelectSprite(entry);
        using var moved = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(moved, canvas.ClientRectangle);
        Assert.NotNull(FirstRedPixel(moved));
        Assert.NotEqual(FirstRedPixel(before), FirstRedPixel(moved));
        var zoom = Descendants(preview).OfType<ComboBox>().Single();
        zoom.SelectedIndex = 0;
        Assert.Equal(.25f, preview.Zoom);
        Descendants(preview).OfType<Button>().Single(button => button.Text == "+").PerformClick();
        Assert.Equal(.3125f, preview.Zoom);
        Descendants(preview).OfType<Button>().Single(button => button.Text == "−").PerformClick();
        Assert.Equal(.25f, preview.Zoom);
        zoom.SelectedIndex = 5;
        Assert.True(preview.Zoom > 0);
        Assert.Empty(editor.UpdateValidation());
    });

    [Fact]
    public void CollectionEditingSaveAsAndPreviewDoNotPolluteRuntime() => Sta(directory =>
    {
        var sprites = SpriteManager.Instance.AllSprites.ToArray();
        var scenes = Scene.GetAllScenes().ToArray();
        var sheets = TilesheetRegistry.Instance.GetAll().ToArray();
        var document = SpriteDocument.Create(directory);
        using var host = new Form { ShowInTaskbar = false, Opacity = 0, Width = 1200, Height = 800 };
        using var editor = new SpriteEditorControl(document);
        host.Controls.Add(editor); host.Show(); Application.DoEvents();
        var first = document.AddSprite();
        var second = document.DuplicateSprite(first);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Nickname, second.Nickname);
        editor.SelectSprite(second);
        Assert.Same(second, editor.SelectedSprite);
        Assert.True(document.IsDirty);
        Assert.NotEmpty(editor.UpdateValidation());
        var path = Path.Combine(directory, "actors.gspr");
        Assert.Throws<InvalidDataException>(() => document.Save(path));
        foreach (var entry in document.Definition.Sprites) { entry.SceneId = "level"; entry.SceneLayerId = "actors"; }
        var sourcePath = Path.Combine(directory, "level.gscn");
        SceneDefinitionSerializer.Save(sourcePath, new SceneDefinition { ID = "level", Layers = [new() { ID = "actors", Columns = 1, Rows = 1, TileWidth = 16, TileHeight = 24 }] });
        editor.AddSceneSources([sourcePath]);
        editor.SelectSprite(first);
        Assert.Empty(editor.UpdateValidation());
        document.Save(path);
        Assert.False(document.IsDirty);
        var moved = Path.Combine(directory, "moved", "actors.gspr");
        document.Save(moved);
        var reopened = SpriteDocument.Open(moved);
        Assert.Equal(2, reopened.Definition.Sprites.Count);
        Assert.Equal(sourcePath, reopened.ResolveReferencePath(Assert.Single(reopened.Definition.SceneSources).GscnPath!));
        Assert.True(document.RemoveSprite(second));
        Assert.Single(document.Definition.Sprites);
        Assert.Equal(sprites, SpriteManager.Instance.AllSprites);
        Assert.Equal(scenes, Scene.GetAllScenes());
        Assert.Equal(sheets, TilesheetRegistry.Instance.GetAll());
    });

    [Fact]
    public void DockPersistenceRecoveryIsolationAndDisposal() => Sta(directory =>
    {
        (Form Host, SpriteEditorControl Editor, DockPanel Dock) Open()
        {
            var host = new Form { Opacity = 0, ShowInTaskbar = false, Size = new Size(1200, 800) };
            var editor = new SpriteEditorControl(SpriteDocument.Create(directory));
            host.Controls.Add(editor); host.Show(); Application.DoEvents();
            return (host, editor, Descendants(editor).OfType<DockPanel>().Single());
        }
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "standalone-gspr");
        var first = Open();
        var panes = first.Dock.Contents.OfType<DockContent>().ToArray();
        Assert.Equal(6, panes.Length);
        Assert.All(panes, pane => { Assert.Same(first.Dock, pane.DockPanel); Assert.Equal(DockAreas.Document, pane.DockAreas); });
        panes[1].Show(panes[0].Pane, DockAlignment.Right, .62);
        panes.Single(pane => pane.Text == "Validation").Hide();
        first.Host.Dispose();
        Assert.All(panes, pane => Assert.True(pane.IsDisposed));
        var second = Open();
        Assert.False(second.Editor.IsPaneVisible("Validation"));
        Assert.True(second.Editor.ShowPane("Validation"));
        second.Editor.ShowAllPanes();
        second.Dock.Contents.OfType<DockContent>().Single(pane => pane.Text == "Validation").Hide();
        second.Host.Dispose();
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "studio");
        var studio = Open(); Assert.True(studio.Editor.IsPaneVisible("Validation")); studio.Host.Dispose();
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "standalone-gspr");
        var third = Open(); Assert.False(third.Editor.IsPaneVisible("Validation")); third.Editor.ResetLayout(); third.Host.Dispose();
        var fourth = Open(); Assert.True(fourth.Editor.IsPaneVisible("Validation")); fourth.Host.Dispose();
    });

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Sta(Action<string> action)
    {
        Exception? error = null;
        var directory = Path.Combine(Path.GetTempPath(), "GsprTests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var thread = new Thread(() => { try { action(directory); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Directory.Delete(directory, true);
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
