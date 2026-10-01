using System.Drawing;
using System.IO.Compression;
using System.Text;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class EngineStatePackedDefinitionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PackedState-" + Guid.NewGuid());
    private string ArchivePath => Path.Combine(_root, "assets", "game.gaf");
    private string StatePath => Path.Combine(_root, "saves", "state.json");

    public EngineStatePackedDefinitionTests()
    {
        Clear();
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        Directory.CreateDirectory(Path.Combine(_root, "saves"));
        AudioResourceManager.Instance.ConfigureBackend(new Backend());
    }

    private static void Clear()
    {
        foreach (var sprite in SpriteManager.Instance.AllSprites)
        {
            SpriteManager.Instance._spriteList.Remove(sprite);
            sprite.DisposeImmediate();
        }
        Scene.ClearAllScenes();
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
        AudioResourceManager.Instance.Clear();
        AssetsFile.ClearAll();
    }

    public void Dispose()
    {
        Clear();
        Directory.Delete(_root, recursive: true);
    }

    private static EngineStateSaveOptions Preserve(bool compress = false, EngineStateParts parts = EngineStateParts.All) => new()
    {
        Compress = compress, Parts = parts,
        Tilesheets = DefinitionPersistence.PreserveSource,
        Cycles = DefinitionPersistence.PreserveSource,
        Scenes = DefinitionPersistence.PreserveSource,
        Audio = DefinitionPersistence.PreserveSource,
        Sprites = DefinitionPersistence.PreserveSource
    };

    private static void Add(AssetsFile archive, AssetTypes type, string entry, string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        archive.Add(type, entry, stream);
    }

    private void CreatePackage(bool split = false)
    {
        var archive = AssetsFile.LoadOrCreate(ArchivePath);
        using (var bitmap = new SKBitmap(16, 16))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = new MemoryStream(data.ToArray()))
            archive.Add(AssetTypes.Image, "terrain.png", stream);

        Add(archive, AssetTypes.TilesheetDefinition, "terrain.gts", TilesheetDefinitionSerializer.ToJson(new TilesheetDefinition
        {
            Name = "terrain",
            Image = new() { AssetEntryName = "terrain.png" },
            Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 16, 16), TileSize = new Size(16, 16) }]
        }));
        var sheet = TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "terrain.gts");
        var cycle = new Cycle(new FrameSequence([sheet.GetFrame("default", 0, 0)]), .2, "walk");
        var scene = new Scene { ID = "level" };
        var layer = scene.AddLayer(1, 1, 16, 16);
        layer.ID = "ground";
        SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame("default", 0, 0), "actor");

        var other = split ? AssetsFile.LoadOrCreate(Path.Combine(_root, "assets", "other.gaf")) : archive;
        Add(other, AssetTypes.AnimationDefinition, "walk.gani", AnimationDefinitionSerializer.ToJson(AnimationDefinitionSerializer.FromCycle(cycle)));
        Add(other, AssetTypes.SceneDefinition, "level.gscn", SceneDefinitionSerializer.ToJson(SceneDefinitionSerializer.FromScene(scene)));
        Add(other, AssetTypes.SpriteDefinition, "actors.gspr", SpriteDefinitionSerializer.ToJson(SpriteDefinitionSerializer.FromSprites(SpriteManager.Instance.AllSprites)));
        Add(other, AssetTypes.AudioDefinition, "audio.gsnd", AudioDefinitionSerializer.ToJson(new AudioDefinition
        {
            Resources = [new() { Key = "music", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "music.ogg", Volume = .4f }]
        }));
        archive.Save();
        if (split) other.Save();
        Clear();

        archive = AssetsFile.LoadOrCreate(ArchivePath);
        other = split ? AssetsFile.LoadOrCreate(Path.Combine(_root, "assets", "other.gaf")) : archive;
        TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "terrain.gts");
        AnimationDefinitionSerializer.LoadCycle(other, "walk.gani");
        SceneDefinitionSerializer.LoadScene(other, "level.gscn");
        SpriteDefinitionSerializer.ToSprites(SpriteDefinitionSerializer.Load(other, "actors.gspr"));
        AudioDefinitionSerializer.LoadIntoManager(AudioDefinitionSerializer.Load(other, "audio.gsnd"));
    }

    private static JObject Read(string path, bool compressed = false)
    {
        using var stream = File.OpenRead(path);
        using var input = compressed ? (Stream)new GZipStream(stream, CompressionMode.Decompress) : stream;
        using var reader = new StreamReader(input);
        return JObject.Parse(reader.ReadToEnd());
    }

    private static IEnumerable<JObject> Entries(JObject root)
    {
        yield return (JObject)root["Tilesheets"]!["terrain"]!;
        yield return (JObject)root["Cycles"]!["walk"]!;
        yield return (JObject)root["Scenes"]!["$values"]![0]!;
        yield return (JObject)root["Audio"]!;
        yield return (JObject)root["Sprites"]!;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AllFiveFormatsReloadFromPackages(bool compressed, bool split)
    {
        CreatePackage(split);
        var id = Assert.Single(SpriteManager.Instance.AllSprites).Id;
        new EngineState().SaveToFile(StatePath, Preserve(compressed));
        var root = Read(StatePath, compressed);
        foreach (var entry in Entries(root))
        {
            Assert.False(Path.IsPathRooted(entry.Value<string>("AssetsFilePath")!));
            Assert.NotNull(entry["AssetEntryName"]);
            Assert.True(entry["Definition"] is null || entry["Definition"]!.Type == JTokenType.Null);
        }
        Assert.DoesNotContain("terrain.png", root.ToString());
        Clear();
        Assert.Empty(TilesheetRegistry.Instance.GetAll());
        EngineState.LoadFromFile(StatePath, compressed);
        Assert.NotNull(TilesheetRegistry.Instance.GetOrNull("terrain"));
        Assert.Equal(.2, Cycle.GetAnimationCycle("walk")!.ThrottleTime);
        Assert.Equal("level", Assert.Single(Scene.GetAllScenes()).ID);
        Assert.Equal(id, Assert.Single(SpriteManager.Instance.AllSprites).Id);
        Assert.Equal(.4f, AudioResourceManager.Instance.Get("music")!.Volume);
        new EngineState().SaveToFile(StatePath, Preserve());
        Assert.All(Entries(Read(StatePath)), entry => Assert.NotNull(entry["AssetEntryName"]));
    }

    [Fact]
    public void RelativePathsSurviveMovingTheTree()
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, Preserve());
        Clear();
        var moved = Path.Combine(_root, "moved");
        Directory.CreateDirectory(moved);
        Directory.Move(Path.Combine(_root, "assets"), Path.Combine(moved, "assets"));
        Directory.Move(Path.Combine(_root, "saves"), Path.Combine(moved, "saves"));
        EngineState.LoadFromFile(Path.Combine(moved, "saves", "state.json"));
        Assert.Single(SpriteManager.Instance.AllSprites);
    }

    [Theory]
    [InlineData(EngineStateParts.Tilesheets)]
    [InlineData(EngineStateParts.Cycles)]
    [InlineData(EngineStateParts.Scenes)]
    [InlineData(EngineStateParts.Sprites)]
    [InlineData(EngineStateParts.Audio)]
    public void SelectiveSavesIncludeRequiredPackages(EngineStateParts parts)
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, Preserve(parts: parts));
        Clear();
        EngineState.LoadFromFile(StatePath, parts: parts);
        Assert.Single(AssetsFile.AllAssetsFiles);
        if (parts != EngineStateParts.Audio) Assert.Empty(AudioResourceManager.Instance.GetAll());
        if (parts == EngineStateParts.Audio) Assert.Empty(TilesheetRegistry.Instance.GetAll());
        if (parts != EngineStateParts.Sprites) Assert.Empty(SpriteManager.Instance.AllSprites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MergeHonorsOverwriteForExistingCycles(bool overwrite)
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, Preserve());
        Cycle.GetAnimationCycles().Single(cycle => cycle.CycleKey == "walk").ThrottleTime = .9;
        EngineState.MergeFromFile(StatePath, overwriteExisting: overwrite);
        Assert.Equal(overwrite ? .2 : .9, Cycle.GetAnimationCycle("walk")!.ThrottleTime);
        Assert.Single(SpriteManager.Instance.AllSprites);
    }

    [Fact]
    public void MutatedRuntimeDefinitionsFallBackToCurrentInlineState()
    {
        CreatePackage();
        TilesheetRegistry.Instance.GetOrNull("terrain")!.GetRegion("default")!.TileSize = new Size(8, 8);
        Cycle.GetAnimationCycles().Single(cycle => cycle.CycleKey == "walk").ThrottleTime = .7;
        Assert.Single(Scene.GetAllScenes()).ID = "changed-level";
        Assert.Single(SpriteManager.Instance.AllSprites).Visible = false;
        AudioResourceManager.Instance.Get("music")!.Volume = .8f;
        new EngineState().SaveToFile(StatePath, Preserve());
        Assert.All(Entries(Read(StatePath)), entry =>
        {
            Assert.Null(entry["AssetsFilePath"]);
            Assert.IsType<JObject>(entry["Definition"]);
        });
        Clear();
        EngineState.LoadFromFile(StatePath);
        Assert.Equal(.7, Cycle.GetAnimationCycle("walk")!.ThrottleTime);
        Assert.False(Assert.Single(SpriteManager.Instance.AllSprites).Visible);
        Assert.Equal(.8f, AudioResourceManager.Instance.Get("music")!.Volume);
    }

    [Theory]
    [InlineData("missing.gani", AssetTypes.AnimationDefinition)]
    [InlineData("wrong.gani", AssetTypes.SceneDefinition)]
    public void MissingOrWrongTypeEntryReportsArchiveAndEntry(string entry, AssetTypes type)
    {
        var archive = AssetsFile.LoadOrCreate(ArchivePath);
        Add(archive, type, "wrong.gani", "{}");
        archive.Save();
        File.WriteAllText(StatePath, JsonConvert.SerializeObject(new
        {
            Cycles = new Dictionary<string, object> { ["walk"] = new { AssetsFilePath = ArchivePath, AssetEntryName = entry } }
        }));
        var ex = Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath, parts: EngineStateParts.Cycles));
        Assert.Contains(ArchivePath, ex.Message);
        Assert.Contains(entry, ex.Message);
    }

    [Fact]
    public void MissingArchiveDoesNotCreateEmptyPackage()
    {
        File.WriteAllText(StatePath, JsonConvert.SerializeObject(new
        {
            Scenes = new[] { new { AssetsFilePath = ArchivePath, AssetEntryName = "level.gscn" } }
        }));
        var ex = Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath));
        Assert.Contains(ArchivePath, ex.Message);
        Assert.Contains("level.gscn", ex.Message);
        Assert.False(File.Exists(ArchivePath));
        Assert.Empty(AssetsFile.AllAssetsFiles);
    }

    [Theory]
    [InlineData("{ 'Definition': {}, 'GaniPath': 'walk.gani' }")]
    [InlineData("{ 'GaniPath': 'walk.gani', 'AssetsFilePath': 'game.gaf' }")]
    [InlineData("{ 'AssetsFilePath': 'game.gaf' }")]
    [InlineData("{ 'AssetEntryName': 'walk.gani' }")]
    public void ConflictingAndPartialSourcesFail(string entry)
    {
        File.WriteAllText(StatePath, new JObject { ["Cycles"] = new JObject { ["walk"] = JObject.Parse(entry) } }.ToString());
        Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StreamArchiveIdentityControlsPackedPreservation(bool identified)
    {
        CreatePackage();
        Clear();
        using var stream = File.OpenRead(ArchivePath);
        var archive = AssetsFile.Load(stream, password: null, register: true, sourcePath: identified ? ArchivePath : null);
        TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "terrain.gts");
        AnimationDefinitionSerializer.LoadCycle(archive, "walk.gani");
        SceneDefinitionSerializer.LoadScene(archive, "level.gscn");
        SpriteDefinitionSerializer.ToSprites(SpriteDefinitionSerializer.Load(archive, "actors.gspr"));
        AudioDefinitionSerializer.LoadIntoManager(AudioDefinitionSerializer.Load(archive, "audio.gsnd"));
        new EngineState().SaveToFile(StatePath, Preserve());
        foreach (var entry in Entries(Read(StatePath)))
            Assert.Equal(identified, entry["AssetsFilePath"] is not null);
        Clear();
        EngineState.LoadFromFile(StatePath);
        Assert.Single(SpriteManager.Instance.AllSprites);
    }

    [Fact]
    public void EditingDefinitionBeforeMaterializationDoesNotPreserveItsOldSource()
    {
        CreatePackage();
        var definition = AnimationDefinitionSerializer.Load(AssetsFile.AllAssetsFiles[0], "walk.gani");
        definition.ThrottleTime = .6;
        AnimationDefinitionSerializer.ToCycle(definition);
        new EngineState().SaveToFile(StatePath, Preserve());
        Assert.Null(Read(StatePath)["Cycles"]!["walk"]!["AssetsFilePath"]);
    }

    [Fact]
    public void PreserveSourceRetainsExistingLooseDefinitions()
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, separateGtsFiles: true, separateGaniFiles: true,
            separateGscnFiles: true, separateGsndFile: true, separateGsprFile: true);
        Clear();
        EngineState.LoadFromFile(StatePath);
        var otherState = Path.Combine(_root, "preserved.json");
        new EngineState().SaveToFile(otherState, Preserve());
        var entries = Entries(Read(otherState)).ToArray();
        var names = new[] { "GtsPath", "GaniPath", "GscnPath", "GsndPath", "GsprPath" };
        for (int i = 0; i < entries.Length; i++)
        {
            Assert.StartsWith("saves", entries[i].Value<string>(names[i]));
            Assert.Null(entries[i]["AssetsFilePath"]);
        }
        Clear();
        EngineState.LoadFromFile(otherState);
        Assert.Single(SpriteManager.Instance.AllSprites);
    }

    [Fact]
    public void LegacyArchiveShapeDeserializesWithoutRegisteringLiveAssets()
    {
        CreatePackage();
        var legacy = JsonConvert.SerializeObject(new { AssetsFiles = AssetsFile.AllAssetsFiles.ToList() }, EngineState.JsonSerializerSettings);
        var root = JObject.Parse(legacy);
        var entry = root["AssetsFiles"]!["$values"]![0]!;
        // Also accept historical explicit type metadata without constructing that CLR type.
        entry["$type"] = "Gondwana.Assets.AssetsFile, Gondwana";
        root["Cycles"] = new JObject { ["bad"] = new JObject { ["AssetsFilePath"] = "partial" } };
        File.WriteAllText(StatePath, root.ToString());
        Clear();
        Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath));
        Assert.Empty(AssetsFile.AllAssetsFiles);
        root.Remove("Cycles");
        File.WriteAllText(StatePath, root.ToString());
        EngineState.LoadFromFile(StatePath, parts: EngineStateParts.AssetsFiles);
        Assert.Single(AssetsFile.AllAssetsFiles);
    }

    [Fact]
    public void MissingListedArchiveStillReportsThePackedEntry()
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, Preserve());
        Clear();
        File.Delete(ArchivePath);
        var ex = Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath));
        Assert.Contains(ArchivePath, ex.Message);
        Assert.Contains("terrain.gts", ex.Message);
    }

    [Fact]
    public void PackedReferencesRequireExactEntryNames()
    {
        CreatePackage();
        new EngineState().SaveToFile(StatePath, Preserve());
        var root = Read(StatePath);
        root["Cycles"]!["walk"]!["AssetEntryName"] = "elsewhere/walk.gani";
        File.WriteAllText(StatePath, root.ToString());
        Clear();
        var ex = Assert.Throws<InvalidDataException>(() => EngineState.LoadFromFile(StatePath));
        Assert.Contains("elsewhere/walk.gani", ex.Message);
    }

    [Fact]
    public void AnonymousStreamAudioExportsReloadableLooseMedia()
    {
        var archive = AssetsFile.LoadOrCreate(ArchivePath);
        using (var bytes = new MemoryStream([1, 2, 3, 4])) archive.Add(AssetTypes.Audio, "sound.wav", bytes);
        archive.Save();
        Clear();
        using var stream = File.OpenRead(ArchivePath);
        archive = AssetsFile.Load(stream);
        AudioResourceManager.Instance.LoadFromEngineAssetsFile(archive);
        new EngineState().SaveToFile(StatePath, Preserve(parts: EngineStateParts.Audio));
        Assert.NotNull(Read(StatePath)["Audio"]!["GsndPath"]);
        Clear();
        EngineState.LoadFromFile(StatePath, parts: EngineStateParts.Audio);
        Assert.Single(AudioResourceManager.Instance.GetAll());
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "saves", "state.audio"), "*.wav"));
    }

    [Fact]
    public void RuntimeGeneratedSceneIdentitiesAreSavedInline()
    {
        var archive = AssetsFile.LoadOrCreate(ArchivePath);
        Add(archive, AssetTypes.SceneDefinition, "generated.gscn", SceneDefinitionSerializer.ToJson(new SceneDefinition()));
        archive.Save();
        var scene = SceneDefinitionSerializer.LoadScene(archive, "generated.gscn");
        new EngineState().SaveToFile(StatePath, Preserve(parts: EngineStateParts.Scenes));
        var entry = Read(StatePath)["Scenes"]!["$values"]![0]!;
        Assert.Null(entry["AssetsFilePath"]);
        Assert.Equal(scene.ID, entry["Definition"]!["ID"]!.Value<string>());
    }

    private sealed class Backend : IAudioBackend
    {
        public string Name => "Packed definitions test";
        public IAudioPlaybackHandle CreateFromUri(string key, string uri, float volume, float pan, float playbackSpeed) => new Handle();
        public IAudioPlaybackHandle CreateFromBytes(string key, byte[] data, string fileNameOrExtension, float volume, float pan, float playbackSpeed) => new Handle();
    }

    private sealed class Handle : IAudioPlaybackHandle
    {
        public event EventHandler? PlaybackCompleted { add { } remove { } }
        public AudioPlaybackState State => AudioPlaybackState.Stopped;
        public TimeSpan CurrentTime => TimeSpan.Zero;
        public TimeSpan Duration => TimeSpan.Zero;
        public bool IsLooping { get; set; }
        public float Volume { get; set; }
        public float Pan { get; set; }
        public float PlaybackSpeed { get; set; }
        public string? TemporaryFilePath => null;
        public void Play(bool fromStart = true) { }
        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void Dispose() { }
    }
}
