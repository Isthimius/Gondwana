using System.Runtime.ExceptionServices;
using Gondwana.Assets;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Studio.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Tilesheets.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class CompositionTests
{
    [Theory]
    [InlineData("gaf", typeof(AssetEditorControl))]
    [InlineData("gts", typeof(TilesheetEditorControl))]
    [InlineData("gani", typeof(AnimationEditorControl))]
    [InlineData("gsnd", typeof(AudioEditorControl))]
    [InlineData("gscn", typeof(SceneEditorControl))]
    [InlineData("gspr", typeof(SpriteEditorControl))]
    public void RealEditors_SaveRekeyRecoverPanesAndDispose(string format, Type editorType) => RunSta(directory =>
    {
        using var studio = Host(directory);
        var firstPath = Path.Combine(directory, "first." + format);
        var document = studio.NewDocument(format, format == "gaf" ? firstPath : null);
        var model = document.Document;
        Assert.IsType(editorType, model.Editor);
        Assert.True(model.Dirty());
        Assert.EndsWith(" *", document.Text);
        Assert.Same(studio.Workspace, document.DockPanel);
        Assert.Equal(3, studio.Workspace.Contents.Count);
        var inner = Assert.Single(Descendants(model.Editor).OfType<DockPanel>());
        var contents = inner.Contents.Cast<DockContent>().ToArray();
        Assert.Equal(model.PaneNames.Count, contents.Length);
        Assert.All(contents, pane => Assert.Same(inner, pane.DockPanel));
        if (model.Editor is SpriteEditorControl sprites)
        {
            foreach (var name in new[] { "player", "guard-01", "guard-02" })
            {
                var entry = sprites.Document.AddSprite();
                entry.Nickname = name;
                entry.SceneId = "level";
                entry.SceneLayerId = "actors";
            }
        }
        Assert.True(studio.SaveDocument(document, destination: firstPath));
        Assert.False(model.Dirty());
        Assert.Equal("first." + format, document.Text);
        switch (model.Editor)
        {
            case TilesheetEditorControl editor: editor.Document.MarkChanged(); break;
            case AnimationEditorControl editor: editor.Document.MarkChanged(); break;
            case AudioEditorControl editor: editor.Document.MarkChanged(); break;
            case SceneEditorControl editor: editor.Document.MarkChanged(); break;
            case SpriteEditorControl editor: editor.Document.MarkChanged(); break;
            case AssetEditorControl editor: editor.MarkChanged(); break;
        }
        Assert.True(model.Dirty());
        Assert.EndsWith(" *", document.Text);
        Assert.Same(document, studio.OpenDocument(Path.Combine(directory, ".", "FIRST." + format)));

        var target = contents[^1];
        target.Activate();
        target.Pane.CloseActiveContent();
        Assert.False(model.IsPaneVisible(target.Text));
        studio.RebuildViewMenu();
        var view = (ToolStripMenuItem)studio.MainMenuStrip!.Items[1];
        var paneItem = view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == target.Text);
        Assert.False(paneItem.Checked);
        paneItem.PerformClick();
        Assert.True(model.IsPaneVisible(target.Text));
        Assert.Contains(target, inner.Contents.Cast<DockContent>());
        target.Pane.CloseActiveContent();
        view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text!.StartsWith("Show all ")).PerformClick();
        Assert.True(model.IsPaneVisible(target.Text));

        var secondPath = Path.Combine(directory, "second." + format);
        Assert.True(studio.SaveDocument(document, saveAs: true, destination: secondPath));
        Assert.Equal(secondPath, model.Path());
        Assert.Equal("second." + format, document.Text);
        Assert.Same(document, studio.OpenDocument(secondPath));
        var oldFile = studio.OpenDocument(firstPath);
        Assert.NotSame(document, oldFile);
        Exception? collision = null;
        studio.ReportError = ex => collision = ex;
        Assert.False(studio.SaveDocument(oldFile, saveAs: true, destination: secondPath));
        Assert.IsType<InvalidOperationException>(collision);
        Assert.Equal(firstPath, oldFile.Document.Path());
        oldFile.Close();
        document.Close();
        Application.DoEvents();
        Assert.Empty(studio.Documents);
        Assert.True(model.Editor.IsDisposed);
        Assert.True(inner.IsDisposed);
        Assert.All(contents, pane => Assert.True(pane.IsDisposed));
        var reopened = studio.OpenDocument(secondPath);
        Assert.NotSame(document, reopened);
        if (reopened.Document.Editor is SpriteEditorControl restoredSprites)
            Assert.Equal(new[] { "player", "guard-01", "guard-02" }, restoredSprites.Document.Definition.Sprites.Select(sprite => sprite.Nickname));
        Assert.Equal(model.PaneNames.Count, Assert.Single(Descendants(reopened.Document.Editor).OfType<DockPanel>()).Contents.Count);
    });

    [Fact]
    public void MixedDocuments_CloseCancelSaveAndShutdownAreTransactional() => RunSta(directory =>
    {
        using var studio = Host(directory);
        foreach (var format in new[] { "gts", "gani", "gsnd", "gscn", "gspr", "gaf" })
            studio.NewDocument(format, format == "gaf" ? Path.Combine(directory, "assets.gaf") : null);
        Assert.Equal(6, studio.Documents.Count);
        Assert.Equal(8, studio.Workspace.Contents.Count);
        var documents = studio.Documents.ToArray();
        int prompts = 0;
        studio.AskSave = _ => ++prompts == 2 ? DialogResult.Cancel : DialogResult.No;
        Assert.False(studio.ApproveShutdown());
        Assert.All(documents, doc => Assert.False(doc.CloseApproved));
        Assert.All(documents, doc => Assert.False(doc.IsDisposed));
        studio.AskSave = _ => DialogResult.Cancel;
        documents[0].Close();
        Assert.False(documents[0].IsDisposed);
        studio.AskSave = _ => DialogResult.Yes;
        studio.SavePath = model => Path.Combine(directory, "saved." + model.Extension);
        documents[0].Close();
        Assert.True(documents[0].IsDisposed);
        Assert.True(File.Exists(Path.Combine(directory, "saved.gts")));
        prompts = 0;
        studio.AskSave = _ => { prompts++; return DialogResult.No; };
        studio.Close();
        Assert.Equal(5, prompts);
        Assert.All(documents, doc => Assert.True(doc.IsDisposed));
        Assert.All(documents, doc => Assert.True(doc.Document.Editor.IsDisposed));
    });

    [Fact]
    public void EncryptedAssetToolbarSaveAsAdoptsPathWithoutRegisteringRuntimeAssets() => RunSta(directory =>
    {
        var registry = AssetsFile.AllAssetsFiles.ToArray();
        var source = Path.Combine(directory, "source.gaf");
        using (var assets = AssetsFile.LoadOrCreate(source, "secret", true, register: false))
        {
            using var bytes = new MemoryStream([1, 2, 3]);
            assets.Add(AssetTypes.Misc, "example", bytes);
            assets.Save();
        }
        using var studio = Host(directory);
        studio.AssetPassword = _ => "secret";
        var document = studio.OpenDocument(source);
        var editor = Assert.IsType<AssetEditorControl>(document.Document.Editor);
        var destination = Path.Combine(directory, "copy.zip");
        studio.SavePath = _ => destination;
        editor.MarkChanged();
        editor.SaveRequested!(true);
        Assert.Equal(destination, editor.FilePath);
        Assert.False(editor.IsDirty);
        Assert.Same(document, studio.OpenDocument(destination));
        Assert.Equal(registry, AssetsFile.AllAssetsFiles);
        using (var saved = AssetsFile.LoadOrCreate(destination, "secret", true, register: false))
        using (var stream = saved[AssetTypes.Misc, "example"])
        {
            Assert.NotNull(stream);
            Assert.Equal(1, stream.ReadByte());
        }
        Assert.ThrowsAny<Exception>(() => AssetsFile.LoadOrCreate(destination));
        Assert.Equal(registry, AssetsFile.AllAssetsFiles);
        document.Close();
        Assert.Null(editor.SaveRequested);
        Assert.Equal(registry, AssetsFile.AllAssetsFiles);
    });

    [Fact]
    public void CancelledOrFailedSavesKeepDocumentOpenAndPathUnchanged() => RunSta(directory =>
    {
        using var studio = Host(directory);
        var document = studio.NewDocument("gani");
        studio.AskSave = _ => DialogResult.Yes;
        studio.SavePath = _ => null;
        document.Close();
        Assert.False(document.IsDisposed);
        Assert.Null(document.Document.Path());
        var destination = Path.Combine(directory, "animation.gani");
        studio.AllowInvalidSave = _ => false;
        Assert.False(studio.SaveDocument(document, destination: destination));
        Assert.False(File.Exists(destination));
        studio.AllowInvalidSave = _ => true;
        Exception? failure = null;
        studio.ReportError = ex => failure = ex;
        var blocked = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blocked, "occupied");
        Assert.False(studio.SaveDocument(document, destination: Path.Combine(blocked, "animation.gani")));
        Assert.NotNull(failure);
        Assert.Null(document.Document.Path());
        Assert.True(document.Document.Dirty());
        Assert.True(studio.SaveDocument(document, destination: destination));
        Assert.Same(document, studio.OpenDocument(destination));
    });

    [Fact]
    public void ViewTracksActiveEditorAndRestoresGlobalTools() => RunSta(directory =>
    {
        using var studio = Host(directory);
        var animation = studio.NewDocument("gani");
        var scene = studio.NewDocument("gscn");
        var view = (ToolStripMenuItem)studio.MainMenuStrip!.Items[1];
        foreach (var document in new[] { animation, scene, animation })
        {
            document.Activate();
            Application.DoEvents();
            studio.RebuildViewMenu();
            var items = view.DropDownItems.OfType<ToolStripMenuItem>().ToArray();
            Assert.Equal(document.Document.PaneNames, items.Where(item => document.Document.PaneNames.Contains(item.Text)).Select(item => item.Text));
        }
        var output = studio.Workspace.Contents.Cast<DockContent>().Single(content => content.Text == "Output");
        output.Hide();
        studio.RebuildViewMenu();
        var item = view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Output");
        Assert.False(item.Checked);
        item.PerformClick();
        Assert.False(output.IsHidden);
    });

    [Theory]
    [InlineData(".gaf", "gaf")]
    [InlineData(".zip", "gaf")]
    [InlineData(".GTS", "gts")]
    [InlineData(".gani", "gani")]
    [InlineData(".gsnd", "gsnd")]
    [InlineData(".gscn", "gscn")]
    [InlineData(".gspr", "gspr")]
    [InlineData(".gondwana-scene", null)]
    public void DispatchUsesCurrentFormats(string extension, string? format) =>
        Assert.Equal(format, StudioDocument.FormatFor("test" + extension));

    [Fact]
    public void BrowserIsLazyAndHandlesMissingDirectories() => RunSta(directory =>
    {
        var child = Directory.CreateDirectory(Path.Combine(directory, "child"));
        File.WriteAllText(Path.Combine(child.FullName, "nested.gts"), "{}");
        foreach (var extension in new[] { "gaf", "zip", "gts", "gani", "gsnd", "gscn", "gspr", "txt" })
            File.WriteAllText(Path.Combine(directory, "file." + extension), "{}");
        using var studio = Host(directory);
        var root = Assert.Single(studio.Browser.Tree.Nodes.Cast<TreeNode>());
        Assert.Equal(8, root.Nodes.Count);
        var folder = root.Nodes.Cast<TreeNode>().Single(node => node.Tag is DirectoryInfo);
        Assert.Null(Assert.Single(folder.Nodes.Cast<TreeNode>()).Tag);
        folder.Expand();
        Assert.IsType<string>(Assert.Single(folder.Nodes.Cast<TreeNode>()).Tag);
        studio.SetWorkingDirectory(Path.Combine(directory, "missing"));
        Assert.Contains("Cannot read directory", studio.Browser.Tree.Nodes[0].Nodes[0].Text);
    });

    private static MainForm Host(string directory)
    {
        var studio = new MainForm(loadPlugins: false) { Opacity = 0, ShowInTaskbar = false };
        studio.ReportError = ex => throw new InvalidOperationException("Unexpected shell error", ex);
        studio.AllowInvalidSave = _ => true;
        studio.AskSave = _ => DialogResult.No;
        studio.SetWorkingDirectory(directory);
        studio.Show();
        Application.DoEvents();
        return studio;
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void RunSta(Action<string> action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "GondwanaStudio-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                action(directory);
            }
            catch (Exception ex) { error = ex; }
            finally { Directory.Delete(directory, recursive: true); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Studio interaction timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
