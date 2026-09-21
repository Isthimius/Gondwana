using System.Runtime.ExceptionServices;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

public sealed class InteractionTests
{
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
