using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    private static AssetImportPlan HistoryImport(AssetEditorControl editor, string root, string name = "nested/new.bin")
    {
        var source = Path.Combine(root, "history-input.bin");
        File.WriteAllBytes(source, [1, 2, 3]);
        return AssetImportPlan.Create(editor.AssetsFile, [new(source, AssetTypes.Misc, name)], false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GafKeyboardMenuGridFiltersAndNativeSearchUndo(bool standalone) => RunSta(root =>
    {
        var path = Path.Combine(root, "keys.gaf");
        using (var initial = AssetsFile.LoadOrCreate(path, null, false, register: false)) initial.Save();
        using var studio = standalone ? null : Host(root);
        using var shell = standalone ? new Gondwana.Tooling.Assets.WinForms.MainForm() : null;
        AssetEditorControl editor;
        if (shell is not null)
        {
            shell.Show(); Application.DoEvents();
            shell.GetType().GetMethod("OpenDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(shell, [path]);
            Application.DoEvents(); editor = Descendants(shell).OfType<AssetEditorControl>().Single();
        }
        else editor = (AssetEditorControl)studio!.OpenDocument(path).Document.Editor;
        var host = (Form?)shell ?? studio!;
        var menu = host.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&Edit");
        var grid = Descendants(editor).OfType<DataGridView>().Single();
        var strip = Descendants(editor).OfType<ToolStrip>().Single(t => t.Items.OfType<ToolStripTextBox>().Any());
        var search = strip.Items.OfType<ToolStripTextBox>().Single();
        var types = strip.Items.OfType<ToolStripComboBox>().Single();
        Assert.False(menu.DropDownItems[0].Enabled);
        editor.ApplyImport(HistoryImport(editor, root));
        Assert.Contains("Import 1 asset(s)", menu.DropDownItems[0].Text);
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        Application.DoEvents(); Assert.True(grid.Focus());
        RouteKey(Keys.Control | Keys.Z); Assert.Empty(grid.Rows.Cast<DataGridViewRow>()); Assert.False(editor.IsDirty);
        RouteKey(Keys.Control | Keys.Y); Assert.Single(grid.Rows.Cast<DataGridViewRow>()); Assert.True(editor.IsDirty);
        RouteKey(Keys.Control | Keys.Z); RouteKey(Keys.Control | Keys.Shift | Keys.Z);
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        types.SelectedItem = AssetTypes.Misc;
        Assert.True(types.ComboBox.Focus());
        RouteKey(Keys.Control | Keys.Z); Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        RouteKey(Keys.Control | Keys.Y); Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        editor.ReplaceAsset(AssetTypes.Misc, "nested/new.bin", [4]);
        Assert.Contains("Replace nested/new.bin", menu.DropDownItems[0].Text);
        // Exactly one execution even though both editor and shell route commands.
        Assert.True(grid.Focus()); RouteKey(Keys.Control | Keys.Z);
        Assert.Single(editor.AssetsFile.GetAllEntries()); Assert.True(editor.History.CanUndo);
        menu.DropDownItems[1].PerformClick();
        search.Text = "nested"; search.TextBox.Focus(); search.TextBox.SelectAll();
        ReplaceText(search.TextBox.Handle, 0xC2, 1, "other");
        Assert.True(search.TextBox.CanUndo); Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        RouteKey(Keys.Control | Keys.Z);
        Assert.Equal("nested", search.Text); Assert.Contains("Replace", editor.History.UndoDescription);
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        menu.DropDownItems[0].PerformClick(); // menu still targets document while search has focus
        Assert.Equal("nested", search.Text); Assert.Equal(AssetTypes.Misc, types.SelectedItem);
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        if (studio is not null) Assert.True(AwaitUi(studio.SaveDocument(studio.Documents.Single())));
        else editor.SaveTo(path);
    });

    [Fact]
    public void GafStudioSaveAsUndoUsesAdoptedArchiveAndFailedSaveKeepsHistory() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, "secret", true, register: false))
        {
            using var seed = new MemoryStream(new byte[] { 7 });
            archive.Add(AssetTypes.Misc, "seed", seed);
            archive.Save();
        }
        using var studio = Host(root); studio.AssetPassword = _ => "secret";
        var document = studio.OpenDocument(path);
        var editor = (AssetEditorControl)document.Document.Editor;
        editor.ApplyImport(HistoryImport(editor, root));
        var original = File.ReadAllBytes(path);
        Exception? failure = null; studio.ReportError = ex => failure = ex;
        studio.BeforeArchiveInstall = () => throw new IOException("injected before install");
        Assert.False(AwaitUi(studio.SaveDocument(document)));
        Assert.NotNull(failure); Assert.True(editor.IsDirty); Assert.True(editor.History.CanUndo);
        Assert.Equal(original, File.ReadAllBytes(path));
        editor.History.Undo(); Assert.False(editor.IsDirty); editor.History.Redo();
        studio.BeforeArchiveInstall = null;
        var target = Path.Combine(root, "copies", "adopted.gaf");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        failure = null;
        Assert.True(AwaitUi(studio.SaveDocument(document, saveAs: true, destination: target)), failure?.ToString());
        Assert.Equal(target, editor.FilePath); Assert.False(editor.IsDirty);
        editor.History.Undo(); Assert.Equal("seed", Assert.Single(editor.AssetsFile.GetAllEntries()).AssetName); Assert.True(editor.IsDirty);
        Assert.Equal(target, document.Document.Path()); Assert.Same(document, studio.OpenDocument(target));
        Assert.Equal(original, File.ReadAllBytes(path));
        editor.History.Redo(); Assert.False(editor.IsDirty); Assert.True(editor.AssetsFile.UseEncryption);
        editor.ReplaceAsset(AssetTypes.Misc, "nested/new.bin", [9]);
        failure = null;
        Assert.False(AwaitUi(studio.SaveDocument(document, saveAs: true, destination: path)));
        Assert.NotNull(failure); Assert.Equal(target, editor.FilePath); Assert.True(editor.IsDirty);
        editor.History.Undo(); Assert.False(editor.IsDirty);
    });

    [Fact]
    public void GafStandaloneSaveCopyAndFailedSaveKeepCheckpoint() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gaf");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false); archive.Save();
        using var editor = new AssetEditorControl(archive);
        editor.ApplyImport(HistoryImport(editor, root));
        var copy = Path.Combine(root, "copy.gaf");
        editor.SaveCopyTo(copy, "secret", true);
        Assert.Equal(path, editor.FilePath); Assert.True(editor.IsDirty); Assert.True(editor.History.CanUndo);
        using (var saved = AssetsFile.LoadOrCreate(copy, "secret", true, register: false)) Assert.Single(saved.GetAllEntries());
        Assert.Throws<IOException>(() => editor.SaveTo(copy));
        Assert.True(editor.IsDirty); editor.History.Undo(); Assert.False(editor.IsDirty); editor.History.Redo();
        editor.SaveTo(path); Assert.False(editor.IsDirty);
        editor.History.Undo(); Assert.True(editor.IsDirty); editor.History.Redo(); Assert.False(editor.IsDirty);
        editor.MarkChanged(); Assert.True(editor.IsDirty); Assert.False(editor.History.CanUndo); Assert.False(editor.History.CanRedo);
    });

    [Fact]
    public void GafAndPackedChildHistoriesRemainIndependentAcrossSavesAndTabSwitches() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var parent = studio.OpenDocument(path);
        var editor = (AssetEditorControl)parent.Document.Editor;
        var child = studio.OpenDocument(NativeLocation(path, "gts"));
        var tiles = ((TilesheetEditorControl)child.Document.Editor).Document;
        var original = tiles.Definition.Name;
        tiles.RenameDefinition("child edit");
        editor.ApplyImport(HistoryImport(editor, root));
        parent.Activate(); Application.DoEvents();
        var menu = studio.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&Edit");
        menu.DropDownItems[0].PerformClick(); Assert.False(editor.IsDirty); Assert.True(tiles.IsDirty);
        Assert.Equal("child edit", tiles.Definition.Name);
        child.Activate(); Application.DoEvents(); menu.DropDownItems[0].PerformClick();
        Assert.Equal(original, tiles.Definition.Name); Assert.False(tiles.IsDirty); Assert.True(editor.History.CanRedo);
        // A persisted child save is an external parent refresh: its own history survives,
        // the parent's now-obsolete redo branch cannot overwrite the new disk baseline.
        tiles.RenameDefinition("persisted child");
        Assert.True(AwaitUi(studio.SaveDocument(child)));
        Assert.True(tiles.History.CanUndo); Assert.False(editor.History.CanRedo); Assert.False(editor.History.CanUndo); Assert.False(editor.IsDirty);
        tiles.History.Undo(); Assert.True(tiles.IsDirty); Assert.False(editor.IsDirty);
    });

    [Fact]
    public void PendingGafSaveBlocksMenuAndQueuedShortcutReplay() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root); WaitForArchives(studio.Browser);
        var document = studio.OpenDocument(path);
        var editor = (AssetEditorControl)document.Document.Editor;
        editor.ApplyImport(HistoryImport(editor, root));
        using var opened = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        studio.Browser.ArchiveOpened = _ => { opened.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(15))); };
        studio.Browser.RefreshDirectory(); Assert.True(opened.Wait(TimeSpan.FromSeconds(10)));
        Task<bool>? save = null;
        try
        {
            save = studio.SaveDocument(document); Assert.False(save.IsCompleted); Assert.False(document.Enabled);
            var menu = studio.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&Edit");
            Assert.False(menu.DropDownItems[0].Enabled); menu.DropDownItems[0].PerformClick();
            studio.Focus(); RouteKey(Keys.Control | Keys.Z); RouteKey(Keys.Control | Keys.Y);
            Assert.Contains(editor.AssetsFile.GetAllEntries(), e => e.AssetName == "nested/new.bin");
            Assert.True(editor.IsDirty);
        }
        finally { release.Set(); if (save is not null) Assert.True(AwaitUi(save)); }
        Assert.False(editor.IsDirty); editor.History.Undo(); Assert.True(editor.IsDirty);
    });

    [Fact]
    public void CommittedDiskRefreshFailureInvalidatesHistoryAndBlocksStaleSaves() => RunSta(root =>
    {
        var path = Path.Combine(root, "refresh.gaf");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        using (var bytes = new MemoryStream(new byte[] { 1 })) archive.Add(AssetTypes.Misc, "entry", bytes);
        archive.Save();
        using var editor = new AssetEditorControl(archive);
        editor.ReplaceAsset(AssetTypes.Misc, "entry", [2]); editor.SaveTo(path);
        Assert.True(editor.History.CanUndo);
        var plan = AssetDeletionPlan.Create(path, [new(AssetTypes.Misc, path, "entry")]);
        plan.Commit(); // committed disk changes are never compensated by document history
        File.WriteAllText(path, "invalid zip after commit"); // simulate failed refresh read
        var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            editor.GetType().GetMethod("RefreshAfterDiskOperation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(editor, ["Deletion committed."]));
        Assert.IsType<IOException>(failure.InnerException);
        Assert.False(editor.History.CanUndo); Assert.False(editor.History.CanRedo);
        Assert.False(editor.CanSave); Assert.True(editor.CommitEdits()); Assert.False(editor.Enabled);
        Assert.Throws<InvalidOperationException>(() => editor.SaveTo(path));
        Assert.Equal("invalid zip after commit", File.ReadAllText(path));
    });

    [Fact]
    public void FailedArchiveAdoptionPreservesCheckpointAndCommands() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gaf");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false); archive.Save();
        using var editor = new AssetEditorControl(archive);
        editor.ApplyImport(HistoryImport(editor, root));
        var replacementPath = Path.Combine(root, "broken.gaf");
        using var replacement = AssetsFile.LoadOrCreate(replacementPath, null, false, register: false);
        replacement.Save(); replacement.Dispose(); File.WriteAllText(replacementPath, "broken zip");
        Assert.ThrowsAny<Exception>(() => editor.AcceptSavedArchive(replacement));
        Assert.Same(archive, editor.AssetsFile); Assert.Equal(path, editor.FilePath);
        Assert.True(editor.IsDirty); Assert.True(editor.History.CanUndo);
        editor.History.Undo(); Assert.False(editor.IsDirty);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedStandaloneSamePathSaveKeepsBothImportsAndSavedBaseline(bool encrypted) => RunSta(root =>
    {
        var path = Path.Combine(root, "locked.gaf");
        using var archive = AssetsFile.LoadOrCreate(path, encrypted ? "secret" : null, encrypted, register: false);
        archive.Save();
        using var editor = new AssetEditorControl(archive);
        editor.ApplyImport(HistoryImport(editor, root, "first.bin"));
        editor.ApplyImport(HistoryImport(editor, root, "second.bin"));
        var disk = File.ReadAllBytes(path);
        // Read-sharing permits version checks, but denies in-place writes and atomic installation.
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => editor.SaveTo(path));
        Assert.Equal(disk, File.ReadAllBytes(path)); Assert.Same(archive, editor.AssetsFile);
        editor.History.Undo();
        Assert.Equal("first.bin", Assert.Single(archive.GetAllEntries()).AssetName);
        Assert.True(editor.IsDirty); Assert.True(editor.History.CanUndo);
        editor.History.Undo(); Assert.Empty(archive.GetAllEntries()); Assert.False(editor.IsDirty);
        editor.History.Redo(); editor.History.Redo(); Assert.Equal(2, archive.GetAllEntries().Count());
        editor.SaveTo(path); Assert.False(editor.IsDirty);
        using var persisted = AssetsFile.LoadOrCreate(path, encrypted ? "secret" : null, encrypted, register: false);
        Assert.Equal(2, persisted.GetAllEntries().Count()); Assert.Equal(encrypted, persisted.UseEncryption);
        Assert.Empty(Directory.GetFiles(root, ".gondwana-save-*"));
    });

    [Theory]
    [InlineData("close")]
    [InlineData("reload")]
    [InlineData("shutdown")]
    public void StudioFailedRefreshCanBeDiscardedClosedReloadedOrShutDown(string action) => RunSta(root =>
    {
        var path = Path.Combine(root, "recovery.gaf");
        using (var initial = AssetsFile.LoadOrCreate(path, null, false, register: false)) initial.Save();
        using var studio = Host(root);
        var document = studio.OpenDocument(path);
        var editor = (AssetEditorControl)document.Document.Editor;
        editor.ApplyImport(HistoryImport(editor, root));
        Assert.True(AwaitUi(studio.SaveDocument(document)));
        AssetDeletionPlan.Create(path, [new(AssetTypes.Misc, path, "nested/new.bin")]).Commit();
        var committed = File.ReadAllBytes(path);
        File.WriteAllText(path, "refresh read failure");
        var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            editor.GetType().GetMethod("RefreshAfterDiskOperation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(editor, ["Deletion committed."]));
        Assert.IsType<IOException>(failure.InnerException);
        File.WriteAllBytes(path, committed); // a subsequent reload can read the committed archive
        Assert.False(editor.CanSave);
        Assert.False(AwaitUi(studio.SaveDocument(document))); Assert.Equal(committed, File.ReadAllBytes(path));
        studio.AskSave = _ => DialogResult.Cancel;
        Assert.False(AwaitUi(studio.ConfirmClose(document))); // retain the existing discard confirmation
        studio.AskSave = _ => DialogResult.Yes;
        Assert.False(AwaitUi(studio.ConfirmClose(document))); // stale buffers cannot be saved through Close
        studio.AskSave = _ => DialogResult.No;
        if (action == "reload")
        {
            studio.ConfirmReload = _ => true;
            Assert.True(studio.ReloadDocument(document));
            var reloaded = studio.OpenDocument(path);
            Assert.NotSame(document, reloaded);
            Assert.True(((AssetEditorControl)reloaded.Document.Editor).CanSave);
            Assert.False(reloaded.Document.Dirty());
        }
        else if (action == "shutdown")
        {
            Assert.True(AwaitUi(studio.ApproveShutdown()));
            studio.Close(); Application.DoEvents(); Assert.True(studio.IsDisposed);
        }
        else
        {
            document.Close(); Application.DoEvents();
            Assert.True(document.IsDisposed); Assert.Empty(studio.Documents);
        }
    });

    [Fact]
    public void GafUnsavedArchiveAndDisposedControlReleaseHistory() => RunSta(root =>
    {
        var path = Path.Combine(root, "unsaved.gaf");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        var editor = new AssetEditorControl(archive);
        Assert.True(editor.IsDirty);
        editor.ApplyImport(HistoryImport(editor, root)); editor.History.Undo(); Assert.True(editor.IsDirty);
        editor.History.Redo(); Assert.True(editor.History.RetainedBytes > 0);
        var history = editor.History;
        editor.Dispose(); Assert.Equal(0, history.RetainedBytes); Assert.False(history.CanUndo);
        Assert.Single(archive.GetAllEntries());
    });
}
