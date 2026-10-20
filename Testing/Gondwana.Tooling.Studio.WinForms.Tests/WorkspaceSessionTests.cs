extern alias studio;

using System.Collections;
using System.Reflection;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Tilesheets.WinForms;
using WeifenLuo.WinFormsUI.Docking;
using WorkspaceSessionController = studio::Gondwana.Tooling.WinForms.WorkspaceSessionController;
using WorkspaceSessionDocument = studio::Gondwana.Tooling.WinForms.WorkspaceSessionDocument;
using WorkspaceSessionStore = studio::Gondwana.Tooling.WinForms.WorkspaceSessionStore;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void SessionReopensAllSixFormatsAndActiveTabUsingFreshModels() => RunSta(directory =>
    {
        string[] formats = ["gaf", "gts", "gani", "gsnd", "gscn", "gspr"];
        Control[] editors;
        using (var studio = Host(directory))
        {
            foreach (string format in formats)
            {
                var document = studio.NewDocument(format, format == "gaf" ? Path.Combine(directory, "saved.gaf") : null);
                Assert.True(AwaitUi(studio.SaveDocument(document, destination: Path.Combine(directory, "saved." + format))));
            }
            studio.Documents[5].Pane.SetContentIndex(studio.Documents[5], 0);
            studio.Documents[1].Activate();
            Application.DoEvents();
            editors = studio.Documents.Select(document => document.Document.Editor).ToArray();
            studio.Close();
        }
        Assert.All(editors, editor => Assert.True(editor.IsDisposed));
        using var reopened = Host(directory);
        Assert.Equal(new[] { "gspr", "gaf", "gts", "gani", "gsnd", "gscn" }, reopened.Documents.Select(document => document.Document.Extension));
        Assert.Equal("gts", reopened.ActiveDocument!.Document.Extension);
        Assert.All(reopened.Documents, document => Assert.DoesNotContain(document.Document.Editor, editors));
        reopened.RestoreSession();
        Assert.Equal(6, reopened.Documents.Count);
        reopened.Close();
    });

    [Fact]
    public void CancelledExitDoesNotReplaceSessionAndSaveOnExitCapturesFinalPath() => RunSta(directory =>
    {
        var store = new WorkspaceSessionStore(_ => { });
        Assert.True(store.Write(new()));
        string original = File.ReadAllText(store.FilePath);
        using var studio = Host(directory);
        studio.NewDocument("gts");
        studio.AskSave = _ => DialogResult.Cancel;
        studio.Close();
        Assert.False(studio.IsDisposed);
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        studio.AskSave = _ => DialogResult.Yes;
        studio.SavePath = _ => Path.Combine(directory, "saved-on-close.gts");
        studio.Close();
        Assert.Equal(Path.Combine(directory, "saved-on-close.gts"), Assert.Single(store.Read().Documents).Path);
    });

    [Fact]
    public void SessionOptOutAndOneTimeEmptyStartupRemainIndependentOfDockLayout() => RunSta(directory =>
    {
        using (var studio = Host(directory))
        {
            var document = studio.NewDocument("gts");
            Assert.True(AwaitUi(studio.SaveDocument(document, destination: Path.Combine(directory, "saved.gts"))));
            SessionMenu(studio, "Restore workspace on startup").PerformClick();
            studio.Close();
        }
        using (var optedOut = Host(directory))
        {
            Assert.Empty(optedOut.Documents);
            SessionMenu(optedOut, "Restore workspace on startup").PerformClick();
            optedOut.OpenDocument(Path.Combine(directory, "saved.gts"));
            SessionMenu(optedOut, "Start empty on next launch").PerformClick();
            optedOut.Close();
        }
        using (var empty = Host(directory))
        {
            Assert.Empty(empty.Documents);
            Assert.False(new WorkspaceSessionStore(_ => { }).Read().StartEmptyOnce);
            // Disposing without closing must not overwrite the saved session.
        }
        using var restored = Host(directory);
        Assert.Single(restored.Documents);
        restored.Close();
    });

    [Theory]
    [InlineData(typeof(AssetEditorControl), "gaf")]
    [InlineData(typeof(TilesheetEditorControl), "gts")]
    [InlineData(typeof(AnimationEditorControl), "gani")]
    [InlineData(typeof(AudioEditorControl), "gsnd")]
    [InlineData(typeof(SceneEditorControl), "gscn")]
    [InlineData(typeof(SpriteEditorControl), "gspr")]
    public void StandaloneShellsRestoreTheirSupportedFileAndCaptureApprovedClose(Type editorType, string format) => RunSta(directory =>
    {
        string path = Path.Combine(directory, "saved." + format);
        using (var studio = Host(directory))
        {
            var document = studio.NewDocument(format, format == "gaf" ? path : null);
            Assert.True(AwaitUi(studio.SaveDocument(document, destination: path)));
            // No session is committed by Dispose alone.
        }
        var store = new WorkspaceSessionStore(_ => { });
        Assert.True(store.Write(new() { WorkingDirectory = directory, Documents = [WorkspaceSessionDocument.FromPath(path)!], ActiveDocument = path }));
        Type shellType = editorType.Assembly.GetType(editorType.Namespace + ".MainForm", throwOnError: true)!;
        using var shell = (Form)Activator.CreateInstance(shellType, nonPublic: true)!;
        shell.Opacity = 0;
        shell.ShowInTaskbar = false;
        shell.Show();
        Application.DoEvents();
        var documents = ((IEnumerable)shellType.GetField("_documents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(shell)!).Cast<DockContent>().ToArray();
        Assert.Single(documents);
        Assert.NotNull(documents[0].Controls.OfType<UserControl>().SingleOrDefault(control => control.GetType() == editorType));
        shell.Close();
        Assert.True(shell.IsDisposed);
        Assert.Equal(path, Assert.Single(store.Read().Documents).Path);
        Assert.Equal(path, store.Read().ActiveDocument);
    });

    [Fact]
    public void StandaloneAssetCloseHonorsCancelBeforeSavingSession() => RunSta(directory =>
    {
        string path = Path.Combine(directory, "saved.gaf");
        using (var studio = Host(directory))
        {
            var created = studio.NewDocument("gaf", path);
            Assert.True(AwaitUi(studio.SaveDocument(created, destination: path)));
        }
        var store = new WorkspaceSessionStore(_ => { });
        Assert.True(store.Write(new() { Documents = [WorkspaceSessionDocument.FromPath(path)!] }));
        string original = File.ReadAllText(store.FilePath);
        using var shell = new Gondwana.Tooling.Assets.WinForms.MainForm { Opacity = 0, ShowInTaskbar = false };
        shell.Show();
        Application.DoEvents();
        var dock = Descendants(shell).OfType<DockPanel>().First();
        var document = dock.Contents.OfType<DockContent>().Single(content => content.Controls.OfType<AssetEditorControl>().Any());
        var editor = document.Controls.OfType<AssetEditorControl>().Single();
        editor.MarkChanged();
        // The wrapper is internal to the Assets assembly; exercise its real close path.
        var askSave = document.GetType().GetProperty("AskSave", BindingFlags.Instance | BindingFlags.NonPublic)!;
        askSave.SetValue(document, (Func<DialogResult>)(() => DialogResult.Cancel));
        shell.Close();
        Assert.False(shell.IsDisposed);
        Assert.False(document.IsDisposed);
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        askSave.SetValue(document, (Func<DialogResult>)(() => DialogResult.No));
        shell.Close();
        Assert.True(shell.IsDisposed);
    });

    [Fact]
    public void ExplicitEmptyStartupBypassesDirectoryAndDocumentsAndCaptureExcludesUntitled() => RunSta(directory =>
    {
        var store = new WorkspaceSessionStore(_ => { });
        string path = Path.Combine(directory, "saved.gts");
        Assert.True(store.Write(new() { WorkingDirectory = directory, Documents = [WorkspaceSessionDocument.FromPath(path)!] }));
        var controller = new WorkspaceSessionController(startEmpty: true);
        Assert.Equal(Environment.CurrentDirectory, controller.InitialWorkingDirectory(directory));
        controller.Restore(["gts"], _ => Assert.Fail(), _ => Assert.Fail(), _ => Assert.Fail(), _ => Assert.Fail());
        controller.Save(directory, [null, path, path], path);
        Assert.Equal(path, Assert.Single(store.Read().Documents).Path);
    });

    [Fact]
    public void CancelledEncryptedArchiveRestoreContinuesWithOtherDocuments() => RunSta(directory =>
    {
        string archive = Path.Combine(directory, "protected.gaf");
        string tiles = Path.Combine(directory, "saved.gts");
        using (var studio = Host(directory))
        {
            var encrypted = studio.NewDocument("gaf", archive, "secret", encrypt: true);
            Assert.True(AwaitUi(studio.SaveDocument(encrypted, destination: archive)));
            var document = studio.NewDocument("gts");
            Assert.True(AwaitUi(studio.SaveDocument(document, destination: tiles)));
        }
        var store = new WorkspaceSessionStore(_ => { });
        Assert.True(store.Write(new() { Documents = [WorkspaceSessionDocument.FromPath(archive)!, WorkspaceSessionDocument.FromPath(tiles)!] }));
        using var reopened = new MainForm(loadPlugins: false) { Opacity = 0, ShowInTaskbar = false };
        int prompts = 0;
        reopened.AssetPassword = _ => { prompts++; return null; };
        reopened.ReportError = _ => Assert.Fail("Restore errors must be logged, not shown as modal failures.");
        reopened.Show();
        Application.DoEvents();
        Assert.Equal(1, prompts);
        Assert.Equal(tiles, Assert.Single(reopened.Documents).Document.Path());
    });

    private static ToolStripMenuItem SessionMenu(Form shell, string text) =>
        ((ToolStripMenuItem)shell.MainMenuStrip!.Items[0]).DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == text);
}
