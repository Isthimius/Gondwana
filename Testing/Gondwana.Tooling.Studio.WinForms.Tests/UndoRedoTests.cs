using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData("gts")]
    [InlineData("gscn")]
    public void PackedUndoSurvivesSaveAndSaveAsWithoutChangingDocumentIdentity(string format) => RunSta(root =>
    {
        var archive = NativeArchive(root);
        using var studio = Host(root);
        var child = studio.OpenDocument(NativeLocation(archive, format));
        string? original = child.Document.SelectedMember;
        if (child.Document.Editor is TilesheetEditorControl gts) gts.Document.RenameDefinition("renamed");
        else ((SceneEditorControl)child.Document.Editor).Document.RenameDefinition("renamed");
        Assert.True(AwaitUi(studio.SaveDocument(child)));
        child.Document.History!.Undo(); Assert.Equal(original, child.Document.SelectedMember); Assert.True(child.Document.Dirty());
        child.Document.History.Redo(); Assert.False(child.Document.Dirty());
        string destination = Path.Combine(root, "new", "saved." + format);
        Assert.True(AwaitUi(studio.SaveDocument(child, saveAs: true, destination: destination)));
        child.Document.History.Undo(); Assert.Equal(destination, child.Document.Path()); Assert.Null(child.Document.PackedLocation());
        Assert.Same(child, studio.OpenDocument(destination));
        Assert.Single(studio.Documents);
    });

    [Fact]
    public void UndoMenusTargetOnlyActiveDocumentAndTrackSavedCheckpoint() => RunSta(root =>
    {
        var tiles = Path.Combine(root, "tiles.gts");
        var scene = Path.Combine(root, "scene.gscn");
        File.WriteAllText(tiles, NativeJson("gts")); File.WriteAllText(scene, NativeJson("gscn"));
        using var studio = Host(root);
        var gts = studio.OpenDocument(tiles);
        var first = ((TilesheetEditorControl)gts.Document.Editor).Document;
        first.RenameDefinition("edited tiles");
        var gscn = studio.OpenDocument(scene);
        var second = ((SceneEditorControl)gscn.Document.Editor).Document;
        second.RenameDefinition("edited scene");
        var menu = studio.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "&Edit");
        menu.DropDownItems[0].PerformClick(); Application.DoEvents();
        Assert.False(second.IsDirty); Assert.True(first.IsDirty);
        Assert.DoesNotContain(" *", gscn.Text);
        gts.Activate(); Application.DoEvents();
        Assert.True(menu.DropDownItems[0].Enabled); Assert.False(menu.DropDownItems[1].Enabled);
        menu.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.False(first.IsDirty);
        menu.DropDownItems[1].PerformClick(); Assert.True(first.IsDirty);
        Assert.True(AwaitUi(studio.SaveDocument(gts))); Assert.False(first.IsDirty);
        menu.DropDownItems[0].PerformClick(); Assert.True(first.IsDirty);
        menu.DropDownItems[1].PerformClick(); Assert.False(first.IsDirty);
        Assert.Equal(2, studio.Documents.Count);
    });
}
