extern alias studio;

using System.Text;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.Tilesheets.WinForms;
using WorkspaceSessionStore = studio::Gondwana.Tooling.WinForms.WorkspaceSessionStore;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void PackedNativeReferenceMetadataSurvivesSaveAndRebasesOnlyOnSaveAs(string format) => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var json = format switch
        {
            "gani" => AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [PreviewAnimation("assets.gaf")] }),
            "gspr" => SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = [new()
            {
                Key = "actors",
                TilesheetSources = [SpriteTilesheetSourceDefinition.Packed("terrain", "assets.gaf", "definitions/world.gts")],
                SceneSources = [SpriteSceneSourceDefinition.Packed("island", "assets.gaf", "definitions/island.gscn")],
                AnimationSources = [SpriteAnimationSourceDefinition.Packed("water.flow", "assets.gaf", "definitions/water.gani")]
            }] }),
            _ => AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = "audio", Resources = [new()
            {
                Key = "sound", SourceKind = AudioResourceSourceKind.PackedAsset,
                AssetsFilePath = "assets.gaf", AssetEntryName = "sound.wav"
            }] }] })
        };
        var entry = "references." + format;
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(json));
            archive.Add(StudioDocument.TypeFor(format), entry, bytes);
            archive.Save();
        }
        using var studio = Host(root);
        var child = studio.OpenDocument(new AssetLocation(StudioDocument.TypeFor(format), path, entry));
        Assert.False(child.Document.Dirty());
        RenameFirst(child.Document, "renamed");
        var expected = child.Document.SerializePacked!();
        Assert.True(AwaitUi(studio.SaveDocument(child)));
        using (var saved = StudioArchiveSnapshot.Open(path, null))
            Assert.Equal(expected, StudioArchiveSnapshot.Entry(saved.Archive, child.Document.Location!));
        Assert.Contains("assets.gaf", Encoding.UTF8.GetString(expected));
        var original = File.ReadAllBytes(path);
        var destination = Path.Combine(root, "copies", entry);
        Assert.True(AwaitUi(studio.SaveDocument(child, saveAs: true, destination: destination)));
        Assert.Contains("../assets.gaf", File.ReadAllText(destination).Replace(@"\\", "/"));
        Assert.Equal(original, File.ReadAllBytes(path));
    });

    private static void SelectPackedMember(Control editor, string identity)
    {
        var selector = editor.Controls.OfType<ToolStrip>().Single(strip => strip.GetType().Name.StartsWith("DefinitionSelectionBar"));
        selector.Items.OfType<ToolStripComboBox>().Single().SelectedItem = identity;
        Application.DoEvents();
    }
    [Fact]
    public void PackedSceneSavePreservesReferencesAndDependencyReloadRendersSavedTilesheet() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        WritePreviewImage(Path.Combine(root, "blue.png"), Color.Blue);
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            archive.Add(AssetTypes.Image, Path.Combine(root, "blue.png"), "blue.png");
            archive.Save();
        }
        foreach (var loose in new[] { "world.gts", "water.gani", "island.gscn", "atlas.png", "blue.png" }) File.Delete(Path.Combine(root, loose));
        using var studio = Host(root);
        var scene = studio.OpenDocument(new AssetLocation(AssetTypes.SceneDefinition, path, "definitions/island.gscn"));
        var scenes = (SceneEditorControl)scene.Document.Editor;
        SelectPackedMember(scenes, "island");
        AssertSourceImages(Private(scenes, "_tilesheetSources"), Color.LimeGreen);
        scenes.Document.Definition.Layers[0].ID = "edited-ground";
        scenes.Document.MarkChanged();
        var expected = scene.Document.SerializePacked!();
        Assert.True(AwaitUi(studio.SaveDocument(scene)));
        using (var saved = StudioArchiveSnapshot.Open(path, null))
            Assert.Equal(expected, StudioArchiveSnapshot.Entry(saved.Archive, scene.Document.Location!));
        Assert.Equal("assets.gaf", scenes.Document.Definition.TilesheetSources[0].AssetsFilePath);
        Assert.Equal(SceneTilesheetSourceKind.PackedDefinitionFile, scenes.Document.Definition.TilesheetSources[0].Kind);
        var tiles = studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, "definitions/world.gts"));
        var sheets = (TilesheetEditorControl)tiles.Document.Editor;
        SelectPackedMember(sheets, "terrain");
        AssertPreviewColor(Property<Bitmap>(Private(sheets, "_viewport"), "Image"), Color.LimeGreen);
        sheets.Document.Definition.Image!.AssetEntryName = "blue.png";
        sheets.Document.MarkChanged();
        Assert.True(AwaitUi(studio.SaveDocument(tiles)));
        Assert.True(scene.Document.PreviewStale);
        Assert.False(scene.Document.ContentStale);
        studio.ReloadPreviews(scene);
        Assert.False(scene.Document.PreviewStale);
        Assert.False(scene.Document.Dirty());
        AssertSourceImages(Private(scenes, "_tilesheetSources"), Color.Blue);
        AssertRenderedColor((Control)Private(scenes, "_preview"), Color.Blue);
        Assert.Equal(expected, scene.Document.SerializePacked!());
    });

    [Fact]
    public void PackedSaveAsRebasesDependenciesFromArchiveDirectoryAndKeepsImplicitImageProvenance() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using var studio = Host(root);
        foreach (var (format, entry) in new[] { ("gts", "world"), ("gani", "water"), ("gscn", "island") })
        {
            var document = studio.OpenDocument(new AssetLocation(StudioDocument.TypeFor(format), path, $"definitions/{entry}.{format}"));
            var original = File.ReadAllBytes(path);
            var destination = Path.Combine(root, "copies", entry + "." + format);
            Assert.True(AwaitUi(studio.SaveDocument(document, saveAs: true, destination: destination)));
            Assert.Equal(original, File.ReadAllBytes(path));
            var json = File.ReadAllText(destination);
            Assert.Contains("../assets.gaf", json.Replace(@"\\", "/"));
            Assert.DoesNotContain(".gondwana-save-", json);
            if (document.Document.Editor is TilesheetEditorControl sheets)
            {
                SelectPackedMember(sheets, "terrain");
                Assert.Equal("../assets.gaf", sheets.Document.Definition.Image!.AssetsFilePath!.Replace('\\', '/'));
                AssertPreviewColor(Property<Bitmap>(Private(sheets, "_viewport"), "Image"), Color.LimeGreen);
            }
        }
    });

    [Theory]
    [InlineData("save")]
    [InlineData("close")]
    [InlineData("shutdown")]
    public void PackedSaveWaitsForActiveScanWithoutBlockingUiOrClosingDirtyDocument(string command) => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        WaitForArchives(studio.Browser);
        var child = studio.OpenDocument(NativeLocation(path, "gsnd"));
        RenameFirst(child.Document, "persisted");
        using var opened = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        studio.Browser.ArchiveOpened = _ => { opened.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(15))); };
        studio.Browser.RefreshDirectory();
        Assert.True(opened.Wait(TimeSpan.FromSeconds(10)));
        try
        {
            studio.AskSave = _ => DialogResult.Yes;
            if (command == "close") child.Close();
            if (command == "shutdown") studio.Close();
            var save = studio.SaveDocument(child);
            Assert.False(save.IsCompleted);
            Assert.False(child.IsDisposed);
            Assert.True(child.Document.Dirty());
            bool responsive = false;
            studio.BeginInvoke(() => responsive = true);
            Application.DoEvents();
            Assert.True(responsive);
            Assert.False(save.IsCompleted);
            studio.Browser.ArchiveOpened = null;
            release.Set();
            Assert.True(AwaitUi(save));
            Application.DoEvents();
            if (command != "save") Assert.True(child.IsDisposed);
            if (command == "shutdown") Assert.True(studio.IsDisposed);
            using var saved = StudioArchiveSnapshot.Open(path, null);
            Assert.Contains("persisted", Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(saved.Archive, NativeLocation(path, "gsnd"))));
        }
        finally { studio.Browser.ArchiveOpened = null; release.Set(); }
    });

    [Fact]
    public void EncryptedPackedEntriesReuseExplicitAuthenticationAndRemainEncryptedAfterSaving() => RunSta(root =>
    {
        var path = NativeArchive(root, password: "secret");
        using var studio = Host(root);
        int prompts = 0;
        studio.AssetPassword = _ => { prompts++; return "secret"; };
        WaitForArchives(studio.Browser);
        Assert.Equal(0, prompts);
        var parent = studio.OpenDocument(path);
        Assert.Equal(1, prompts);
        WaitForArchives(studio.Browser);
        var node = Assert.Single(studio.Browser.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>());
        DoubleClickNode(studio.Browser, node);
        var child = studio.ActiveDocument!;
        Assert.NotSame(parent, child);
        RenameFirst(child.Document, "encrypted-edit");
        Assert.True(AwaitUi(studio.SaveDocument(child)));
        WaitForArchives(studio.Browser);
        Assert.Equal(1, prompts);
        Assert.ThrowsAny<Exception>(() => StudioArchiveSnapshot.Open(path, null));
        using var saved = StudioArchiveSnapshot.Open(path, "secret");
        Assert.True(saved.Archive.UseEncryption);
        Assert.Contains("encrypted-edit", Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(saved.Archive, NativeLocation(path, "gts"))));
    });

    [Fact]
    public void PackedOpenRejectsMissingMistypedInvalidAndCancelledEncryptedEntries() => RunSta(root =>
    {
        var path = NativeArchive(root);
        var encrypted = NativeArchive(root, "secret.gaf", "secret");
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("broken json"));
            archive.Add(AssetTypes.SceneDefinition, "invalid.gscn", invalid);
            archive.Save();
        }
        using var studio = Host(root);
        Assert.Throws<FileNotFoundException>(() => studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, "same.gts")));
        Assert.Throws<FileNotFoundException>(() => studio.OpenDocument(new AssetLocation(AssetTypes.AnimationDefinition, path, "nested/same.gts")));
        Assert.ThrowsAny<Exception>(() => studio.OpenDocument(new AssetLocation(AssetTypes.SceneDefinition, path, "invalid.gscn")));
        studio.AssetPassword = _ => null;
        Assert.Throws<OperationCanceledException>(() => studio.OpenDocument(NativeLocation(encrypted, "gsnd")));
        studio.AssetPassword = _ => "wrong";
        Assert.ThrowsAny<Exception>(() => studio.OpenDocument(NativeLocation(encrypted, "gsnd")));
        Assert.Empty(studio.Documents);
    });

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void PackedSaveAsAdoptsLooseLocationAndLeavesArchiveUntouched(string format) => RunSta(root =>
    {
        var path = NativeArchive(root);
        var original = File.ReadAllBytes(path);
        using var studio = Host(root);
        var child = studio.OpenDocument(NativeLocation(path, format));
        RenameFirst(child.Document, "loose-edit");
        var destination = Path.Combine(root, "copy", "saved." + format);
        Assert.True(AwaitUi(studio.SaveDocument(child, saveAs: true, destination: destination)));
        Assert.Null(child.Document.PackedLocation());
        Assert.Equal(destination, child.Document.Path());
        Assert.False(child.Document.Dirty());
        Assert.Same(child, studio.OpenDocument(destination));
        Assert.Equal(original, File.ReadAllBytes(path));
        var packed = studio.OpenDocument(NativeLocation(path, format));
        Assert.NotSame(child, packed);
        Assert.Equal(NativeJson(format), Encoding.UTF8.GetString(packed.Document.SerializePacked!()));
        Assert.Equal(NativeJson(format, "loose-edit"), File.ReadAllText(destination));
    });

    [Fact]
    public void PackedSessionRestoresTypedEntriesAndSkipsRemovedEntry() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using (var studio = Host(root))
        {
            studio.OpenDocument(path);
            studio.OpenDocument(NativeLocation(path, "gts"));
            studio.OpenDocument(NativeLocation(path, "gsnd"));
            studio.Close();
        }
        var store = new WorkspaceSessionStore(_ => { });
        Assert.Equal(3, store.Read().Documents.Length);
        Assert.Equal("nested/same.gsnd", store.Read().ActivePackedDocument!.EntryName);
        using (var studio = Host(root))
        {
            Assert.Equal(3, studio.Documents.Count);
            Assert.Equal("nested/same.gsnd", studio.ActiveDocument!.Document.PackedLocation()!.AssetEntryName);
            studio.Close();
        }
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            archive.Remove(AssetTypes.TilesheetDefinition, "nested/same.gts");
            archive.Save();
        }
        using var restored = Host(root);
        Assert.Equal(2, restored.Documents.Count);
        Assert.Contains(restored.Documents, doc => doc.Document.Extension == "gsnd");
    });

    private static string NativeJson(string format, string first = "first") => format switch
    {
        "gts" => TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Name = first }, new() { Name = "sibling" }] }),
        "gani" => AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = first }, new() { Key = "sibling" }] }),
        "gscn" => SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { ID = first }, new() { ID = "sibling" }] }),
        "gspr" => SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = first }, new() { Key = "sibling" }] }),
        "gsnd" => AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = first }, new() { Key = "sibling" }] }),
        _ => throw new ArgumentException(format)
    };

    private static void AddNative(AssetsFile archive, string format, string entry, string first = "first")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(NativeJson(format, first)));
        archive.Add(StudioDocument.TypeFor(format), entry, stream);
    }

    private static string NativeArchive(string root, string name = "assets.gaf", string? password = null)
    {
        var path = Path.Combine(root, name);
        using var archive = AssetsFile.LoadOrCreate(path, password, password is not null, register: false);
        foreach (var format in new[] { "gts", "gani", "gscn", "gspr", "gsnd" }) AddNative(archive, format, "nested/same." + format);
        using var bytes = new MemoryStream([42, 11, 7]);
        archive.Add(AssetTypes.Misc, "keep.bin", bytes);
        archive.Save();
        return path;
    }

    private static AssetLocation NativeLocation(string path, string format) => new(StudioDocument.TypeFor(format), path, "nested/same." + format);

    private static void RenameFirst(StudioDocument model, string name)
    {
        switch (model.Editor)
        {
            case TilesheetEditorControl e: e.Document.Definition.Name = name; e.Document.MarkChanged(); break;
            case AnimationEditorControl e: e.Document.Definition.Key = name; e.Document.MarkChanged(); break;
            case SceneEditorControl e: e.Document.Definition.ID = name; e.Document.MarkChanged(); break;
            case SpriteEditorControl e: e.Document.Definition.Key = name; e.Document.MarkChanged(); break;
            case AudioEditorControl e: e.Document.Definition.Key = name; e.Document.MarkChanged(); break;
        }
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void PackedNativeDocumentsRoundTripAllMembersAndKeepLooseDuplicates(string format) => RunSta(root =>
    {
        var path = NativeArchive(root);
        var second = NativeArchive(root, "second.gaf");
        var loose = Path.Combine(root, "same." + format);
        File.WriteAllText(loose, NativeJson(format));
        var originalLoose = File.ReadAllBytes(loose);
        using var studio = Host(root);
        var location = NativeLocation(path, format);
        WaitForArchives(studio.Browser);
        var node = studio.Browser.Tree.Nodes[0].Nodes.Cast<TreeNode>().SelectMany(group => group.Nodes.Cast<TreeNode>())
            .Single(node => node.Tag is PackedAssetNodeIdentity packed && packed.ArchivePath == path && packed.AssetType == location.AssetType);
        Assert.True(studio.Browser.PrepareFileMenu(node));
        studio.Browser.FileMenu.Items[0].PerformClick();
        var document = Assert.Single(studio.Documents);
        DoubleClickNode(studio.Browser, node);
        Assert.Same(document, Assert.Single(studio.Documents));
        Assert.False(document.Document.Dirty());
        Assert.Same(document, studio.OpenDocument(location));
        Assert.NotSame(document, studio.OpenDocument(NativeLocation(second, format)));
        Assert.NotSame(document, studio.OpenDocument(loose));
        Assert.Contains("nested/same.", document.Text);
        Assert.Contains(path, document.ToolTipText);
        Assert.Contains(location.AssetEntryName!, document.ToolTipText);
        Assert.Contains(location.AssetType.ToString(), document.ToolTipText);
        using var before = StudioArchiveSnapshot.Open(path, null);
        var unchanged = before.Archive.GetAllEntries().Where(e => e.AssetType != location.AssetType)
            .ToDictionary(e => e, e => StudioArchiveSnapshot.Entry(before.Archive, new(e.AssetType, path, e.AssetName)));
        RenameFirst(document.Document, "edited");
        Assert.True(document.Document.Dirty());
        Assert.True(AwaitUi(studio.SaveDocument(document)));
        Assert.False(document.Document.Dirty());
        Assert.Equal(originalLoose, File.ReadAllBytes(loose));
        using var saved = StudioArchiveSnapshot.Open(path, null);
        Assert.Equal(NativeJson(format, "edited"), Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(saved.Archive, location)));
        foreach (var pair in unchanged)
            Assert.Equal(pair.Value, StudioArchiveSnapshot.Entry(saved.Archive, new(pair.Key.AssetType, path, pair.Key.AssetName)));
        document.Close();
        var reopened = studio.OpenDocument(location);
        Assert.Equal(NativeJson(format, "edited"), Encoding.UTF8.GetString(reopened.Document.SerializePacked!()));
        Assert.False(reopened.Document.Dirty());
        Assert.False(Directory.Exists(Path.Combine(root, "nested")));
    });

    [Fact]
    public void PackedSavesCoordinateChildrenAndDirtyParentWithoutLosingChanges() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var parent = studio.OpenDocument(path);
        var asset = (AssetEditorControl)parent.Document.Editor;
        var a = studio.OpenDocument(NativeLocation(path, "gts"));
        var b = studio.OpenDocument(NativeLocation(path, "gani"));
        RenameFirst(a.Document, "tiles-edited");
        RenameFirst(b.Document, "animation-edited");
        Assert.True(AwaitUi(studio.SaveDocument(a)));
        Assert.True(b.Document.Dirty());
        Assert.True(AwaitUi(studio.SaveDocument(b)));
        Assert.Contains("tiles-edited", Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(asset.AssetsFile, NativeLocation(path, "gts"))));
        using var payload = new MemoryStream([9]);
        asset.AssetsFile.Add(AssetTypes.Misc, "parent.bin", payload);
        asset.MarkChanged();
        RenameFirst(a.Document, "later");
        Exception? error = null;
        studio.ReportError = ex => error = ex;
        Assert.False(AwaitUi(studio.SaveDocument(a)));
        Assert.Contains("unsaved", error!.Message);
        Assert.True(a.Document.Dirty());
        Assert.True(parent.Document.Dirty());
        Assert.True(AwaitUi(studio.SaveDocument(parent)));
        Assert.True(AwaitUi(studio.SaveDocument(a)));
        using var saved = StudioArchiveSnapshot.Open(path, null);
        Assert.Equal(new byte[] { 9 }, StudioArchiveSnapshot.Entry(saved.Archive, new(AssetTypes.Misc, path, "parent.bin")));
        Assert.Contains("animation-edited", Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(saved.Archive, NativeLocation(path, "gani"))));
    });

    [Fact]
    public void ParentEntryConflictIsNotHiddenByPreviewReloadAndDirtyReloadCanBeCancelled() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var parent = studio.OpenDocument(path);
        var child = studio.OpenDocument(NativeLocation(path, "gsnd"));
        RenameFirst(child.Document, "child-edited");
        var asset = (AssetEditorControl)parent.Document.Editor;
        AddNative(asset.AssetsFile, "gsnd", "nested/same.gsnd", "parent-edited");
        asset.MarkChanged();
        Assert.True(AwaitUi(studio.SaveDocument(parent)));
        Assert.True(child.Document.ContentStale);
        studio.ReloadPreviews(child);
        Assert.True(child.Document.ContentStale);
        var before = File.ReadAllBytes(path);
        studio.ReportError = _ => { };
        Assert.False(AwaitUi(studio.SaveDocument(child)));
        Assert.True(child.Document.Dirty());
        Assert.Equal(before, File.ReadAllBytes(path));
        studio.ConfirmReload = _ => false;
        Assert.False(studio.ReloadDocument(child));
        Assert.False(child.IsDisposed);
        studio.ConfirmReload = _ => true;
        Assert.True(studio.ReloadDocument(child));
        Assert.Contains("parent-edited", Encoding.UTF8.GetString(studio.ActiveDocument!.Document.SerializePacked!()));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedStagingOrExternalChangesLeaveOriginalArchiveAndDirtyDocumentIntact(bool external) => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var child = studio.OpenDocument(NativeLocation(path, "gts"));
        RenameFirst(child.Document, "edited");
        if (external)
        {
            WaitForArchives(studio.Browser);
            using var changed = AssetsFile.LoadOrCreate(path, null, false, register: false);
            AddNative(changed, "gani", "external.gani");
            changed.Save();
        }
        else studio.BeforeArchiveInstall = () => throw new IOException("Injected failure after staging.");
        var before = File.ReadAllBytes(path);
        studio.ReportError = _ => { };
        Assert.False(AwaitUi(studio.SaveDocument(child)));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.True(child.Document.Dirty());
        Assert.Empty(Directory.GetFiles(root, ".gondwana-save-*"));
        studio.AskSave = _ => DialogResult.Yes;
        Assert.False(AwaitUi(studio.ApproveShutdown()));
        Assert.False(child.IsDisposed);
    });

    [Fact]
    public void PackedValidationAndSaveAsCancellationDoNotWriteOrClearDirtyState() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var child = studio.OpenDocument(NativeLocation(path, "gts"));
        RenameFirst(child.Document, "edited");
        var before = File.ReadAllBytes(path);
        studio.AllowInvalidSave = _ => false;
        Assert.False(AwaitUi(studio.SaveDocument(child)));
        studio.SavePath = _ => null;
        Assert.False(AwaitUi(studio.SaveDocument(child, saveAs: true)));
        Assert.True(child.Document.Dirty());
        Assert.Equal(before, File.ReadAllBytes(path));
    });
}
