using System.Runtime.ExceptionServices;
using Gondwana.Assets;
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
        Assert.True(studio.SaveDocument(document, destination: firstPath));
        Assert.False(model.Dirty());
        Assert.Equal("first." + format, document.Text);
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
        Assert.Equal(model.PaneNames.Count, Assert.Single(Descendants(reopened.Document.Editor).OfType<DockPanel>()).Contents.Count);
    });

    [Fact]
    public void MixedDocuments_CloseCancelSaveAndShutdownAreTransactional() => RunSta(directory =>
    {
        using var studio = Host(directory);
        foreach (var format in new[] { "gts", "gani", "gsnd", "gscn", "gaf" })
            studio.NewDocument(format, format == "gaf" ? Path.Combine(directory, "assets.gaf") : null);
        Assert.Equal(5, studio.Documents.Count);
        Assert.Equal(7, studio.Workspace.Contents.Count);
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
        Assert.Equal(4, prompts);
        Assert.All(documents, doc => Assert.True(doc.IsDisposed));
        Assert.All(documents, doc => Assert.True(doc.Document.Editor.IsDisposed));
    });

    [Theory]
    [InlineData(".gaf", "gaf")]
    [InlineData(".zip", "gaf")]
    [InlineData(".GTS", "gts")]
    [InlineData(".gani", "gani")]
    [InlineData(".gsnd", "gsnd")]
    [InlineData(".gscn", "gscn")]
    [InlineData(".gondwana-scene", null)]
    public void DispatchUsesCurrentFormats(string extension, string? format) =>
        Assert.Equal(format, StudioDocument.FormatFor("test" + extension));

    [Fact]
    public void BrowserIsLazyAndHandlesMissingDirectories() => RunSta(directory =>
    {
        var child = Directory.CreateDirectory(Path.Combine(directory, "child"));
        File.WriteAllText(Path.Combine(child.FullName, "nested.gts"), "{}");
        foreach (var extension in new[] { "gaf", "zip", "gts", "gani", "gsnd", "gscn", "txt" })
            File.WriteAllText(Path.Combine(directory, "file." + extension), "{}");
        using var studio = Host(directory);
        var root = Assert.Single(studio.Browser.Tree.Nodes.Cast<TreeNode>());
        Assert.Equal(7, root.Nodes.Count);
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
