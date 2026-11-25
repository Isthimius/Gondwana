using System.Reflection;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Studio.Animations.WinForms;
using Gondwana.Tooling.Studio.Scenes.WinForms;
using Gondwana.Tooling.Studio.Shared.WinForms;
using Gondwana.Tooling.Studio.Sprites.WinForms;
using Gondwana.Tooling.Studio.Tilesheets.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    private static Type StandaloneEditorType(string format) => format switch
    {
        "gts" => typeof(TilesheetEditorControl),
        "gani" => typeof(AnimationEditorControl),
        "gscn" => typeof(SceneEditorControl),
        "gspr" => typeof(SpriteEditorControl),
        "gsnd" => typeof(AudioEditorControl),
        _ => typeof(Gondwana.Tooling.Studio.Assets.WinForms.AssetEditorControl)
    };

    private static Form StandaloneHost(string path, string format)
    {
        var type = StandaloneEditorType(format).Assembly.GetTypes().Single(t => t.Name == "MainForm");
        var host = (Form)Activator.CreateInstance(type)!;
        host.Show();
        Application.DoEvents();
        type.GetMethod("OpenDocument", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, [path]);
        Application.DoEvents();
        return host;
    }

    private static EditorSourceMonitorController StandaloneMonitor(Form host) =>
        (EditorSourceMonitorController)host.GetType().GetField(
            "_sources",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;

    private static Control StandaloneEditor(Form host, string format) => Descendants(host).Single(c =>
        c.GetType() == StandaloneEditorType(format) && (format == "gaf" || AuthoringModel(c).FilePath is not null));

    private static DockContent StandaloneTab(Control editor)
    {
        for (var control = editor.Parent; control is not null; control = control.Parent)
            if (control is DockContent dock)
                return dock;

        throw new InvalidOperationException("No document tab");
    }

    private static EditorSourceMonitorController.Document StandaloneSources(DockContent tab) =>
        (EditorSourceMonitorController.Document)tab.GetType().GetField(
            "Sources",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tab)!;

    private static void VerifyStandalone(EditorSourceMonitorController monitor)
    {
        Application.DoEvents();
        monitor.Verify();
        Application.DoEvents();
    }

    private static string MemberName(dynamic model, string format) => format == "gts" ? model.Definition.Name :
        format == "gscn" ? model.Definition.ID : model.Definition.Key;

    private static void SaveStandaloneModel(dynamic model, string path, string format)
    {
        if (format == "gts")
            model.Save(path, null, true);
        else
            model.Save(path, true);
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void StandaloneSourceMonitoringCleanDirtyReloadSaveAsAndCleanup(string format) => RunSta(root =>
    {
        var path = Path.Combine(root, "source." + format);
        File.WriteAllText(path, NativeJson(format));
        using var host = StandaloneHost(path, format);
        var monitor = StandaloneMonitor(host);
        monitor.ReportError = ex => throw new InvalidOperationException("Unexpected reload error", ex);
        var editor = StandaloneEditor(host, format);
        dynamic model = AuthoringModel(editor);
        var tab = StandaloneTab(editor);
        var sources = StandaloneSources(tab);
        Assert.True(monitor.RegistrationCount > 0);
        var original = MemberName(model, format);
        File.WriteAllText(path, NativeJson(format).Replace(original, "external"));
        VerifyStandalone(monitor);
        Assert.Contains("[reload]", tab.Text);
        Assert.Equal(original, MemberName(model, format));
        monitor.ConfirmDiscard = () => throw new InvalidOperationException("Clean reload prompted");
        sources.Reload();
        Assert.True(tab.IsDisposed);
        editor = StandaloneEditor(host, format);
        model = AuthoringModel(editor);
        tab = StandaloneTab(editor);
        sources = StandaloneSources(tab);
        Assert.Equal("external", MemberName(model, format));
        Assert.False((bool)model.IsDirty);
        model.RenameDefinition("local");
        EditHistory history = model.History;
        var accepted = (DocumentSourceVersion)model.SourceVersion;
        File.WriteAllText(path, NativeJson(format).Replace(original, "another"));
        VerifyStandalone(monitor);
        Assert.Contains("[conflict]", tab.Text);
        Assert.Equal("local", MemberName(model, format));
        int prompts = 0;

        monitor.ChooseConflict = () =>
        {
            prompts++;
            return DialogResult.Cancel;
        };

        for (int i = 0; i < 2; i++)
        {
            var error = Assert.Throws<DocumentSourceConflictException>(() =>
            {
                SaveStandaloneModel(model, path, format);
            });

            Assert.True(sources.SaveFailed(error));
            Application.DoEvents();
        }

        Assert.Equal(1, prompts);
        var otherDestination = new AssetLocation(AssetTypes.Misc, Path.Combine(root, "occupied." + format));

        Assert.False(
            sources.SaveFailed(
                new DocumentSourceConflictException(
                    otherDestination,
                    new(DocumentSourceStatus.Available, "other", "other"))));

        Application.DoEvents();
        Assert.Equal(1, prompts);
        Assert.Equal(accepted, (DocumentSourceVersion)model.SourceVersion);
        Assert.True(history.CanUndo);
        monitor.ConfirmDiscard = () => false;
        sources.Reload();
        Assert.False(tab.IsDisposed);
        Assert.Same(history, (EditHistory)model.History);
        var copy = Path.Combine(root, "copy." + format);
        SaveStandaloneModel(model, copy, format);
        sources.Saved();
        VerifyStandalone(monitor);
        Assert.False(sources.ContentStale);
        Assert.Contains("another", File.ReadAllText(path));
        Assert.Contains("local", File.ReadAllText(copy));
        model.RenameDefinition("saved");
        SaveStandaloneModel(model, copy, format);
        sources.Saved();
        VerifyStandalone(monitor);
        Assert.False(sources.ContentStale);
        File.WriteAllText(copy, NativeJson(format).Replace(original, "after save"));
        VerifyStandalone(monitor);
        Assert.True(sources.ContentStale);
        model.RenameDefinition("unsaved");
        monitor.ConfirmDiscard = () => true;
        sources.Reload();
        editor = StandaloneEditor(host, format);
        model = AuthoringModel(editor);
        Assert.Equal("after save", MemberName(model, format));
        Assert.False((bool)model.IsDirty);
        Assert.False(((EditHistory)model.History).CanUndo);
        tab = StandaloneTab(editor);
        File.WriteAllText(copy, NativeJson(format));
        monitor.Verify(); // queue then dispose
        tab.Dispose();
        Application.DoEvents();
        Assert.Equal(0, monitor.RegistrationCount);
        host.Dispose();
        Assert.Equal(0, monitor.RegistrationCount);
    });

    [Fact]
    public void StandaloneSourceMonitoringFailedReloadOptOutAndDependencyPreserveHistory() => RunSta(root =>
    {
        var path = Path.Combine(root, "source.gts");
        var image = Path.Combine(root, "image.png");
        File.WriteAllBytes(image, [1, 2, 3]);

        Gondwana.Drawing.Tilesheets.GTS.TilesheetDefinitionSerializer.SaveAll(
            path,
            [new() { Name = "sheet", Image = new() { FilePath = "image.png" } }]);

        using var host = StandaloneHost(path, "gts");
        var monitor = StandaloneMonitor(host);
        var editor = (TilesheetEditorControl)StandaloneEditor(host, "gts");
        var tab = StandaloneTab(editor);
        var sources = StandaloneSources(tab);
        editor.Document.RenameDefinition("local");
        var history = editor.Document.History;
        File.WriteAllBytes(image, [3, 2, 1]);
        VerifyStandalone(monitor);
        Assert.True(sources.PreviewStale);
        Assert.False(sources.ContentStale);
        Assert.True(history.CanUndo);
        sources.RefreshPreviews();
        Assert.Same(history, editor.Document.History);
        monitor.Enabled = false;
        Assert.Equal(0, monitor.RegistrationCount);
        File.WriteAllText(path, "broken");
        Assert.Throws<DocumentSourceConflictException>(() => editor.Document.Save(path, null, true));
        monitor.Enabled = true;
        VerifyStandalone(monitor);
        int failures = 0;
        monitor.ReportError = _ => failures++;
        monitor.ConfirmDiscard = () => true;
        sources.Reload();
        Assert.Equal(1, failures);
        Assert.False(tab.IsDisposed);
        Assert.Same(history, editor.Document.History);
        Assert.True(editor.Document.IsDirty);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneSourceMonitoringArchivesRetainCopySemanticsAndEncryption(bool encrypted) => RunSta(root =>
    {
        var password = encrypted ? "test password" : null;
        var path = Path.Combine(root, "archive.gaf");

        using (var initial = AssetsFile.LoadOrCreate(
                   path,
                   password,
                   encrypted,
                   register: false))
        {
            using var bytes = new MemoryStream([1, 2, 3]);
            initial.Add(AssetTypes.Misc, "initial.bin", bytes);
            initial.Save();
        }

        using var host = new Gondwana.Tooling.Studio.Assets.WinForms.MainForm();
        host.Show();
        Application.DoEvents();
        var version = DocumentSourceVersion.Read(new(AssetTypes.Misc, path));

        var loaded = AssetsFile.LoadOrCreate(
            path,
            password,
            encrypted,
            register: false);

        host.GetType().GetMethod("ShowDocument", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            host,
            [loaded, version]);

        var monitor = StandaloneMonitor(host);
        monitor.ReportError = ex => throw new InvalidOperationException("Archive reload failed", ex);
        var editor = (Gondwana.Tooling.Studio.Assets.WinForms.AssetEditorControl)StandaloneEditor(host, "gaf");
        var tab = StandaloneTab(editor);
        var sources = StandaloneSources(tab);

        void External(byte value)
        {
            using var disk = AssetsFile.LoadOrCreate(
                path,
                password,
                encrypted,
                register: false);

            using var bytes = new MemoryStream([value]);
            disk.Add(AssetTypes.Misc, "external.bin", bytes);

            AssetArchivePersistence.Commit(
                disk,
                path,
                Convert.FromHexString(DocumentSourceVersion.Read(new(AssetTypes.Misc, path)).PhysicalHash!));
        }

        External(1);
        VerifyStandalone(monitor);
        Assert.Contains("[reload]", tab.Text);
        sources.Reload();
        Assert.True(tab.IsDisposed);
        editor = (Gondwana.Tooling.Studio.Assets.WinForms.AssetEditorControl)StandaloneEditor(host, "gaf");
        tab = StandaloneTab(editor);
        sources = StandaloneSources(tab);

        using (var bytes = new MemoryStream([7]))
            editor.AssetsFile.Add(AssetTypes.Misc, "local.bin", bytes);

        editor.MarkChanged();
        External(2);
        VerifyStandalone(monitor);
        Assert.Contains("[conflict]", tab.Text);
        monitor.ChooseConflict = () => DialogResult.Cancel;
        var error = Assert.Throws<DocumentSourceConflictException>(() => editor.SaveTo(path));
        Assert.True(sources.SaveFailed(error));
        Application.DoEvents();
        var history = editor.History;
        var accepted = editor.SourceVersion;
        var copy = Path.Combine(root, "copy.gaf");
        editor.SaveCopyTo(copy, password, encrypted);
        Assert.Equal(path, editor.FilePath);
        Assert.True(editor.IsDirty);
        Assert.Same(history, editor.History);
        Assert.Equal(accepted, editor.SourceVersion);

        using (var disk = AssetsFile.LoadOrCreate(
                   path,
                   password,
                   encrypted,
                   register: false))
            Assert.DoesNotContain(disk.GetAllEntries(), e => e.AssetName == "local.bin");

        using (var disk = AssetsFile.LoadOrCreate(
                   copy,
                   password,
                   encrypted,
                   register: false))
            Assert.Contains(disk.GetAllEntries(), e => e.AssetName == "local.bin");

        monitor.ConfirmDiscard = () => true;
        sources.Reload();
        editor = (Gondwana.Tooling.Studio.Assets.WinForms.AssetEditorControl)StandaloneEditor(host, "gaf");
        tab = StandaloneTab(editor);
        sources = StandaloneSources(tab);

        using (var bytes = new MemoryStream([9]))
            editor.AssetsFile.Add(AssetTypes.Misc, "saved.bin", bytes);

        editor.MarkChanged();
        editor.SaveTo(path);
        VerifyStandalone(monitor);
        Assert.False(sources.ContentStale);
        Assert.False(editor.IsDirty);
        External(3);
        VerifyStandalone(monitor);
        Assert.True(sources.ContentStale);
        host.Dispose();
        Application.DoEvents();
        Assert.Equal(0, monitor.RegistrationCount);
    });

    [Theory]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void StandaloneSourceMonitoringDependenciesAcrossUnselectedMembers(string format) => RunSta(root =>
    {
        var media = Path.Combine(root, "media.png");

        using (var bitmap = new Bitmap(2, 2))
            bitmap.Save(media, System.Drawing.Imaging.ImageFormat.Png);

        var sheet = Path.Combine(root, "sheet.gts");
        TilesheetDefinitionSerializer.SaveAll(sheet, [new() { Name = "sheet", Image = new() { FilePath = media } }]);
        var path = Path.Combine(root, "source." + format);

        var json = format switch
        {
            "gani" => AnimationDefinitionSerializer.ToDocumentJson(
                new()
                {
                    Definitions =
                    [
                        new() { Key = "first" },
                        new()
                        {
                            Key = "second",
                            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", sheet)]
                        }
                    ]
                }),
            "gscn" => SceneDefinitionSerializer.ToDocumentJson(
                new()
                {
                    Definitions =
                    [
                        new() { ID = "first" },
                        new()
                        {
                            ID = "second", TilesheetSources = [SceneTilesheetSourceDefinition.Loose("sheet", sheet)]
                        }
                    ]
                }),
            "gspr" => SpriteDefinitionSerializer.ToDocumentJson(
                new()
                {
                    Definitions =
                    [
                        new() { Key = "first" },
                        new()
                        {
                            Key = "second", TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("sheet", sheet)]
                        }
                    ]
                }),
            _ => AudioDefinitionSerializer.ToDocumentJson(
                new()
                {
                    Definitions =
                    [
                        new() { Key = "first" },
                        new()
                        {
                            Key = "second",
                            Resources =
                            [
                                new() { Key = "local", FilePath = media },
                                new()
                                {
                                    Key = "remote", SourceKind = AudioResourceSourceKind.Uri,
                                    SourceUri = "https://example.test/audio.wav"
                                }
                            ]
                        }
                    ]
                })
        };

        File.WriteAllText(path, json);
        using var host = StandaloneHost(path, format);
        var monitor = StandaloneMonitor(host);
        var editor = StandaloneEditor(host, format);
        dynamic model = AuthoringModel(editor);
        var sources = StandaloneSources(StandaloneTab(editor));
        model.RenameDefinition("local edits");
        EditHistory history = model.History;
        VerifyStandalone(monitor);
        Assert.Equal(format == "gsnd" ? 2 : 3, monitor.RegistrationCount);
        File.WriteAllBytes(media, [7, 8, 9]);
        VerifyStandalone(monitor);
        Assert.True(sources.PreviewStale);
        Assert.False(sources.ContentStale);
        sources.RefreshPreviews();
        Assert.True(history.CanUndo);
        Assert.Same(history, (EditHistory)model.History);
        Assert.Equal("local edits", MemberName(model, format));
        Assert.False(sources.ContentStale);

        if (format != "gsnd")
        {
            using (var locked = new FileStream(
                       sheet,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                model.RenameDefinition("while dependency locked");
                VerifyStandalone(monitor);
                Assert.Equal(3, monitor.RegistrationCount); // Do not lose the transitive image watcher.
            }

            VerifyStandalone(monitor);
            Assert.Equal(3, monitor.RegistrationCount);
        }
    });

    [Fact]
    public void StandaloneSourceMonitoringSharedDependencySelectionAndAdjacentSave() => RunSta(root =>
    {
        var image = Path.Combine(root, "image.png");

        using (var bitmap = new Bitmap(2, 2))
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);

        var first = Path.Combine(root, "first.gts");
        var second = Path.Combine(root, "second.gts");

        foreach (var path in new[] { first, second })
            TilesheetDefinitionSerializer.SaveAll(
                path,
                [
                    new() { Name = "first", Image = new() { FilePath = image } },
                    new() { Name = "second", Image = new() { FilePath = image } }
                ]);

        using var host = StandaloneHost(first, "gts");

        host.GetType().GetMethod("OpenDocument", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(host, [second]);

        var monitor = StandaloneMonitor(host);

        var editors = Descendants(host).OfType<TilesheetEditorControl>().Where(e => e.Document.FilePath is not null)
            .ToArray();

        Assert.Equal(2, editors.Length);
        File.WriteAllBytes(image, [7, 8, 9]);
        VerifyStandalone(monitor);
        Assert.All(editors, editor => Assert.True(StandaloneSources(StandaloneTab(editor)).PreviewStale));
        var current = editors.Single(e => e.Document.FilePath == first);
        current.SelectDefinition(current.Document.Definitions[1]);
        current.PreviewZoom = 2;
        var tab = StandaloneTab(current);
        var pane = tab.Pane;
        StandaloneSources(tab).Reload();
        current = Descendants(host).OfType<TilesheetEditorControl>().Single(e => e.Document.FilePath == first);
        Assert.Equal("second", current.Document.Definition.Name);
        Assert.Equal(2, current.PreviewZoom);
        Assert.Same(pane, StandaloneTab(current).Pane);
        current.Document.RenameDefinition("local");
        bool wrote = false;

        current.Document.History.Changed += (_, _) =>
        {
            if (!current.Document.IsDirty && !wrote)
            {
                wrote = true;
                File.WriteAllText(first, NativeJson("gts", "external"));
            }
        };

        current.Document.Save(first, null, true);
        StandaloneSources(StandaloneTab(current)).Saved();
        VerifyStandalone(monitor);
        Assert.True(wrote);
        Assert.True(StandaloneSources(StandaloneTab(current)).ContentStale);
        host.Dispose();
        Assert.Equal(0, monitor.RegistrationCount);
    });

    [Fact]
    public void StandaloneSourceMonitoringPackedDependencyUsesExactTypedEntry() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var sheet = PreviewSheet("terrain");
        sheet.Image = new() { AssetsFilePath = archive, AssetEntryName = "images/atlas.png" };
        var path = Path.Combine(root, "packed-image.gts");
        TilesheetDefinitionSerializer.SaveAll(path, [sheet]);
        using var host = StandaloneHost(path, "gts");
        var monitor = StandaloneMonitor(host);
        var editor = (TilesheetEditorControl)StandaloneEditor(host, "gts");
        var sources = StandaloneSources(StandaloneTab(editor));
        editor.Document.RenameDefinition("local");
        VerifyStandalone(monitor);

        using (var disk = AssetsFile.LoadOrCreate(
                   archive,
                   null,
                   false,
                   register: false))
        {
            using var bytes = new MemoryStream([1, 2, 3]);
            disk.Add(AssetTypes.Misc, "images/atlas.png", bytes);
            disk.Save();
        }

        VerifyStandalone(monitor);
        Assert.False(sources.ContentStale);
        Assert.False(sources.PreviewStale);
        ReplacePreviewImage(root, archive, Color.Blue);
        VerifyStandalone(monitor);
        Assert.True(sources.PreviewStale);
        Assert.False(sources.ContentStale);
        sources.RefreshPreviews();
        Assert.False(sources.PreviewStale);
        Assert.True(editor.Document.IsDirty);
        Assert.Equal("local", editor.Document.Definition.Name);
        Assert.True(editor.Document.History.CanUndo);
    });

    [Theory]
    [InlineData("gts")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void StandaloneSourceMonitoringReloadPreservesCaseSensitiveMember(string format) => RunSta(root =>
    {
        var path = Path.Combine(root, "case." + format);
        File.WriteAllText(path, NativeJson(format).Replace("sibling", "First"));
        using var host = StandaloneHost(path, format);
        var editor = StandaloneEditor(host, format);
        dynamic model = AuthoringModel(editor);
        model.SelectDefinition(model.Definitions[1]);
        Assert.Equal("First", MemberName(model, format));
        File.AppendAllText(path, "\n");
        VerifyStandalone(StandaloneMonitor(host));
        StandaloneSources(StandaloneTab(editor)).Reload();
        Assert.Equal("First", MemberName(AuthoringModel(StandaloneEditor(host, format)), format));
    });

    [Fact]
    public void StandaloneSourceMonitoringIncompleteFirstMemberStillWatchesLaterImage() => RunSta(root =>
    {
        var image = Path.Combine(root, "later.png");
        WritePreviewImage(image, Color.Red);
        var path = Path.Combine(root, "partial.gts");

        TilesheetDefinitionSerializer.SaveAll(
            path,
            [new() { Name = "incomplete" }, new() { Name = "valid", Image = new() { FilePath = image } }]);

        using var host = StandaloneHost(path, "gts");
        var monitor = StandaloneMonitor(host);
        var editor = StandaloneEditor(host, "gts");
        Assert.Equal(2, monitor.RegistrationCount);
        WritePreviewImage(image, Color.Blue);
        VerifyStandalone(monitor);
        Assert.True(StandaloneSources(StandaloneTab(editor)).PreviewStale);
    });

    [Fact]
    public void StandaloneSourceMonitoringFailedPreviewRefreshRetainsWarningUntilSuccess() => RunSta(root =>
    {
        var image = Path.Combine(root, "image.png");
        WritePreviewImage(image, Color.Red);
        var path = Path.Combine(root, "sheet.gts");
        TilesheetDefinitionSerializer.SaveAll(path, [new() { Name = "sheet", Image = new() { FilePath = image } }]);
        using var host = StandaloneHost(path, "gts");
        var monitor = StandaloneMonitor(host);
        var editor = (TilesheetEditorControl)StandaloneEditor(host, "gts");
        var sources = StandaloneSources(StandaloneTab(editor));
        editor.Document.RenameDefinition("local");
        File.WriteAllText(image, "not an image");
        VerifyStandalone(monitor);
        sources.RefreshPreviews();
        Assert.True(sources.PreviewStale);
        sources.RefreshPreviews();
        Assert.True(sources.PreviewStale);
        File.Delete(image);
        VerifyStandalone(monitor);
        sources.RefreshPreviews();
        Assert.True(sources.PreviewStale);
        sources.RefreshPreviews();
        Assert.True(sources.PreviewStale);
        WritePreviewImage(image, Color.Blue);
        VerifyStandalone(monitor);
        sources.RefreshPreviews();
        Assert.False(sources.PreviewStale);
        Assert.True(editor.Document.IsDirty);
        Assert.True(editor.Document.History.CanUndo);
    });
}
