using System.Reflection;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void StudioSelectionsShareCredentialsAcrossAllEditorsAndPackedDocumentOpens() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "encrypted.gaf", Color.LimeGreen);
        using (var package = AssetsFile.LoadOrCreate(archive, "secret", true, register: false))
        {
            using var audio = new MemoryStream([82, 73, 70, 70]);
            package.Add(AssetTypes.Audio, "audio/chime.wav", audio);
            package.Save();
        }
        var aniPath = Path.Combine(root, "empty.gani");
        AnimationDocument.Create(root).Save(aniPath, allowInvalid: true);
        var scenePath = Path.Combine(root, "empty.gscn");
        SceneDocument.Create(root).Save(scenePath, allowInvalid: true);
        var spritePath = Path.Combine(root, "empty.gspr");
        SpriteDocument.Create(root).Save(spritePath, allowInvalid: true);
        var audioPath = Path.Combine(root, "empty.gsnd");
        AudioDocument.Create(root).Save(audioPath, allowInvalid: true);
        using var studio = Host(root);
        int prompts = 0;
        studio.AssetPassword = _ => { prompts++; return "secret"; };
        var animation = (AnimationEditorControl)studio.OpenDocument(aniPath).Document.Editor;
        var scene = (SceneEditorControl)studio.OpenDocument(scenePath).Document.Editor;
        var sprite = (SpriteEditorControl)studio.OpenDocument(spritePath).Document.Editor;
        var audioEditor = (AudioEditorControl)studio.OpenDocument(audioPath).Document.Editor;
        var gts = new PackedResourceSelection(new(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts"), "terrain");
        animation.AddPackedTilesheetSource(gts);
        scene.AddPackedTilesheetSource(gts);
        sprite.AddPackedTilesheetSource(gts);
        audioEditor.AddPackedAudio(new(new(archive, AssetTypes.Audio, "audio/chime.wav")));
        Assert.Same(Private(animation, "_packages"), Private(audioEditor, "_packages"));
        animation.ReloadSources();
        studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, archive, "definitions/world.gts"));
        Assert.Equal(1, prompts);
        Assert.Empty(audioEditor.Document.Validate());
        AssertSourceImages(Private(animation, "_sources"), Color.LimeGreen);
        AssertSourceImages(Private(scene, "_tilesheetSources"), Color.LimeGreen);
        AssertSourceImages(Private(sprite, "_sources"), Color.LimeGreen);
    });

    [Fact]
    public void StandalonePickerReusesVerifiedCredentialsAndRetriesAfterPasswordChange() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "encrypted.gaf", Color.Blue);
        using (var package = AssetsFile.LoadOrCreate(archive, "secret", true, register: false)) package.Save();
        using var owner = new Form();
        var type = typeof(AnimationEditorControl).Assembly.GetType("Gondwana.Tooling.WinForms.PackedResourcePicker")!;
        using var packages = (AssetPackageCatalog)type.GetMethod("CreateCatalog", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [owner])!;
        int prompts = 0;
        string password = "wrong";
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            var prompt = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Asset password");
            if (prompt is null) return;
            prompts++;
            prompt.Controls.OfType<TextBox>().Single().Text = password;
            ((Button)prompt.AcceptButton!).PerformClick();
        };
        timer.Start();
        Assert.Throws<InvalidDataException>(() => packages.GetResources(archive, AssetTypes.TilesheetDefinition));
        password = "secret";
        packages.Invalidate(archive);
        Assert.Single(packages.GetResources(archive, AssetTypes.TilesheetDefinition));
        packages.Clear();
        Assert.Single(packages.GetResources(archive, AssetTypes.TilesheetDefinition));
        Assert.Equal(2, prompts);
        // Rewrite using a changed password; the cached old credential fails once, then retry prompts.
        using (var original = AssetsFile.LoadOrCreate(archive, "secret", true, register: false))
        using (var replacement = AssetsFile.LoadOrCreate(Path.Combine(root, "new.gaf"), "changed", true, register: false))
        {
            foreach (var entry in original.GetAllEntries())
            {
                using var stream = original.Get(entry.AssetType, entry.AssetName)!;
                replacement.Add(entry.AssetType, entry.AssetName, stream);
            }
            replacement.Save();
        }
        File.Copy(Path.Combine(root, "new.gaf"), archive, true);
        packages.Clear();
        Assert.Throws<InvalidDataException>(() => packages.GetResources(archive, AssetTypes.TilesheetDefinition));
        password = "changed";
        packages.Invalidate(archive);
        Assert.Single(packages.GetResources(archive, AssetTypes.TilesheetDefinition));
        Assert.Equal(3, prompts);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedSelectionsPreviewNonFirstMembersAndSurviveSaveAs(bool sharedCatalog) => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        foreach (var file in Directory.GetFiles(root).Where(file => file != archive)) File.Delete(file);
        var original = File.ReadAllBytes(archive);
        using var packages = new AssetPackageCatalog();
        var catalog = sharedCatalog ? packages : null;
        var gts = new PackedResourceSelection(new(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts"), "terrain");
        var gani = new PackedResourceSelection(new(archive, AssetTypes.AnimationDefinition, "definitions/water.gani"), "water.flow");
        var gscn = new PackedResourceSelection(new(archive, AssetTypes.SceneDefinition, "definitions/island.gscn"), "island");
        Assert.Equal(new[] { "unrelated", "terrain" }, PackedResourceSelection.GetMembers(packages, gts.Resource));
        using var animations = new AnimationEditorControl(AnimationDocument.Create(root), catalog);
        animations.AddPackedTilesheetSource(gts);
        Assert.Throws<InvalidOperationException>(() => animations.AddPackedTilesheetSource(gts));
        animations.Document.Definition.Frames.Add(new() { Tilesheet = "terrain", RegionName = "default" });
        AssertSourceImages(Private(animations, "_sources"), Color.LimeGreen);
        var scenePath = Path.Combine(root, "scene.gscn");
        var scene = PreviewScene("assets.gaf");
        scene.TilesheetSources.Clear(); scene.AnimationSources.Clear();
        SceneDefinitionSerializer.Save(scenePath, scene);
        using var scenes = new SceneEditorControl(SceneDocument.Open(scenePath), catalog);
        scenes.AddPackedTilesheetSource(gts);
        scenes.AddPackedAnimationSource(gani);
        Assert.Throws<InvalidOperationException>(() => scenes.AddPackedTilesheetSource(gts));
        Assert.Throws<InvalidOperationException>(() => scenes.AddPackedAnimationSource(gani));
        AssertRenderedColor((Control)Private(scenes, "_preview"), Color.LimeGreen);
        var spriteDocument = SpriteDocument.Create(root);
        var sprite = spriteDocument.AddSprite();
        sprite.SceneId = "island"; sprite.SceneLayerId = "ground";
        sprite.Frame = new() { Tilesheet = "terrain", RegionName = "default" };
        sprite.AnimationKey = "water.flow"; sprite.StartAnimation = true;
        using var sprites = new SpriteEditorControl(spriteDocument, catalog);
        sprites.AddPackedTilesheetSource(gts);
        sprites.AddPackedSceneSource(gscn);
        sprites.AddPackedAnimationSource(gani);
        Assert.Throws<InvalidOperationException>(() => sprites.AddPackedTilesheetSource(gts));
        Assert.Throws<InvalidOperationException>(() => sprites.AddPackedAnimationSource(gani));
        Assert.Throws<InvalidOperationException>(() => sprites.AddPackedSceneSource(gscn));
        AssertSourceImages(Private(sprites, "_sources"), Color.LimeGreen);
        AssertRenderedColor((Control)Private(Private(sprites, "_preview"), "_canvas"), Color.LimeGreen);

        foreach (var directory in new[] { root, Path.Combine(root, "saved") })
        {
            Directory.CreateDirectory(directory);
            var animationPath = Path.Combine(directory, "picked.gani");
            animations.Document.Save(animationPath);
            var animationRef = Assert.Single(AnimationDocument.Open(animationPath).Definition.TilesheetSources);
            Assert.Equal("terrain", animationRef.Tilesheet);
            Assert.Equal("definitions/world.gts", animationRef.AssetEntryName);
            Assert.Equal(archive, Path.GetFullPath(animationRef.AssetsFilePath!, directory));
            var savedScene = Path.Combine(directory, "picked.gscn");
            scenes.Document.Save(savedScene);
            var sceneRef = Assert.Single(SceneDocument.Open(savedScene).Definition.AnimationSources);
            Assert.Equal("water.flow", sceneRef.AnimationKey);
            Assert.Equal("definitions/water.gani", sceneRef.AssetEntryName);
            Assert.Equal(archive, Path.GetFullPath(sceneRef.AssetsFilePath!, directory));
            var savedSprite = Path.Combine(directory, "picked.gspr");
            sprites.Document.Save(savedSprite);
            var spriteRef = Assert.Single(SpriteDocument.Open(savedSprite).Definition.SceneSources);
            Assert.Equal("island", spriteRef.SceneId);
            Assert.Equal("definitions/island.gscn", spriteRef.AssetEntryName);
            Assert.Equal(archive, Path.GetFullPath(spriteRef.AssetsFilePath!, directory));
        }
        Assert.Equal(original, File.ReadAllBytes(archive));
        animations.Document.Definition.Key = "unsaved";
        animations.Document.MarkChanged();
        ReplacePreviewImage(root, archive, Color.Blue);
        animations.ReloadSources(); scenes.ReloadSources(); sprites.ReloadSources();
        AssertSourceImages(Private(animations, "_sources"), Color.Blue);
        AssertRenderedColor((Control)Private(scenes, "_preview"), Color.Blue);
        Assert.Equal("unsaved", animations.Document.Definition.Key);
        Assert.True(animations.Document.IsDirty);
        using var exclusive = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public void PackedSelectionRejectsWrongMissingInvalidAndConflictingSourcesWithoutEdits() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using var packages = new AssetPackageCatalog();
        using var editor = new AnimationEditorControl(AnimationDocument.Create(root), packages);
        var selection = new PackedResourceSelection(new(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts"), "terrain");
        foreach (var invalid in new[] {
            selection with { Member = "absent" }, selection with { Member = null },
            selection with { Resource = selection.Resource with { EntryName = "world.gts" } },
            selection with { Resource = selection.Resource with { AssetType = AssetTypes.Image } } })
        {
            Assert.Throws<InvalidDataException>(() => editor.AddPackedTilesheetSource(invalid));
            Assert.Empty(editor.Document.Definition.TilesheetSources);
        }
        var invalidFile = Path.Combine(root, "invalid.gts");
        File.WriteAllText(invalidFile, "not JSON");
        using (var package = AssetsFile.LoadOrCreate(archive, null, false, register: false))
        {
            package.Add(AssetTypes.TilesheetDefinition, invalidFile, "invalid.gts");
            package.Save();
        }
        packages.Invalidate(archive);
        Assert.Throws<InvalidDataException>(() => editor.AddPackedTilesheetSource(selection with { Resource = selection.Resource with { EntryName = "invalid.gts" } }));
        File.WriteAllText(invalidFile, "{\"Definitions\":[]}");
        using (var package = AssetsFile.LoadOrCreate(archive, null, false, register: false))
        {
            package.Add(AssetTypes.TilesheetDefinition, invalidFile, "empty.gts");
            package.Save();
        }
        packages.Invalidate(archive);
        Assert.Contains("no logical definitions", Assert.Throws<InvalidDataException>(() =>
            PackedResourceSelection.GetMembers(packages, selection.Resource with { EntryName = "empty.gts" })).Message);
        editor.AddPackedTilesheetSource(selection);
        var before = AnimationDefinitionSerializer.ToJson(editor.Definition);
        var other = CreatePreviewArchive(root, "other.gaf", Color.Red);
        Assert.Throws<InvalidOperationException>(() => editor.AddPackedTilesheetSource(selection with { Resource = selection.Resource with { Path = other } }));
        TilesheetDefinitionSerializer.Save(Path.Combine(root, "world.gts"), PreviewSheet("terrain"));
        Assert.Throws<InvalidOperationException>(() => editor.AddTilesheetSource(Path.Combine(root, "world.gts")));
        Assert.Equal(before, AnimationDefinitionSerializer.ToJson(editor.Definition));
        AssertSourceImages(Private(editor, "_sources"), Color.LimeGreen);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedAudioPickerRoundTripAndExtensionlessEntries(bool extensionless) => RunSta(root =>
    {
        var archive = Path.Combine(root, "sound.zip");
        var wave = Path.Combine(root, "sound.wav");
        // Real PCM WAVE header and one silent sample.
        using (var writer = new BinaryWriter(File.Create(wave)))
        {
            writer.Write("RIFF"u8.ToArray()); writer.Write(38); writer.Write("WAVEfmt "u8.ToArray());
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000);
            writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8.ToArray()); writer.Write(2); writer.Write((short)0);
        }
        var entry = extensionless ? "sounds/alert" : "sounds/alert.wav";
        using (var package = AssetsFile.LoadOrCreate(archive, null, false, register: false))
        {
            package.Add(AssetTypes.Audio, wave, entry);
            package.Add(AssetTypes.Misc, wave, "wrong.wav");
            package.Save();
        }
        File.Delete(wave);
        using var packages = new AssetPackageCatalog();
        var resource = Assert.Single(packages.GetResources(archive, AssetTypes.Audio));
        Assert.Equal(entry, resource.EntryName);
        using var editor = new AudioEditorControl(AudioDocument.Create(root), packages);
        if (extensionless) Assert.Throws<InvalidDataException>(() => editor.AddPackedAudio(new(resource)));
        editor.AddPackedAudio(new(resource, SourceExtension: ".wav"));
        Assert.Empty(editor.Document.Validate());
        foreach (var directory in new[] { root, Path.Combine(root, "saved") })
        {
            var path = Path.Combine(directory, "audio.gsnd");
            editor.Document.Save(path);
            var reopened = AudioDocument.Open(path);
            var audio = Assert.Single(reopened.Definition.Resources);
            Assert.Empty(reopened.Validate());
            Assert.Equal(AudioResourceSourceKind.PackedAsset, audio.SourceKind);
            Assert.Equal(entry, audio.AssetEntryName);
            Assert.Equal(".wav", audio.SourceExtension);
            Assert.Equal(archive, Path.GetFullPath(audio.AssetsFilePath!, directory));
        }
        using var exclusive = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Theory]
    [InlineData("Animations")]
    [InlineData("Scenes")]
    [InlineData("Sprites")]
    [InlineData("Audio")]
    public void SharedPickerListsExactEntriesRequiresMemberAndRetriesAuthentication(string assembly) => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var locked = Path.Combine(root, "locked.gaf");
        using (var package = AssetsFile.LoadOrCreate(locked, "secret", true, register: false))
        {
            package.Add(AssetTypes.TilesheetDefinition, Path.Combine(root, "world.gts"), "nested/world.gts");
            package.Add(AssetTypes.Misc, Path.Combine(root, "world.gts"), "wrong.gts");
            package.Save();
        }
        using var packages = new AssetPackageCatalog();
        var prompts = 0;
        var accepted = 0;
        packages.PasswordProvider = _ => ++prompts == 1 ? "wrong" : "secret";
        packages.PasswordAccepted += (_, _) => accepted++;
        PackedResourceSelection? result = null;
        var type = Assembly.Load($"Gondwana.Tooling.{assembly}.WinForms").GetType("Gondwana.Tooling.WinForms.PackedResourcePicker")!;
        using var dialog = (Form)type.GetMethod("CreateDialog", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [packages, locked, AssetTypes.TilesheetDefinition, (Action<PackedResourceSelection>)(selected => result = selected)])!;
        dialog.Show(); Application.DoEvents();
        T Find<T>(string name) where T : Control => (T)dialog.Controls.Find(name, true).Single();
        Assert.Contains("password", Find<TextBox>("status").Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Find<ListBox>("entries").Items.Cast<object>());
        Find<Button>("reload").PerformClick();
        var entries = Find<ListBox>("entries");
        Assert.Single(entries.Items.Cast<object>());
        entries.SelectedIndex = 0;
        Assert.Equal("nested/world.gts", entries.GetItemText(entries.SelectedItem));
        Assert.False(Find<Button>("add").Enabled);
        Find<ListBox>("members").SelectedIndex = 1;
        Find<Button>("add").PerformClick();
        Assert.Equal("terrain", result!.Member);
        Assert.Equal(locked, result.Resource.Path);
        result.Validate(packages, AssetTypes.TilesheetDefinition);
        Assert.Equal(2, prompts);
        Assert.Equal(1, accepted);
        using var exclusive = File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });
}
