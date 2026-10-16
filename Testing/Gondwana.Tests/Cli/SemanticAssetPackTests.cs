using System.Drawing;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Audio.GSND;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using ICSharpCode.SharpZipLib.Zip;
using SkiaSharp;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

/// <summary>Tests semantic packing through the CLI and Core after removing loose sources.</summary>
[Collection("Global engine state")]
public sealed partial class SemanticAssetPackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SemanticPack-" + Guid.NewGuid());
    private string Source => Path.Combine(_root, "source");
    private string Output => Path.Combine(_root, "output", "game.gaf");

    /// <summary>Creates isolated source content and resets runtime registries.</summary>
    public SemanticAssetPackTests()
    {
        Clear();
        Directory.CreateDirectory(Source);
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

    private static int Run(params string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<AssetsExtractCommand>("unpack");
            config.AddCommand<AssetsExtractCommand>("extract");
            config.AddCommand<AssetsPackCommand>("pack");
            config.AddBranch("assets", branch => branch.AddCommand<AssetsPackCommand>("pack"));
        });
        return app.Run(args);
    }

    private void Image(string file)
    {
        using var bitmap = new SKBitmap(16, 16);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(Source, file), data.ToArray());
    }

    private static TilesheetDefinition Sheet(string name, string image) => new()
    {
        Name = name,
        Image = new() { FilePath = image },
        Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 16, 16), TileSize = new Size(16, 16) }]
    };

    private void Content(bool explicitSources, bool legacy)
    {
        int count = legacy ? 1 : 2;
        var sheets = new List<TilesheetDefinition>();
        var animations = new List<AnimationDefinition>();
        var scenes = new List<SceneDefinition>();
        var sprites = new List<SpriteDefinition>();
        var sounds = new List<AudioDefinition>();
        for (int i = 0; i < count; i++)
        {
            Image($"art{i}.png");
            File.WriteAllBytes(Path.Combine(Source, $"sound{i}.wav"), [(byte)i, 2, 3]);
            sheets.Add(Sheet($"terrain{i}", $"art{i}.png"));
            animations.Add(new()
            {
                Key = $"walk{i}",
                NextCycleKey = $"walk{(i + 1) % count}",
                ThrottleTime = .2,
                Frames = [new() { Tilesheet = $"terrain{i}" }]
            });
            scenes.Add(new()
            {
                ID = $"world{i}",
                Layers = [new() { ID = "ground", Columns = 1, Rows = 1,
                Tiles = [new() { Frame = new() { Tilesheet = $"terrain{i}" }, AnimationKey = $"walk{i}" }] }]
            });
            sprites.Add(new()
            {
                Key = $"actors{i}",
                Sprites = [new() { Nickname = $"actor{i}", SceneId = $"world{i}",
                SceneLayerId = "ground", Frame = new() { Tilesheet = $"terrain{i}" }, AnimationKey = $"walk{i}" }]
            });
            sounds.Add(new() { Key = $"sounds{i}", Resources = [new() { Key = $"sound{i}", FilePath = $"sound{i}.wav" }] });
            if (explicitSources)
            {
                animations[i].TilesheetSources.Add(AnimationTilesheetSourceDefinition.Loose($"terrain{i}", "tiles.gts"));
                scenes[i].TilesheetSources.Add(SceneTilesheetSourceDefinition.Loose($"terrain{i}", "tiles.gts"));
                scenes[i].AnimationSources.Add(SceneAnimationSourceDefinition.Loose($"walk{i}", "cycles.gani"));
                sprites[i].TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose($"terrain{i}", "tiles.gts"));
                sprites[i].AnimationSources.Add(SpriteAnimationSourceDefinition.Loose($"walk{i}", "cycles.gani"));
                sprites[i].SceneSources.Add(SpriteSceneSourceDefinition.Loose($"world{i}", "worlds.gscn"));
            }
        }
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), legacy ? TilesheetDefinitionSerializer.ToJson(sheets[0]) : TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = sheets }));
        File.WriteAllText(Path.Combine(Source, "cycles.gani"), legacy ? AnimationDefinitionSerializer.ToJson(animations[0]) : AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = animations }));
        File.WriteAllText(Path.Combine(Source, "worlds.gscn"), legacy ? SceneDefinitionSerializer.ToJson(scenes[0]) : SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = scenes }));
        File.WriteAllText(Path.Combine(Source, "actors.gspr"), legacy ? SpriteDefinitionSerializer.ToJson(sprites[0]) : SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = sprites }));
        File.WriteAllText(Path.Combine(Source, "sounds.gsnd"), legacy ? AudioDefinitionSerializer.ToJson(sounds[0]) : AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = sounds }));
    }

    /// <summary>All five formats and every member materialize from the package without loose inputs.</summary>
    /// <param name="explicitSources">Whether authoring sources are supplied or inferred.</param>
    /// <param name="legacy">Whether input files use legacy singleton JSON.</param>
    /// <param name="protection">Zero for none, one for password, two for AES.</param>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 2)]
    public void PackedHierarchyLoadsWithoutLooseSources(bool explicitSources, bool legacy, int protection)
    {
        Content(explicitSources, legacy);
        var args = new List<string> { "assets", "pack", Source, Output };
        if (protection != 0) args.AddRange(["--password", "test-password"]);
        if (protection == 2) args.Add("--encrypt");
        Assert.Equal(0, Run(args.ToArray()));
        Directory.Delete(Source, true);
        var archive = AssetsFile.LoadOrCreate(Output, protection == 0 ? null : "test-password");
        int count = legacy ? 1 : 2;
        var groups = SpriteDefinitionSerializer.LoadAll(archive, "actors.gspr");
        Assert.Equal(count, groups.Count);
        foreach (var group in groups)
        {
            var sheetSource = Assert.Single(group.TilesheetSources);
            Assert.Equal(SpriteTilesheetSourceKind.PackedDefinitionFile, sheetSource.Kind);
            Assert.Equal(Output, Path.GetFullPath(sheetSource.AssetsFilePath!, Path.GetDirectoryName(Output)!));
            Assert.NotNull(TilesheetDefinitionSerializer.Load(archive, sheetSource.AssetEntryName!, sheetSource.Tilesheet));
            var sceneSource = Assert.Single(group.SceneSources);
            var scene = SceneDefinitionSerializer.Load(archive, sceneSource.AssetEntryName!, sceneSource.SceneId);
            Assert.Equal(SceneAnimationSourceKind.PackedDefinitionFile, Assert.Single(scene.AnimationSources).Kind);
            var animationSource = Assert.Single(group.AnimationSources);
            var animation = AnimationDefinitionSerializer.Load(archive, animationSource.AssetEntryName!, animationSource.AnimationKey);
            Assert.Equal(AnimationTilesheetSourceKind.PackedDefinitionFile, Assert.Single(animation.TilesheetSources).Kind);
        }
        Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionAsset(archive, "tiles.gts").Count);
        Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(archive, "cycles.gani").Count);
        Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(archive, "worlds.gscn").Count);
        Assert.Equal(count, SpriteDefinitionSerializer.LoadAllSprites(archive, "actors.gspr").Count);
        var backend = new Backend();
        AudioResourceManager.Instance.ConfigureBackend(backend);
        Assert.Equal(count, AudioDefinitionSerializer.LoadAllIntoManager(archive, "sounds.gsnd").Count);
        Assert.Equal(count, backend.Loaded.Count);
        Assert.All(backend.Loaded, data => Assert.Equal(new byte[] { data[0], 2, 3 }, data));
        foreach (var entry in archive.GetAllEntries().Where(entry => (int)entry.AssetType >= 7))
        {
            using var reader = new StreamReader(archive.Get(entry.AssetType, entry.AssetName)!);
            var json = reader.ReadToEnd();
            Assert.Contains("Definitions", json);
            Assert.DoesNotContain(".tmp", json);
            Assert.DoesNotContain(Source.Replace("\\", "\\\\"), json);
        }
    }

    /// <summary>Built-in, shipped, and partial maps retain every native type; explicit non-native overrides remain meaningful.</summary>
    [Fact]
    public void NativeInferenceAndConfigurationPrecedence()
    {
        var path = Path.Combine(_root, "map.json");
        File.WriteAllText(path, "{\"Misc\":[\"png\"],\"Image\":[\"custom\"]}");
        var custom = AssetTypeMap.Load(path, out var error)!;
        Assert.Null(error);
        var shipped = AssetTypeMap.Load(Path.Combine(Path.GetDirectoryName(typeof(AssetTypeMap).Assembly.Location)!, "gondwana-asset-types.json"), out error)!;
        Assert.Null(error);
        foreach (var map in new[] { AssetTypeMap.BuildDefault(), shipped, custom })
        {
            foreach (var (extension, type) in new[] { ("gts", AssetTypes.TilesheetDefinition), ("gani", AssetTypes.AnimationDefinition),
                ("gscn", AssetTypes.SceneDefinition), ("gspr", AssetTypes.SpriteDefinition), ("gsnd", AssetTypes.AudioDefinition) })
                Assert.Equal(type, map.Infer("test." + extension.ToUpperInvariant(), AssetTypes.Misc));
            Assert.Equal(AssetTypes.Font, map.Infer("font.ttf", AssetTypes.Misc));
            Assert.Equal(AssetTypes.Video, map.Infer("movie.mp4", AssetTypes.Misc));
            Assert.Equal(AssetTypes.Audio, map.Infer("sound.wav", AssetTypes.Misc));
            Assert.Equal(AssetTypes.Svg, map.Infer("shape.svg", AssetTypes.Misc));
            Assert.Equal(AssetTypes.Cursor, map.Infer("cursor.cur", AssetTypes.Misc));
            Assert.Equal(AssetTypes.Audio, map.Infer("unknown.xyz", AssetTypes.Audio));
        }
        Assert.Equal(AssetTypes.Misc, custom.Infer("image.png", AssetTypes.Image));
        Assert.Equal(AssetTypes.Image, custom.Infer("image.custom", AssetTypes.Misc));
    }

    /// <summary>Invalid configuration cannot silently demote native definitions or overwrite duplicate mappings.</summary>
    /// <param name="json">The invalid configuration.</param>
    [Theory]
    [InlineData("{\"Misc\":[\"gts\"]}")]
    [InlineData("{\"999\":[\"png\"]}")]
    [InlineData("{\"Image\":null}")]
    [InlineData("{\"Image\":[null]}")]
    [InlineData("{\"Image\":[\"\"]}")]
    [InlineData("{\"Image\":[\"png\"],\"Misc\":[\"PNG\"]}")]
    [InlineData("{\"Image\":[\"png\"],\"Image\":[\"jpg\"]}")]
    public void InvalidMapsFail(string json)
    {
        var path = Path.Combine(_root, "map.json");
        File.WriteAllText(path, json);
        Assert.Null(AssetTypeMap.Load(path, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    /// <summary>Append resolves omitted loose dependencies from existing entries and preserves unrelated bytes.</summary>
    [Fact]
    public void AppendUsesExistingDependenciesAndPreservesEncryption()
    {
        Image("art.png");
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "unchanged");
        Assert.Equal(0, Run("pack", Source, Output, "--password", "secret", "--encrypt"));
        File.Delete(Path.Combine(Source, "art.png"));
        File.Delete(Path.Combine(Source, "keep.txt"));
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "art.png")));
        Assert.Equal(0, Run("pack", Source, Output, "--append", "--password", "secret"));
        using (var archive = AssetsFile.LoadOrCreate(Output, "secret", false, register: false))
        {
            using var reader = new StreamReader(archive.Get(AssetTypes.Misc, "keep.txt")!);
            Assert.Equal("unchanged", reader.ReadToEnd());
            Assert.Equal("art.png", TilesheetDefinitionSerializer.Load(archive, "tiles.gts").Image.AssetEntryName);
        }
        using var zip = new ZipFile(File.OpenRead(Output));
        Assert.Contains(zip.Cast<ZipEntry>(), entry => entry.AESKeySize > 0);
    }

    /// <summary>Inferred sources between retained documents survive moving the archive and appending again.</summary>
    [Fact]
    public void AppendInferredRetainedSourcesRemainRelativeAfterMovingArchive()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            var sheet = Sheet("terrain", "unused");
            sheet.Image.FilePath = null;
            sheet.Image.AssetsFilePath = "game.gaf";
            sheet.Image.AssetEntryName = "art.png";
            Image("art.png");

            using var image = File.OpenRead(Path.Combine(Source, "art.png"));
            archive.Add(AssetTypes.Image, "art.png", image);

            using var tiles = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", tiles);

            using var animation = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                AnimationDefinitionSerializer.ToJson(new AnimationDefinition
                {
                    Key = "walk",
                    ThrottleTime = .2,
                    Frames = [new() { Tilesheet = "terrain" }]
                })));

            archive.Add(AssetTypes.AnimationDefinition, "walk.gani", animation);
            archive.Save();
        }

        File.Delete(Path.Combine(Source, "art.png"));
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "first append");
        Assert.Equal(0, Run("pack", Source, Output, "--append"));
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            var source = Assert.Single(AnimationDefinitionSerializer.Load(archive, "walk.gani").TilesheetSources);
            Assert.Equal("game.gaf", source.AssetsFilePath);
            Assert.Equal("tiles.gts", source.AssetEntryName);
        }
        var moved = Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "moved")).FullName, "game.gaf");
        File.Move(Output, moved);
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "second append");
        Assert.Equal(0, Run("pack", Source, moved, "--append"));
        using var movedArchive = AssetsFile.LoadOrCreate(moved, null, false, register: false);
        var movedSource = Assert.Single(AnimationDefinitionSerializer.Load(movedArchive, "walk.gani").TilesheetSources);
        Assert.Equal("game.gaf", movedSource.AssetsFilePath);
        Assert.Equal("terrain", TilesheetDefinitionSerializer.Load(movedArchive, movedSource.AssetEntryName!).Name);
    }

    /// <summary>Failed preflight and ambiguous inference preserve the existing archive byte for byte.</summary>
    [Fact]
    public void MissingAndAmbiguousDependenciesLeaveOutputIntact()
    {
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "original");
        Assert.Equal(0, Run("pack", Source, Output));
        var original = File.ReadAllBytes(Output);
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "missing.png")));
        Assert.NotEqual(0, Run("pack", Source, Output, "--allow-external"));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Image("missing.png");
        File.Copy(Path.Combine(Source, "tiles.gts"), Path.Combine(Source, "other.gts"));
        File.WriteAllText(Path.Combine(Source, "walk.gani"), AnimationDefinitionSerializer.ToJson(new AnimationDefinition() { Key = "walk", ThrottleTime = .2, Frames = [new() { Tilesheet = "terrain" }] }));
        var settings = new AssetsPackCommand.Settings { Source = Source, Output = Output };
        var error = Assert.Throws<InvalidDataException>(() => SemanticPackPlan.Create(settings, AssetTypeMap.BuildDefault(), AssetTypes.Misc, default));
        Assert.Contains("terrain", error.Message);
        Assert.Contains("walk", error.Message);
        Assert.Contains("2 matching", error.Message);
        Assert.Equal(original, File.ReadAllBytes(Output));
    }

    /// <summary>External resources require opt-in and retain paths relative to the final archive.</summary>
    [Fact]
    public void ExternalReferencesRequireExplicitPolicy()
    {
        Image("art.png");
        var external = Path.Combine(_root, "art.png");
        File.Move(Path.Combine(Source, "art.png"), external);
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "../art.png")));
        Assert.NotEqual(0, Run("pack", Source, Output));
        Assert.False(File.Exists(Output));
        Assert.Equal(0, Run("pack", Source, Output, "--allow-external"));
        using var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        var sheet = TilesheetDefinitionSerializer.Load(archive, "tiles.gts");
        Assert.Equal(external, Path.GetFullPath(sheet.Image.FilePath!, Path.GetDirectoryName(Output)!));
        Assert.Null(sheet.Image.AssetEntryName);
    }

    /// <summary>Repeated packing excludes its own output and replaces stale entries.</summary>
    [Fact]
    public void OutputInsideSourceIsExcludedOnRepeatedPacking()
    {
        var output = Path.Combine(Source, "game.gaf");
        File.WriteAllText(Path.Combine(Source, "old.txt"), "old");
        Assert.Equal(0, Run("pack", Source, output));
        File.Delete(Path.Combine(Source, "old.txt"));
        File.WriteAllText(Path.Combine(Source, "new.txt"), "new");
        Assert.Equal(0, Run("pack", Source, output));
        using var archive = AssetsFile.LoadOrCreate(output, null, false, register: false);
        Assert.Equal("new.txt", Assert.Single(archive.GetAllEntries()).AssetName);
    }

    /// <summary>Cancellation after preflight cannot modify an existing output or leave staging files.</summary>
    [Fact]
    public void CancellationBeforeCommitPreservesOutput()
    {
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "original");
        Assert.Equal(0, Run("pack", Source, Output));
        var original = File.ReadAllBytes(Output);
        using var cancellation = new CancellationTokenSource();
        var plan = SemanticPackPlan.Create(new AssetsPackCommand.Settings() { Source = Source, Output = Output }, AssetTypeMap.BuildDefault(), AssetTypes.Misc, cancellation.Token);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => plan.Commit());
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Output)!, "*.tmp"));
    }

    /// <summary>Replacing a whole document cannot strand references in retained append entries.</summary>
    [Fact]
    public void AppendRejectsRemovalOfReferencedMember()
    {
        Content(true, false);
        Assert.Equal(0, Run("pack", Source, Output));
        var original = File.ReadAllBytes(Output);
        foreach (var file in Directory.GetFiles(Source)) File.Delete(file);
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(Sheet("terrain0", "art0.png")));
        Assert.NotEqual(0, Run("pack", Source, Output, "--append"));
        Assert.Equal(original, File.ReadAllBytes(Output));
    }

    /// <summary>A commit failure cleans staging files without truncating the original output.</summary>
    [Fact]
    public void FailedCommitPreservesOutput()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows sharing rules provide a deterministic replacement failure.
        File.WriteAllText(Path.Combine(Source, "keep.txt"), "original");
        Assert.Equal(0, Run("pack", Source, Output));
        var original = File.ReadAllBytes(Output);
        var plan = SemanticPackPlan.Create(new AssetsPackCommand.Settings() { Source = Source, Output = Output }, AssetTypeMap.BuildDefault(), AssetTypes.Misc, default);
        using (var locked = File.Open(Output, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.True(Record.Exception(() => plan.Commit()) is IOException or UnauthorizedAccessException);
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Output)!, "*.tmp"));
    }

    /// <summary>Explicit packed external resources are verified, while URI audio stays untouched.</summary>
    [Fact]
    public void PackedExternalAndUriReferencesRemainExplicit()
    {
        Image("art.png");
        var external = Path.Combine(_root, "external.gaf");
        Assert.Equal(0, Run("pack", Source, external));
        File.Delete(Path.Combine(Source, "art.png"));
        var sheet = Sheet("terrain", "unused");
        sheet.Image.FilePath = null;
        sheet.Image.AssetsFilePath = "../external.gaf";
        sheet.Image.AssetEntryName = "art.png";
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(sheet));
        File.WriteAllText(Path.Combine(Source, "sounds.gsnd"), AudioDefinitionSerializer.ToJson(new AudioDefinition
        { Key = "stream", Resources = [new() { Key = "radio", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.invalid/radio" }] }));
        Assert.NotEqual(0, Run("pack", Source, Output));
        Assert.Equal(0, Run("pack", Source, Output, "--allow-external"));
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            var packed = TilesheetDefinitionSerializer.Load(archive, "tiles.gts");
            Assert.Equal(external, Path.GetFullPath(packed.Image.AssetsFilePath!, Path.GetDirectoryName(Output)!));
            Assert.Equal("https://example.invalid/radio", Assert.Single(AudioDefinitionSerializer.Load(archive, "sounds.gsnd").Resources).SourceUri);
        }
        sheet.Image.AssetEntryName = "missing.png";
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(sheet));
        var before = File.ReadAllBytes(Output);
        Assert.NotEqual(0, Run("pack", Source, Output, "--allow-external"));
        Assert.Equal(before, File.ReadAllBytes(Output));
    }

    /// <summary>Distinct typed dependencies and normalized path aliases share one per-plan archive cache.</summary>
    /// <param name="protection">Zero for none, one for password, two for AES.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PackedExternalDependenciesShareArchiveContents(int protection)
    {
        var external = Path.Combine(_root, "external.gaf");
        var password = protection == 0 ? null : "test-password";
        using (var archive = AssetsFile.LoadOrCreate(external, password, protection == 2, register: false))
        {
            foreach (var (type, name) in new[] { (AssetTypes.Image, "first.png"), (AssetTypes.Image, "second.png"), (AssetTypes.Audio, "first.png") })
            {
                using var stream = new MemoryStream([(byte)type, 2, 3]);
                archive.Add(type, name, stream);
            }
            archive.Save();
        }
        var sheets = new[] { Sheet("first", "unused"), Sheet("second", "unused") };
        for (int i = 0; i < sheets.Length; i++)
        {
            sheets[i].Image.FilePath = null;
            sheets[i].Image.AssetsFilePath = i == 0 ? "../external.gaf" : "../source/../external.gaf";
            sheets[i].Image.AssetEntryName = i == 0 ? "FIRST.PNG" : "second.png";
        }
        File.WriteAllText(Path.Combine(Source, "tiles.gts"), TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = sheets.ToList() }));
        File.WriteAllText(Path.Combine(Source, "sounds.gsnd"), AudioDefinitionSerializer.ToJson(new AudioDefinition
        {
            Key = "sounds",
            Resources = [new() { Key = "sound", SourceKind = AudioResourceSourceKind.PackedAsset, AssetsFilePath = "../external.gaf", AssetEntryName = "first.png" }]
        }));

        var plan = SemanticPackPlan.Create(new AssetsPackCommand.Settings() { Source = Source, Output = Output, Password = password, AllowExternal = true },
            AssetTypeMap.BuildDefault(), AssetTypes.Misc, default);
        var cache = Assert.IsType<Dictionary<string, Dictionary<AssetsFileEntry, byte[]>>>(typeof(SemanticPackPlan)
            .GetField("_externalArchives", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(plan));
        var contents = Assert.Single(cache);
        Assert.Equal(external, contents.Key);
        Assert.Equal(3, contents.Value.Count);
        Assert.Equal(new byte[] { (byte)AssetTypes.Image, 2, 3 }, contents.Value[new() { AssetType = AssetTypes.Image, AssetName = "FIRST.PNG" }]);
        Assert.Equal(new byte[] { (byte)AssetTypes.Audio, 2, 3 }, contents.Value[new() { AssetType = AssetTypes.Audio, AssetName = "first.png" }]);
        Assert.Equal(2, plan.Commit());
        using var packed = AssetsFile.LoadOrCreate(Output, password, false, register: false);
        Assert.Equal(2, TilesheetDefinitionSerializer.LoadAll(packed, "tiles.gts").Count);
        Assert.Single(AudioDefinitionSerializer.Load(packed, "sounds.gsnd").Resources);
        Assert.Equal(2, packed.GetAllEntries().Count());
    }

    /// <summary>Nested document paths resolve against their containing directory and load without loose resources.</summary>
    [Fact]
    public void NestedRelativeDependenciesRemainUsable()
    {
        Image("art.png");
        var definitions = Directory.CreateDirectory(Path.Combine(Source, "definitions")).FullName;
        File.WriteAllText(Path.Combine(definitions, "tiles.gts"), TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "../art.png")));
        Assert.Equal(0, Run("pack", Source, Output));
        Directory.Delete(Source, true);
        var archive = AssetsFile.LoadOrCreate(Output);
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "definitions/tiles.gts").Name);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Clear();
        Directory.Delete(_root, true);
    }
    private sealed class Backend : IAudioBackend
    {
        /// <inheritdoc/>
        public string Name => "Semantic pack test";
        /// <summary>Gets the media bytes supplied by Core.</summary>
        public List<byte[]> Loaded { get; } = [];
        /// <summary>Gets the format hints passed to the backend.</summary>
        public List<string> Formats { get; } = [];
        /// <inheritdoc/>
        public IAudioPlaybackHandle CreateFromUri(string key, string uri, float volume, float pan, float playbackSpeed) => new Handle();
        /// <inheritdoc/>
        public IAudioPlaybackHandle CreateFromBytes(string key, byte[] data, string fileNameOrExtension, float volume, float pan, float playbackSpeed) { Loaded.Add(data); Formats.Add(fileNameOrExtension); return new Handle(); }
    }

    private sealed class Handle : IAudioPlaybackHandle
    {
        /// <inheritdoc/>
        public event EventHandler? PlaybackCompleted { add { } remove { } }
        /// <inheritdoc/>
        public AudioPlaybackState State => AudioPlaybackState.Stopped;
        /// <inheritdoc/>
        public TimeSpan CurrentTime => TimeSpan.Zero;
        /// <inheritdoc/>
        public TimeSpan Duration => TimeSpan.Zero;
        /// <inheritdoc/>
        public bool IsLooping { get; set; }
        /// <inheritdoc/>
        public float Volume { get; set; }
        /// <inheritdoc/>
        public float Pan { get; set; }
        /// <inheritdoc/>
        public float PlaybackSpeed { get; set; }
        /// <inheritdoc/>
        public string? TemporaryFilePath => null;
        /// <inheritdoc/>
        public void Play(bool fromStart = true) { }
        /// <inheritdoc/>
        public void Pause() { }
        /// <inheritdoc/>
        public void Resume() { }
        /// <inheritdoc/>
        public void Stop() { }
        /// <inheritdoc/>
        public void Seek(TimeSpan position) { }
        /// <summary>
        /// Releases the resources owned by this instance.
        /// </summary>
        public void Dispose() { }
    }
}
