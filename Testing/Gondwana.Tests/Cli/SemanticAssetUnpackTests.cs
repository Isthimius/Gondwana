using System.Diagnostics;
using System.Text;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Audio.GSND;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    private string Export => Path.Combine(_root, "export");

    /// <summary>All formats survive semantic pack/export and Core materialization without either original source.</summary>
    /// <param name="legacy">Whether the loose inputs are historical singletons.</param>
    /// <param name="protection">Zero for plain, one for password, two for AES.</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public void SemanticUnpackRoundTrip(bool legacy, int protection)
    {
        Content(true, legacy);
        var nested = Directory.CreateDirectory(Path.Combine(Source, "native")).FullName;
        foreach (var path in Directory.GetFiles(Source).Where(file => Path.GetExtension(file) is ".gts" or ".gani" or ".gscn" or ".gspr" or ".gsnd"))
        {
            var json = File.ReadAllText(path).Replace("\"art", "\"../art").Replace("\"sound0.wav\"", "\"../sound0.wav\"").Replace("\"sound1.wav\"", "\"../sound1.wav\"");
            File.WriteAllText(Path.Combine(nested, Path.GetFileName(path)), json);
            File.Delete(path);
        }
        var pack = new List<string> { "pack", Source, Output };
        var unpack = new List<string> { "unpack", Output, Export };
        if (protection > 0) { pack.AddRange(["--password", "secret"]); unpack.AddRange(["--password", "secret"]); }
        if (protection == 2) pack.Add("--encrypt");
        Assert.Equal(0, Run(pack.ToArray()));
        var original = File.ReadAllBytes(Output);
        Assert.Equal(0, Run(unpack.ToArray()));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Directory.Delete(Source, true);
        File.Delete(Output);
        int count = legacy ? 1 : 2;
        string PathFor(string name) => Path.Combine(Export, "native", name);
        var sheets = TilesheetDefinitionSerializer.LoadAll(PathFor("tiles.gts"));
        Assert.Equal(Enumerable.Range(0, count).Select(i => "terrain" + i), sheets.Select(sheet => sheet.Name));
        foreach (var sheet in sheets)
        {
            Assert.Null(sheet.Image.AssetEntryName);
            Assert.False(Path.IsPathRooted(sheet.Image.FilePath));
        }
        Assert.All(AnimationDefinitionSerializer.LoadAll(PathFor("cycles.gani")), animation =>
        {
            Assert.Equal(AnimationTilesheetSourceKind.LooseDefinitionFile, Assert.Single(animation.TilesheetSources).Kind);
            Assert.StartsWith("walk", animation.NextCycleKey);
        });
        Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(PathFor("tiles.gts")).Count);
        Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(PathFor("cycles.gani")).Count);
        Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(PathFor("worlds.gscn")).Count);
        Assert.Equal(count, SpriteDefinitionSerializer.LoadAllSprites(PathFor("actors.gspr")).Count);
        var backend = new Backend();
        AudioResourceManager.Instance.ConfigureBackend(backend);
        Assert.Equal(count, AudioDefinitionSerializer.LoadAllIntoManager(PathFor("sounds.gsnd")).Count);
        Assert.Equal(count, backend.Loaded.Count);
        foreach (var path in Directory.GetFiles(Path.Combine(Export, "native")))
        {
            var json = File.ReadAllText(path);
            Assert.Contains("Definitions", json);
            Assert.DoesNotContain(".gondwana-unpack-", json);
            Assert.DoesNotContain(_root.Replace("\\", "\\\\"), json);
        }
        Assert.Equal(0, Run("pack", Export, Output));
        using var repacked = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        Assert.Equal(count, SpriteDefinitionSerializer.LoadAll(repacked, "native/actors.gspr").Count);
        Assert.Equal("native/tiles.gts", Assert.Single(AnimationDefinitionSerializer.Load(repacked, "native/cycles.gani", "walk0").TilesheetSources).AssetEntryName);
    }

    /// <summary>Every type can be exported alone while its dependencies remain usable in the original GAF.</summary>
    /// <param name="type">The definition type selected for export.</param>
    /// <param name="name">The physical entry name.</param>
    [Theory]
    [InlineData(AssetTypes.TilesheetDefinition, "tiles.gts")]
    [InlineData(AssetTypes.AnimationDefinition, "cycles.gani")]
    [InlineData(AssetTypes.SceneDefinition, "worlds.gscn")]
    [InlineData(AssetTypes.SpriteDefinition, "actors.gspr")]
    [InlineData(AssetTypes.AudioDefinition, "sounds.gsnd")]
    public void PartialUnpackLoadsRetainedPackedDependencies(AssetTypes type, string name)
    {
        Content(true, false);
        Assert.Equal(0, Run("pack", Source, Output));
        var before = File.ReadAllBytes(Output);
        Directory.Delete(Source, true);
        Assert.Equal(0, Run("extract", Output, Export, "--type", type.ToString()));
        Assert.Equal(name, Path.GetFileName(Assert.Single(Directory.GetFiles(Export))));
        var path = Path.Combine(Export, name);
        AssetsFile Open(string? archive) => AssetsFile.LoadOrCreate(Path.GetFullPath(archive!, Export));
        switch (type)
        {
            case AssetTypes.TilesheetDefinition:
                Assert.Equal(2, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(path).Count);
                break;
            case AssetTypes.AnimationDefinition:
                foreach (var animation in AnimationDefinitionSerializer.LoadAll(path))
                    foreach (var source in animation.TilesheetSources)
                        TilesheetRegistry.Instance.LoadFromDefinitionAsset(Open(source.AssetsFilePath), source.AssetEntryName!, source.Tilesheet);
                Assert.Equal(2, AnimationDefinitionSerializer.LoadAllCycles(path).Count);
                break;
            case AssetTypes.SceneDefinition:
                var scenes = SceneDefinitionSerializer.LoadAll(path);
                foreach (var source in scenes.SelectMany(scene => scene.TilesheetSources))
                    TilesheetRegistry.Instance.LoadFromDefinitionAsset(Open(source.AssetsFilePath), source.AssetEntryName!, source.Tilesheet);
                foreach (var source in scenes.SelectMany(scene => scene.AnimationSources).DistinctBy(source => (source.AssetsFilePath, source.AssetEntryName)))
                    AnimationDefinitionSerializer.LoadAllCycles(Open(source.AssetsFilePath), source.AssetEntryName!);
                Assert.Equal(2, SceneDefinitionSerializer.LoadAllScenes(path).Count);
                break;
            case AssetTypes.SpriteDefinition:
                var groups = SpriteDefinitionSerializer.LoadAll(path);
                foreach (var source in groups.SelectMany(group => group.TilesheetSources))
                    TilesheetRegistry.Instance.LoadFromDefinitionAsset(Open(source.AssetsFilePath), source.AssetEntryName!, source.Tilesheet);
                foreach (var source in groups.SelectMany(group => group.AnimationSources).DistinctBy(source => (source.AssetsFilePath, source.AssetEntryName)))
                    AnimationDefinitionSerializer.LoadAllCycles(Open(source.AssetsFilePath), source.AssetEntryName!);
                foreach (var source in groups.SelectMany(group => group.SceneSources))
                    SceneDefinitionSerializer.LoadScene(Open(source.AssetsFilePath), source.AssetEntryName!, source.SceneId);
                Assert.Equal(2, SpriteDefinitionSerializer.LoadAllSprites(path).Count);
                break;
            case AssetTypes.AudioDefinition:
                foreach (var resource in AudioDefinitionSerializer.LoadAll(path).SelectMany(group => group.Resources)) Open(resource.AssetsFilePath);
                var backend = new Backend();
                AudioResourceManager.Instance.ConfigureBackend(backend);
                Assert.Equal(2, AudioDefinitionSerializer.LoadAllIntoManager(path).Count);
                Assert.Equal(2, backend.Loaded.Count);
                break;
        }
        Assert.Equal(before, File.ReadAllBytes(Output));
    }

    private void Archive(params (AssetTypes Type, string Name, byte[] Bytes)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        foreach (var entry in entries)
        {
            using var stream = new MemoryStream(entry.Bytes);
            archive.Add(entry.Type, entry.Name, stream);
        }
        archive.Save();
    }

    /// <summary>Implicit same-GAF images become explicit for partial export and loose for full export.</summary>
    /// <param name="partial">Whether only the GTS entry is selected.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImplicitSameGafImageExports(bool partial)
    {
        Image("art.png");
        var sheet = Sheet("terrain", "unused");
        sheet.Image = new() { AssetEntryName = "images/art.png" };
        Archive((AssetTypes.Image, "images/art.png", File.ReadAllBytes(Path.Combine(Source, "art.png"))),
            (AssetTypes.TilesheetDefinition, "tiles/terrain.gts", Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet))));
        var args = new List<string> { "unpack", Output, Export };
        if (partial) args.AddRange(["--entry", "tiles/terrain.gts"]);
        Assert.Equal(0, Run(args.ToArray()));
        var definition = TilesheetDefinitionSerializer.Load(Path.Combine(Export, "tiles", "terrain.gts"));
        Assert.Equal(partial, definition.Image.AssetEntryName is not null);
        if (!partial) File.Delete(Output);
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionFile(Path.Combine(Export, "tiles", "terrain.gts")).Name);
    }

    /// <summary>Exact entry selection rejects ambiguity and basename lookup; a typed selection preserves original bytes.</summary>
    [Fact]
    public void ExactTypedSelectionAndSourceProtection()
    {
        Archive((AssetTypes.Image, "same.bin", [1]), (AssetTypes.Audio, "same.bin", [2]), (AssetTypes.Misc, "nested/one.bin", [3]));
        Assert.NotEqual(0, Run("unpack", Output, Export, "--entry", "same.bin"));
        Assert.NotEqual(0, Run("unpack", Output, Export, "--entry", "one.bin"));
        Assert.False(Directory.Exists(Export));
        Assert.Equal(0, Run("unpack", Output, Export, "--entry", "SAME.bin", "--type", "Audio"));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(Export, "same.bin")));
        Archive((AssetTypes.Misc, "game.gaf", [4]));
        var before = File.ReadAllBytes(Output);
        Assert.NotEqual(0, Run("unpack", Output, Path.GetDirectoryName(Output)!, "--entry", "game.gaf", "--overwrite"));
        Assert.Equal(before, File.ReadAllBytes(Output));
    }

    /// <summary>A late commit failure restores earlier overwritten files and removes transaction artifacts.</summary>
    [Fact]
    public void UnpackRollbackRestoresEarlierFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        Archive((AssetTypes.Misc, "a.txt", [1]), (AssetTypes.Misc, "z.txt", [2]));
        Directory.CreateDirectory(Export);
        var first = Path.Combine(Export, "a.txt");
        var last = Path.Combine(Export, "z.txt");
        File.WriteAllText(first, "first original");
        File.WriteAllText(last, "last original");
        var plan = SemanticUnpackPlan.Create(Output, Export, null, null, null, true, default);
        using (var locked = File.Open(last, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.True(Record.Exception(() => plan.Commit()) is IOException or UnauthorizedAccessException);
        Assert.Equal("first original", File.ReadAllText(first));
        Assert.Equal("last original", File.ReadAllText(last));
        Assert.Empty(Directory.GetDirectories(Export));
    }

    /// <summary>Cancellation and malformed dependencies cannot leave partially exported output.</summary>
    [Fact]
    public void UnpackCancellationAndMissingDependencyPreflight()
    {
        Archive((AssetTypes.Misc, "a.txt", [1]));
        using var cancellation = new CancellationTokenSource();
        var plan = SemanticUnpackPlan.Create(Output, Export, null, null, null, false, cancellation.Token);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => plan.Commit());
        Assert.False(Directory.Exists(Export));
        var sheet = Sheet("missing-owner", "unused");
        sheet.Image = new() { AssetEntryName = "missing.png" };
        Archive((AssetTypes.TilesheetDefinition, "sheet.gts", Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet))));
        var error = Assert.Throws<InvalidDataException>(() => SemanticUnpackPlan.Create(Output, Export, null, null, null, false, default));
        Assert.Contains("missing-owner", error.Message);
        Assert.Contains("missing.png", error.Message);
        Assert.False(Directory.Exists(Export));
    }

    /// <summary>External loose and packed references remain at their original targets after rebasing.</summary>
    [Fact]
    public void UnpackPreservesExternalReferences()
    {
        Image("art.png");
        var image = File.ReadAllBytes(Path.Combine(Source, "art.png"));
        var loose = Path.Combine(_root, "art.png");
        File.WriteAllBytes(loose, image);
        var external = Path.Combine(_root, "external.gaf");
        using (var archive = AssetsFile.LoadOrCreate(external, null, false, register: false))
        {
            using var stream = new MemoryStream(image);
            archive.Add(AssetTypes.Image, "art.png", stream);
            archive.Save();
        }
        var looseSheet = Sheet("loose", "../art.png");
        var packedSheet = Sheet("packed", "unused");
        packedSheet.Image = new() { AssetsFilePath = "../external.gaf", AssetEntryName = "art.png" };
        Archive((AssetTypes.Image, "art.png", [0]), // Must not capture the external loose reference by basename.
            (AssetTypes.TilesheetDefinition, "nested/tiles.gts", Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [looseSheet, packedSheet] }))));
        Assert.Equal(0, Run("unpack", Output, Export, "--type", "TilesheetDefinition"));
        var path = Path.Combine(Export, "nested", "tiles.gts");
        var definitions = TilesheetDefinitionSerializer.LoadAll(path);
        Assert.Equal(loose, Path.GetFullPath(definitions[0].Image.FilePath!, Path.GetDirectoryName(path)!));
        Assert.Equal(external, Path.GetFullPath(definitions[1].Image.AssetsFilePath!, Path.GetDirectoryName(path)!));
        File.Delete(Output);
        Assert.Equal(2, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(path).Count);
    }

    /// <summary>Exported extensionless audio retains its format for playback, source serialization, and reload; URI audio is unchanged.</summary>
    [Fact]
    public void ExtensionlessAudioPreservesFormatAndLooseProvenance()
    {
        var audio = new AudioDefinition
        {
            Key = "sounds",
            Resources =
        [
            new() { Key = "sound", SourceKind = AudioResourceSourceKind.PackedAsset, AssetsFilePath = "game.gaf", AssetEntryName = "audio/track", SourceExtension = ".ogg" },
            new() { Key = "radio", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.invalid/radio" }
        ]
        };
        Archive((AssetTypes.Audio, "audio/track", [1, 2, 3]),
            (AssetTypes.AudioDefinition, "definitions/sounds.gsnd", Encoding.UTF8.GetBytes(AudioDefinitionSerializer.ToJson(audio))));
        Assert.Equal(0, Run("unpack", Output, Export));
        File.Delete(Output);
        var path = Path.Combine(Export, "definitions", "sounds.gsnd");
        var definition = AudioDefinitionSerializer.Load(path);
        Assert.Equal("https://example.invalid/radio", definition.Resources[1].SourceUri);
        Assert.Equal(".ogg", definition.Resources[0].SourceExtension);
        var backend = new Backend();
        AudioResourceManager.Instance.ConfigureBackend(backend);
        var resources = AudioDefinitionSerializer.LoadIntoManager(path);
        Assert.Equal(".ogg", Assert.Single(backend.Formats));
        Assert.Equal(Path.Combine(Export, "audio", "track"), resources[0].SourceFilePath);
        resources[0].ReloadIntoManager(forceReload: true);
        Assert.Equal(new[] { ".ogg", ".ogg" }, backend.Formats);
    }

    /// <summary>Output path conflicts and symlink ancestors are rejected before any files are created.</summary>
    [Fact]
    public void UnpackRejectsFileDirectoryAndLinkConflicts()
    {
        Archive((AssetTypes.Misc, "folder", [1]), (AssetTypes.Misc, "folder/child", [2]));
        Assert.NotEqual(0, Run("unpack", Output, Export));
        Assert.False(Directory.Exists(Export));
        var elsewhere = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;
        var link = Path.Combine(_root, "link");
        if (OperatingSystem.IsWindows())
        {
            // Directory junction creation does not require the symbolic-link privilege.
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{elsewhere}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, elsewhere);
        try
        {
            Assert.ThrowsAny<IOException>(() => SemanticUnpackPlan.Create(Output, Path.Combine(link, "child"), null, AssetTypes.Misc, "folder", false, default));
            Assert.Empty(Directory.GetFileSystemEntries(elsewhere));
        }
        finally { Directory.Delete(link); }
    }

    /// <summary>Physical source ambiguity cannot be silently resolved from one of several matching logical members.</summary>
    [Fact]
    public void UnpackRejectsAmbiguousLogicalDependency()
    {
        var sheet = Sheet("terrain", "unused");
        sheet.Image = new() { AssetEntryName = "art.png" };
        var bytes = Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet));
        var animation = new AnimationDefinition { Key = "walk", ThrottleTime = .2, Frames = [new() { Tilesheet = "terrain" }] };
        Archive((AssetTypes.TilesheetDefinition, "one.gts", bytes), (AssetTypes.TilesheetDefinition, "two.gts", bytes),
            (AssetTypes.AnimationDefinition, "walk.gani", Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(animation))));
        var error = Assert.Throws<InvalidDataException>(() => SemanticUnpackPlan.Create(Output, Export, null, null, "walk.gani", false, default));
        Assert.Contains("walk", error.Message);
        Assert.Contains("terrain", error.Message);
        Assert.Contains("2 matching", error.Message);
        Assert.False(Directory.Exists(Export));
    }

    /// <summary>A scene can combine extracted tilesheets with animation dependencies retained in an external GAF.</summary>
    [Fact]
    public void ExportSceneWithMixedLooseAndRetainedPackedDependencies()
    {
        Image("art.png");
        var external = Path.Combine(_root, "animations.gaf");
        using (var archive = AssetsFile.LoadOrCreate(external, null, false, register: false))
        {
            var animation = new AnimationDefinition { Key = "walk", ThrottleTime = .2, Frames = [new() { Tilesheet = "terrain" }] };
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(animation)));
            archive.Add(AssetTypes.AnimationDefinition, "walk.gani", stream);
            archive.Save();
        }
        var externalBytes = File.ReadAllBytes(external);
        var sheet = Sheet("terrain", "unused");
        sheet.Image = new() { AssetEntryName = "art.png" };
        var scene = new SceneDefinition
        {
            ID = "world",
            Layers = [new() { ID = "ground", Columns = 1, Rows = 1,
            Tiles = [new() { Frame = new() { Tilesheet = "terrain" }, AnimationKey = "walk" }] }],
            TilesheetSources = [SceneTilesheetSourceDefinition.Packed("terrain", "game.gaf", "tiles.gts")],
            AnimationSources = [SceneAnimationSourceDefinition.Packed("walk", "../animations.gaf", "walk.gani")]
        };
        Archive((AssetTypes.Image, "art.png", File.ReadAllBytes(Path.Combine(Source, "art.png"))),
            (AssetTypes.TilesheetDefinition, "tiles.gts", Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet))),
            (AssetTypes.SceneDefinition, "scenes/world.gscn", Encoding.UTF8.GetBytes(SceneDefinitionSerializer.ToJson(scene))));
        Assert.Equal(0, Run("unpack", Output, Export));
        File.Delete(Output);
        var path = Path.Combine(Export, "scenes", "world.gscn");
        var result = SceneDefinitionSerializer.Load(path);
        var tiles = Assert.Single(result.TilesheetSources);
        var cycles = Assert.Single(result.AnimationSources);
        Assert.Equal(SceneTilesheetSourceKind.LooseDefinitionFile, tiles.Kind);
        Assert.Equal(SceneAnimationSourceKind.PackedDefinitionFile, cycles.Kind);
        TilesheetRegistry.Instance.LoadFromDefinitionFile(Path.GetFullPath(tiles.GtsPath!, Path.GetDirectoryName(path)!), tiles.Tilesheet);
        AnimationDefinitionSerializer.LoadCycle(AssetsFile.LoadOrCreate(Path.GetFullPath(cycles.AssetsFilePath!, Path.GetDirectoryName(path)!)), cycles.AssetEntryName!, cycles.AnimationKey);
        Assert.Equal("world", SceneDefinitionSerializer.LoadScene(path).ID);
        Assert.Equal(externalBytes, File.ReadAllBytes(external));
    }
}
