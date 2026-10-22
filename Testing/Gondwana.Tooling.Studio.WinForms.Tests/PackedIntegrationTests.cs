extern alias studio;

using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Tilesheets.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;
using WorkspaceSessionDocument = studio::Gondwana.Tooling.WinForms.WorkspaceSessionDocument;
using WorkspaceSessionStore = studio::Gondwana.Tooling.WinForms.WorkspaceSessionStore;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void ArchiveReplacementSharingFailurePreservesBytesAndEditsAndCanBeRetried() => RunSta(root =>
    {
        var path = NativeArchive(root);
        var original = File.ReadAllBytes(path);
        using var studio = Host(root);
        var document = studio.OpenDocument(NativeLocation(path, "gts"));
        RenameFirst(document.Document, "edited");
        var edited = document.Document.SerializePacked!();
        Exception? failure = null;
        studio.ReportError = ex => failure = ex;
        FileStream? blocker = null;
        studio.BeforeArchiveInstall = () => blocker = File.OpenRead(path); // Denies File.Replace, after successful staging.
        try { Assert.False(AwaitUi(studio.SaveDocument(document))); }
        finally { blocker?.Dispose(); }
        Assert.IsType<IOException>(failure);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.True(document.Document.Dirty());
        Assert.Equal(edited, document.Document.SerializePacked!());
        Assert.Empty(Directory.GetFiles(root, ".gondwana-save-*"));
        studio.BeforeArchiveInstall = null;
        Assert.True(AwaitUi(studio.SaveDocument(document)));
        using var saved = StudioArchiveSnapshot.Open(path, null);
        Assert.Equal(edited, StudioArchiveSnapshot.Entry(saved.Archive, NativeLocation(path, "gts")));
        Assert.Equal(new byte[] { 42, 11, 7 }, StudioArchiveSnapshot.Entry(saved.Archive, new(AssetTypes.Misc, path, "keep.bin")));
    });

    [Fact]
    public void PackedTilesheetSaveRefreshesDirtyAnimationSceneAndSpritePreviewsWithoutReplacingEdits() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var spritePath = Path.Combine(root, "actors.gspr");
        SpriteDefinitionSerializer.Save(spritePath, new SpriteDefinition
        {
            Key = "actors",
            TilesheetSources = [SpriteTilesheetSourceDefinition.Packed("terrain", "assets.gaf", "definitions/world.gts")],
            SceneSources = [SpriteSceneSourceDefinition.Packed("island", "assets.gaf", "definitions/island.gscn")],
            AnimationSources = [SpriteAnimationSourceDefinition.Packed("water.flow", "assets.gaf", "definitions/water.gani")],
            Sprites = [new() { Nickname = "hero", SceneId = "island", SceneLayerId = "ground",
                Frame = new() { Tilesheet = "terrain", RegionName = "default" },
                AnimationKey = "water.flow", StartAnimation = true }]
        });
        WritePreviewImage(Path.Combine(root, "blue.png"), Color.Blue);
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            archive.Add(AssetTypes.SpriteDefinition, spritePath, "actors.gspr");
            archive.Add(AssetTypes.Image, Path.Combine(root, "blue.png"), "blue.png");
            archive.Save();
        }
        foreach (var file in Directory.GetFiles(root).Where(file => !file.EndsWith(".gaf"))) File.Delete(file);
        using var studio = Host(root);
        var animation = studio.OpenDocument(new AssetLocation(AssetTypes.AnimationDefinition, path, "definitions/water.gani"));
        SelectPackedMember(animation.Document.Editor, "water.flow");
        var scene = studio.OpenDocument(new AssetLocation(AssetTypes.SceneDefinition, path, "definitions/island.gscn"));
        SelectPackedMember(scene.Document.Editor, "island");
        var sprite = studio.OpenDocument(new AssetLocation(AssetTypes.SpriteDefinition, path, "actors.gspr"));
        var dependents = new[] { animation, scene, sprite };
        foreach (var document in dependents) RenameFirst(document.Document, "unsaved-member");
        var edits = dependents.Select(document => document.Document.SerializePacked!()).ToArray();
        var tilesheet = studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, "definitions/world.gts"));
        SelectPackedMember(tilesheet.Document.Editor, "terrain");
        var editor = (TilesheetEditorControl)tilesheet.Document.Editor;
        editor.Document.Definition.Image!.AssetEntryName = "blue.png";
        editor.Document.MarkChanged();
        Assert.True(AwaitUi(studio.SaveDocument(tilesheet)));
        for (int i = 0; i < dependents.Length; i++)
        {
            var document = dependents[i];
            Assert.True(document.Document.PreviewStale);
            Assert.False(document.Document.ContentStale);
            studio.ReloadPreviews(document);
            Assert.True(document.Document.Dirty());
            Assert.Equal(edits[i], document.Document.SerializePacked!());
            Assert.False(document.Document.PreviewStale);
            AssertSourceImages(Private(document.Document.Editor, i == 1 ? "_tilesheetSources" : "_sources"), Color.Blue);
            AssertRenderedColor((Control)Private(document.Document.Editor, "_preview"), Color.Blue);
        }
        // Buffered previews and document snapshots must not retain archive handles.
        WaitForArchives(studio.Browser);
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public void VerifiedPreviewPasswordSurvivesReloadAndPackedDocumentOpen() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "encrypted.gaf", Color.LimeGreen);
        using (var encrypted = AssetsFile.LoadOrCreate(path, "secret", true, register: false)) encrypted.Save();
        var loose = Path.Combine(root, "preview.gani");
        AnimationDefinitionSerializer.Save(loose, PreviewAnimation("encrypted.gaf"));
        using var studio = Host(root);
        int prompts = 0;
        studio.AssetPassword = _ => { prompts++; return "secret"; };
        var animation = studio.OpenDocument(loose);
        AssertSourceImages(Private(animation.Document.Editor, "_sources"), Color.LimeGreen);
        Assert.Equal(1, prompts);
        studio.ReloadPreviews(animation);
        AssertSourceImages(Private(animation.Document.Editor, "_sources"), Color.LimeGreen);
        studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, "definitions/world.gts"));
        Assert.Equal(1, prompts);
        Assert.False(animation.Document.Dirty());

        // Replacing the archive with a different password must not poison later retries.
        using (var original = AssetsFile.LoadOrCreate(path, "secret", true, register: false))
        using (var replacement = AssetsFile.LoadOrCreate(Path.Combine(root, "replacement.gaf"), "new-secret", true, register: false))
        {
            foreach (var entry in original.GetAllEntries())
            {
                using var bytes = original.Get(entry.AssetType, entry.AssetName)!;
                replacement.Add(entry.AssetType, entry.AssetName, bytes);
            }
            replacement.Save();
        }
        WaitForArchives(studio.Browser);
        File.Move(Path.Combine(root, "replacement.gaf"), path, overwrite: true);
        studio.ReloadPreviews(animation); // The formerly verified credential is now rejected.
        studio.AssetPassword = _ => { prompts++; return "new-secret"; };
        studio.ReloadPreviews(animation);
        Assert.Equal(2, prompts);
        AssertSourceImages(Private(animation.Document.Editor, "_sources"), Color.LimeGreen);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public void RestorationDoesNotRepeatFailedArchiveAuthenticationAndManualOpenCanRetry(string? answer) => RunSta(root =>
    {
        var path = NativeArchive(root, password: "secret");
        var loose = Path.Combine(root, "loose.gts");
        File.WriteAllText(loose, NativeJson("gts"));
        using (var first = Host(root))
        {
            first.AssetPassword = _ => "secret";
            first.OpenDocument(NativeLocation(path, "gts"));
            first.OpenDocument(NativeLocation(path, "gani"));
            first.OpenDocument(loose);
            first.Close();
        }
        using var restored = new MainForm(loadPlugins: false) { Opacity = 0, ShowInTaskbar = false };
        int prompts = 0;
        restored.AssetPassword = _ => { prompts++; return answer; };
        restored.Show();
        Application.DoEvents();
        Assert.Equal(1, prompts);
        Assert.Equal(loose, Assert.Single(restored.Documents).Document.Path());
        restored.AssetPassword = _ => { prompts++; return "secret"; };
        var retry = restored.OpenDocument(NativeLocation(path, "gts"));
        Assert.Equal(2, prompts);
        Assert.False(retry.Document.Dirty());
    });

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "wrong")]
    [InlineData(false, null)]
    [InlineData(false, "wrong")]
    public void RestorationSharesFailedAuthenticationAcrossPackedDocumentsAndPreviews(bool packedFirst, string? answer) => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "encrypted.gaf", Color.LimeGreen);
        using (var encrypted = AssetsFile.LoadOrCreate(path, "secret", true, register: false)) encrypted.Save();
        var loose = Path.Combine(root, "loose.gani");
        AnimationDefinitionSerializer.Save(loose, PreviewAnimation("encrypted.gaf"));
        var packedDocument = new WorkspaceSessionDocument(Path.GetFullPath(path), "gts",
            (int)AssetTypes.TilesheetDefinition, "definitions/world.gts");
        var looseDocument = WorkspaceSessionDocument.FromPath(loose)!;
        var store = new WorkspaceSessionStore(_ => { });
        Assert.True(store.Write(new()
        {
            WorkingDirectory = root,
            Documents = packedFirst ? [packedDocument, looseDocument] : [looseDocument, packedDocument]
        }));

        using var restored = new MainForm(loadPlugins: false) { Opacity = 0, ShowInTaskbar = false };
        int prompts = 0;
        restored.AssetPassword = _ => { prompts++; return answer; };
        restored.Show();
        Application.DoEvents();
        Assert.Equal(1, prompts);
        var animation = Assert.Single(restored.Documents, document => document.Document.Path() == loose);

        restored.AssetPassword = _ => { prompts++; return "secret"; };
        restored.ReloadPreviews(animation);
        Assert.Equal(2, prompts);
        AssertSourceImages(Private(animation.Document.Editor, "_sources"), Color.LimeGreen);
    });
}
