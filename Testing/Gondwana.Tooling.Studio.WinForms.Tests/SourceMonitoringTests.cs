using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Tilesheets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    private static void WriteMonitored(string path, string format, string identity)
    {
        switch (format)
        {
            case "gts":
                TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = identity }]);
                break;
            case "gani":
                AnimationDefinitionSerializer.SaveAll(path, [new() { Key = identity }]);
                break;
            case "gscn":
                SceneDefinitionSerializer.SaveAll(path, [new() { ID = identity }]);
                break;
        }
    }

    private static void VerifySources(MainForm studio)
    {
        Application.DoEvents(); // Apply queued reference edits before verifying their inventory.
        studio.VerifyDocumentSources();
        Application.DoEvents(); // Deliver verified observations through the real asynchronous UI route.
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    public void SourceMonitoringCleanChangesOfferReloadAndEstablishCleanReplacement(string format) => RunSta(root =>
    {
        var path = Path.Combine(root, "source." + format);
        WriteMonitored(path, format, "before");
        using var studio = Host(root);
        var original = studio.OpenDocument(path);
        var pane = original.Pane;
        studio.ConfirmReload = _ => throw new InvalidOperationException("Clean reload must not ask for discard.");
        WriteMonitored(path, format, "after");
        VerifySources(studio);
        Assert.Contains("[reload]", original.Text);
        Assert.False(original.Document.Dirty());
        Assert.Equal("before", original.Document.SelectedMember);
        Assert.True(studio.ReloadDocument(original));
        var replacement = Assert.Single(studio.Documents);
        Assert.True(original.IsDisposed);
        Assert.Same(pane, replacement.Pane);
        Assert.Equal("after", replacement.Document.SelectedMember);
        Assert.False(replacement.Document.Dirty());
        Assert.False(replacement.Document.ContentStale);
        VerifySources(studio);
        Assert.DoesNotContain("[reload]", replacement.Text);
        Assert.Equal(1, studio.SourceRegistrationCount);
    });

    [Fact]
    public void SourceMonitoringKeepEditsCannotOverwriteAndSaveAsPreservesExternalContent() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "before");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        var editor = (TilesheetEditorControl)tab.Document.Editor;
        editor.Document.History.Set("Rename", () => editor.Document.Definition.Name, value => editor.Document.Definition.Name = value!, "local");
        var history = editor.Document.History;
        var accepted = tab.Document.AcceptedSource;
        WriteMonitored(path, "gts", "external");
        VerifySources(studio);
        Assert.Contains("[conflict]", tab.Text);
        Assert.Equal("local", tab.Document.SelectedMember);
        int prompts = 0;
        studio.AskSourceConflict = _ =>
        {
            prompts++;
            return MainForm.SourceConflictChoice.KeepEdits;
        };
        Assert.False(AwaitUi(studio.SaveDocument(tab)));
        Application.DoEvents();
        Assert.False(AwaitUi(studio.SaveDocument(tab)));
        Application.DoEvents();
        Assert.Equal(1, prompts);
        Assert.Equal(accepted, tab.Document.AcceptedSource);
        Assert.Same(history, tab.Document.History);
        Assert.True(history.CanUndo);
        Assert.True(tab.Document.Dirty());
        Assert.Equal("external", TilesheetDefinitionSerializer.LoadAll(path)[0].Name);
        var copy = Path.Combine(root, "copy.gts");
        Assert.True(AwaitUi(studio.SaveDocument(tab, saveAs: true, destination: copy)));
        Assert.Equal("external", TilesheetDefinitionSerializer.LoadAll(path)[0].Name);
        Assert.Equal("local", TilesheetDefinitionSerializer.LoadAll(copy)[0].Name);
        VerifySources(studio);
        Assert.False(tab.Document.ContentStale);
        Assert.DoesNotContain(tab.Document.Sources.Keys, s => s.Path == path);
    });

    [Fact]
    public void SourceMonitoringFailedOrCancelledReloadRetainsHistoryAndSelectedMember() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = "first" }, new() { Name = "selected" }]);
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        Assert.True(tab.Document.SelectMember("selected"));
        var editor = (TilesheetEditorControl)tab.Document.Editor;
        editor.PreviewZoom = 2f;
        editor.Document.History.Set("Alpha", () => editor.Document.Definition.PremultiplyAlpha, value => editor.Document.Definition.PremultiplyAlpha = value, true);
        var history = tab.Document.History;
        File.WriteAllText(path, "incomplete {");
        studio.ConfirmReload = _ => false;
        Assert.False(studio.ReloadDocument(tab));
        studio.ConfirmReload = _ => true;
        Exception? error = null;
        studio.ReportError = e => error = e;
        Assert.False(studio.ReloadDocument(tab));
        Assert.NotNull(error);
        Assert.Same(tab, Assert.Single(studio.Documents));
        Assert.Same(history, tab.Document.History);
        Assert.True(history!.CanUndo);
        Assert.True(tab.Document.Dirty());
        TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = "first" }, new() { Name = "selected" }]);
        Assert.True(studio.ReloadDocument(tab));
        var replacement = Assert.Single(studio.Documents);
        Assert.Equal("selected", replacement.Document.SelectedMember);
        Assert.Equal(2f, ((TilesheetEditorControl)replacement.Document.Editor).PreviewZoom);
        Assert.False(replacement.Document.Dirty());
        Assert.False(replacement.Document.History!.CanUndo);
    });

    [Fact]
    public void SourceMonitoringOwnSaveRejectsQueuedOldObservationButDetectsFollowingExternalWrite() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "before");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        tab.Document.History!.MarkUntracked();
        Assert.True(AwaitUi(studio.SaveDocument(tab)));
        VerifySources(studio);
        Assert.False(tab.Document.ContentStale);
        Assert.DoesNotContain("[reload]", tab.Text);
        WriteMonitored(path, "gts", "external");
        VerifySources(studio);
        Assert.True(tab.Document.ContentStale);
    });

    [Fact]
    public void SourceMonitoringImageChangesOnlyInvalidatePreviewsAndScanAndReferencesAreReplaced() => RunSta(root =>
    {
        var image = Path.Combine(root, "one.png");
        var other = Path.Combine(root, "two.png");
        using (var bitmap = new Bitmap(8, 8))
        {
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(other, System.Drawing.Imaging.ImageFormat.Png);
        }

        var path = Path.Combine(root, "source.gts");
        TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = "one", Image = new() { FilePath = image } }]);
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        studio.ShowDependencies();
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        Assert.NotNull(studio.DependencyScan);
        tab.Document.History!.MarkUntracked();
        using (var bitmap = new Bitmap(9, 9))
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
        VerifySources(studio);
        Assert.Null(studio.DependencyScan);
        Assert.True(tab.Document.PreviewStale);
        Assert.False(tab.Document.ContentStale);
        Assert.Contains("[preview]", tab.Text);
        var history = tab.Document.History;
        studio.ReloadPreviews(tab);
        Assert.False(tab.Document.PreviewStale);
        Assert.True(tab.Document.Dirty());
        Assert.Same(history, tab.Document.History);
        var editor = (TilesheetEditorControl)tab.Document.Editor;
        editor.Document.Definition.Image.FilePath = other;
        editor.Document.MarkChanged();
        Application.DoEvents();
        Assert.DoesNotContain(tab.Document.Sources.Keys, s => s.Path == image);
        Assert.Contains(tab.Document.Sources.Keys, s => s.Path == other);
    });

    [Fact]
    public void SourceMonitoringMalformedReferenceKeepsLastValidDependencyWatchers() => RunSta(root =>
    {
        var image = Path.Combine(root, "image.png");
        using (var bitmap = new Bitmap(8, 8))
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
        var path = Path.Combine(root, "sheet.gts");
        TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = "sheet", Image = new() { FilePath = image } }]);
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        Assert.Contains(tab.Document.Sources.Keys, source => source.Path == image);
        var editor = (TilesheetEditorControl)tab.Document.Editor;
        editor.Document.Definition.Image.FilePath = null; // Incomplete property edit; reference walk fails.
        editor.Document.MarkChanged();
        Application.DoEvents();
        Assert.Contains(tab.Document.Sources.Keys, source => source.Path == image);
        using (var bitmap = new Bitmap(9, 9))
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
        VerifySources(studio);
        Assert.True(tab.Document.PreviewStale);
        Assert.Contains("[preview]", tab.Text);
    });

    [Fact]
    public void SourceMonitoringPrimaryRevertClearsStaleFlagOnRegistrationRefresh() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "original");
        var original = File.ReadAllBytes(path);
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        var accepted = tab.Document.AcceptedSource;
        WriteMonitored(path, "gts", "external");
        VerifySources(studio);
        Assert.True(tab.Document.ContentStale);
        File.WriteAllBytes(path, original);
        studio.StudioToolbar.Items["Refresh"]!.PerformClick();
        Application.DoEvents();
        Assert.False(tab.Document.ContentStale);
        Assert.Null(tab.Document.SourceProblem);
        Assert.DoesNotContain("[reload]", tab.Text);
        Assert.Equal(accepted, tab.Document.AcceptedSource);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceMonitoringFailedDependencyReloadRetainsBaselineUntilSuccess(bool missing) => RunSta(root =>
    {
        var sheet = Path.Combine(root, "sheet.gts");
        WriteMonitored(sheet, "gts", "sheet");
        var animation = Path.Combine(root, "animation.gani");
        AnimationDefinitionSerializer.SaveAll(animation, [new() { Key = "animation",
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", sheet)] }]);
        using var studio = Host(root);
        var tab = studio.OpenDocument(animation);
        var dependency = Assert.Single(tab.Document.Sources, pair => pair.Key.Path == sheet);
        var accepted = dependency.Value.Accepted;
        if (missing)
            File.Delete(sheet);
        else
            File.WriteAllText(sheet, "invalid {");
        VerifySources(studio);
        Assert.True(tab.Document.PreviewStale);
        studio.ReloadPreviews(tab);
        Assert.True(tab.Document.PreviewStale);
        Assert.Equal(accepted, tab.Document.Sources[dependency.Key].Accepted);
        studio.ReloadPreviews(tab); // A second failed refresh must not accept the missing/invalid file.
        Assert.True(tab.Document.PreviewStale);
        Assert.Equal(accepted, tab.Document.Sources[dependency.Key].Accepted);
        WriteMonitored(sheet, "gts", "sheet");
        studio.ReloadPreviews(tab);
        Assert.False(tab.Document.PreviewStale);
        Assert.Equal(DocumentSourceVersion.Read(dependency.Key), tab.Document.Sources[dependency.Key].Accepted);
    });

    [Fact]
    public void SourceMonitoringCloseAndWorkspaceChangeDiscardQueuedCallbacks() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "before");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        WriteMonitored(path, "gts", "external");
        studio.VerifyDocumentSources(); // Queue delivery, then dispose before the UI processes it.
        tab.Close();
        var other = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;
        studio.SetWorkingDirectory(other);
        Application.DoEvents();
        Assert.Empty(studio.Documents);
        Assert.Equal(0, studio.SourceRegistrationCount);
    });

    [Fact]
    public void SourceMonitoringDefinitionDependencyAndInFlightScanAreInvalidated() => RunSta(root =>
    {
        var sheet = Path.Combine(root, "sheet.gts");
        WriteMonitored(sheet, "gts", "sheet");
        var path = Path.Combine(root, "animation.gani");
        AnimationDefinitionSerializer.SaveAll(path, [new() { Key = "animation",
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", sheet)] }]);
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        Assert.Contains(tab.Document.Sources.Keys, s => s.Path == sheet);
        var scan = DefinitionReferenceDiscovery.Inspect(root);
        var completion = new TaskCompletionSource<DefinitionReferenceDiscovery>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        studio.InspectDependencies = (_, _, cancellation) =>
        {
            token = cancellation;
            return completion.Task;
        };
        studio.ShowDependencies();
        tab.Document.History!.MarkUntracked();
        WriteMonitored(sheet, "gts", "changed");
        VerifySources(studio);
        Assert.True(tab.Document.PreviewStale);
        Assert.True(tab.Document.Dirty());
        Assert.False(tab.Document.ContentStale);
        Assert.True(token.IsCancellationRequested);
        completion.SetResult(scan);
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        Assert.Null(studio.DependencyScan);
    });

    [Fact]
    public void SourceMonitoringCompletedScanSubscriptionsEndOnWorkspaceSwitch() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "source");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        studio.ShowDependencies();
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        Assert.NotNull(studio.DependencyScan);
        tab.Close();
        Assert.True(studio.SourceRegistrationCount > 0); // Saved scan remains relevant without an open tab.
        WriteMonitored(path, "gts", "external");
        VerifySources(studio);
        Assert.Null(studio.DependencyScan);
        Assert.Equal(0, studio.SourceRegistrationCount);
        studio.SetWorkingDirectory(Directory.CreateDirectory(Path.Combine(root, "other")).FullName);
        Assert.Equal(0, studio.SourceRegistrationCount);
    });

    [Fact]
    public void SourceMonitoringMissingAndInaccessiblePrimaryRetainEditorAndRecover() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "source");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            VerifySources(studio);
            Assert.Contains("inaccessible", tab.Document.SourceProblem);
            Assert.True(tab.Document.ContentStale);
        }

        VerifySources(studio);
        Assert.False(tab.Document.ContentStale);
        File.Move(path, path + ".renamed");
        VerifySources(studio);
        Assert.True(tab.Document.ContentStale);
        Assert.Equal(path, tab.Document.Location!.Path);
        Assert.False(tab.IsDisposed);
        Assert.Equal("source", tab.Document.SelectedMember);
    });

    [Fact]
    public void SourceMonitoringExternalWriteAtSaveCompletionIsNotAcceptedAsOwnSave() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        WriteMonitored(path, "gts", "source");
        using var studio = Host(root);
        var tab = studio.OpenDocument(path);
        tab.Document.History!.MarkUntracked();
        bool written = false;
        tab.Document.Changed += (_, _) =>
        {
            if (!written && !tab.Document.Dirty())
            {
                written = true;
                WriteMonitored(path, "gts", "external");
            }
        };
        Assert.True(AwaitUi(studio.SaveDocument(tab)));
        Assert.True(written);
        VerifySources(studio);
        Assert.True(tab.Document.ContentStale);
        Assert.Contains("[reload]", tab.Text);
        Assert.Equal("source", tab.Document.SelectedMember);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceMonitoringOtherPackedEntryDoesNotConflictAndSaveRetainsIt(bool encrypted) => RunSta(root =>
    {
        var path = Path.Combine(root, "assets.gaf");
        var password = encrypted ? "secret" : null;
        using (var archive = AssetsFile.LoadOrCreate(path, password, encrypted, register: false))
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"sheet\"}"));
            archive.Add(AssetTypes.TilesheetDefinition, "sheet.gts", source);
            archive.Save();
        }

        using var studio = Host(root);
        var tab = studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, "sheet.gts"), password);
        tab.Document.History!.MarkUntracked();
        using (var archive = AssetsFile.LoadOrCreate(path, password, encrypted, register: false))
        {
            using var payload = new MemoryStream([1, 2, 3]);
            archive.Add(AssetTypes.Misc, "external.bin", payload);
            archive.Save();
        }

        VerifySources(studio);
        Assert.False(tab.Document.ContentStale);
        Assert.True(AwaitUi(studio.SaveDocument(tab)));
        VerifySources(studio);
        Assert.False(tab.Document.ContentStale);
        using var saved = AssetsFile.LoadOrCreate(path, password, encrypted, register: false);
        Assert.Contains(saved.GetAllEntries(), e => e.AssetName == "external.bin");
    });
}
