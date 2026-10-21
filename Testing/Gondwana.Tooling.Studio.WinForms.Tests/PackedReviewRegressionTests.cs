using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EmptyEncryptedArchiveRetainsPolicyAfterSaveAndSaveAs(bool removeLastEntry, bool saveAs) => RunSta(root =>
    {
        var path = Path.Combine(root, "encrypted.gaf");
        using var studio = Host(root);
        var document = studio.NewDocument("gaf", path, "secret", encrypt: true);
        var editor = (AssetEditorControl)document.Document.Editor;
        if (removeLastEntry)
        {
            using var bytes = new MemoryStream([1]);
            editor.AssetsFile.Add(AssetTypes.Misc, "last", bytes);
            editor.MarkChanged();
            Assert.True(AwaitUi(studio.SaveDocument(document)));
            editor.AssetsFile.Remove(AssetTypes.Misc, "last");
            editor.MarkChanged();
        }
        var destination = saveAs ? Path.Combine(root, "copy.gaf") : path;
        Assert.True(AwaitUi(studio.SaveDocument(document, saveAs, destination)));
        Assert.Empty(editor.AssetsFile.GetAllEntries());
        Assert.True(editor.AssetsFile.UseEncryption);
        Assert.Equal("secret", editor.AssetsFile.Password);
        using var imported = new MemoryStream([2, 3]);
        editor.AssetsFile.Add(AssetTypes.Misc, "later", imported);
        editor.MarkChanged();
        Assert.True(AwaitUi(studio.SaveDocument(document)));
        Assert.ThrowsAny<Exception>(() => StudioArchiveSnapshot.Open(destination, null));
        using var saved = StudioArchiveSnapshot.Open(destination, "secret");
        Assert.True(saved.Archive.UseEncryption);
        Assert.Equal(new byte[] { 2, 3 }, StudioArchiveSnapshot.Entry(saved.Archive, new(AssetTypes.Misc, destination, "later")));
    });

    [Fact]
    public void DependencyReloadCanCorrectAnUnverifiedPassword() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "encrypted.gaf", Color.LimeGreen);
        using (var encrypted = AssetsFile.LoadOrCreate(path, "secret", true, register: false)) encrypted.Save();
        var loose = Path.Combine(root, "preview.gani");
        AnimationDefinitionSerializer.Save(loose, PreviewAnimation("encrypted.gaf"));
        using var studio = Host(root);
        int prompts = 0;
        studio.AssetPassword = _ => ++prompts == 1 ? "wrong" : "secret";
        var document = studio.OpenDocument(loose);
        Assert.Equal(1, prompts);
        studio.ReloadPreviews(document);
        Assert.Equal(2, prompts);
        AssertSourceImages(Private(document.Document.Editor, "_sources"), Color.LimeGreen);
        Assert.False(document.Document.Dirty());
        // A successful load is reused by the catalog without another password request.
        var second = Path.Combine(root, "second.gani");
        File.Copy(loose, second);
        studio.OpenDocument(second);
        Assert.Equal(2, prompts);
    });

    [Theory]
    [InlineData("gani", false)]
    [InlineData("gani", true)]
    [InlineData("gscn", false)]
    [InlineData("gscn", true)]
    [InlineData("gspr", false)]
    [InlineData("gspr", true)]
    [InlineData("gsnd", false)]
    [InlineData("gsnd", true)]
    public void PackedSaveAsRebasesAddedAndImportedMembersFromDocumentOrigin(string format, bool import) => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            AddNative(archive, format, "nested/same." + format);
            archive.Save();
        }
        var animation = PreviewAnimation("assets.gaf");
        var scene = PreviewScene("assets.gaf");
        var sprite = new SpriteDefinition
        {
            Key = "imported",
            TilesheetSources = [SpriteTilesheetSourceDefinition.Packed("terrain", "assets.gaf", "definitions/world.gts")],
            SceneSources = [SpriteSceneSourceDefinition.Packed("island", "assets.gaf", "definitions/island.gscn")],
            AnimationSources = [SpriteAnimationSourceDefinition.Packed("water.flow", "assets.gaf", "definitions/water.gani")]
        };
        var audio = new AudioDefinition
        {
            Key = "imported", Resources = [new() { Key = "loose", FilePath = "tone.wav" },
                new() { Key = "packed", SourceKind = AudioResourceSourceKind.PackedAsset, AssetsFilePath = "assets.gaf", AssetEntryName = "tone.wav" }]
        };
        var source = Path.Combine(root, "import." + format);
        File.WriteAllText(source, format switch
        {
            "gani" => AnimationDefinitionSerializer.ToJson(animation),
            "gscn" => SceneDefinitionSerializer.ToJson(scene),
            "gspr" => SpriteDefinitionSerializer.ToJson(sprite),
            _ => AudioDefinitionSerializer.ToJson(audio)
        });
        using var studio = Host(root);
        var document = studio.OpenDocument(NativeLocation(path, format));
        switch (document.Document.Editor)
        {
            case AnimationEditorControl e:
                if (import) e.Document.ImportDefinitions(source);
                else e.Document.AddDefinition().TilesheetSources = animation.TilesheetSources;
                break;
            case SceneEditorControl e:
                if (import) e.Document.ImportDefinitions(source);
                else
                {
                    var member = e.Document.AddDefinition();
                    member.TilesheetSources = scene.TilesheetSources;
                    member.AnimationSources = scene.AnimationSources;
                }
                break;
            case SpriteEditorControl e:
                if (import) e.Document.ImportDefinitions(source);
                else
                {
                    var member = e.Document.AddDefinition();
                    member.TilesheetSources = sprite.TilesheetSources;
                    member.SceneSources = sprite.SceneSources;
                    member.AnimationSources = sprite.AnimationSources;
                }
                break;
            case AudioEditorControl e:
                if (import) e.Document.ImportDefinitions(source);
                else e.Document.AddDefinition().Resources = audio.Resources;
                break;
        }
        var originalArchive = File.ReadAllBytes(path);
        var destination = Path.Combine(root, "copies", "adopted." + format);
        Assert.True(AwaitUi(studio.SaveDocument(document, saveAs: true, destination: destination)));
        var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(destination));
        var paths = json.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>().Where(p => p.Name == "AssetsFilePath" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToArray();
        Assert.NotEmpty(paths);
        Assert.All(paths, p => Assert.Equal(path, Path.GetFullPath((string)p.Value!, Path.GetDirectoryName(destination)!)));
        if (format == "gsnd")
        {
            var loose = json.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>().Single(p => p.Name == "FilePath" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String);
            Assert.Equal(Path.Combine(root, "tone.wav"), Path.GetFullPath((string)loose.Value!, Path.GetDirectoryName(destination)!));
        }
        Assert.Equal(3, ((Newtonsoft.Json.Linq.JArray)json["Definitions"]!).Count);
        Assert.Equal(originalArchive, File.ReadAllBytes(path));
        Assert.False(document.Document.Dirty());
    });
}
