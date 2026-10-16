using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    /// <summary>Recursive and external imports preserve all members, source files, unsaved edits, and encryption.</summary>
    /// <param name="dependencies">Whether to import the recursive dependency closure.</param>
    /// <param name="legacy">Whether input definitions are legacy singletons.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void EditorImportMaterializesWholeHierarchy(bool dependencies, bool legacy)
    {
        Content(true, legacy);
        var original = Directory.GetFiles(Source).ToDictionary(path => path, File.ReadAllBytes);
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var destination = AssetsFile.LoadOrCreate(Output, "secret", true, register: false))
        {
            using var unsaved = new MemoryStream([42]);
            destination.Add(AssetTypes.Misc, "unsaved", unsaved);
            string[] roots = ["actors.gspr", "sounds.gsnd"];
            var sources = roots.Select(name => new AssetImportSource(Path.Combine(Source, name), AssetImportPlan.InferType(name), name));
            var plan = AssetImportPlan.Create(destination, sources, dependencies);
            Assert.Single(destination.GetAllEntries());
            Assert.Contains(plan.Preview, line => line.StartsWith(dependencies ? "Rewrite" : "External"));
            Assert.Equal(plan.AdditionCount, plan.Apply());
            Assert.False(File.Exists(Output));
            Assert.Equal("secret", destination.Password);
            Assert.True(destination.UseEncryption);
            Assert.Equal(original.Count(pair => roots.Contains(Path.GetFileName(pair.Key))) + (dependencies ? (legacy ? 5 : 7) : 0), plan.AdditionCount);
            destination.Save();
        }
        foreach (var pair in original) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        if (dependencies) Directory.Delete(Source, true);
        var archive = AssetsFile.LoadOrCreate(Output, "secret");
        using (var unsaved = archive.Get(AssetTypes.Misc, "unsaved")) Assert.Equal(42, unsaved!.ReadByte());
        int count = legacy ? 1 : 2;
        if (dependencies)
        {
            Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionAsset(archive, "tiles.gts").Count);
            Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(archive, "cycles.gani").Count);
            Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(archive, "worlds.gscn").Count);
        }
        else
        {
            var group = SpriteDefinitionSerializer.LoadAll(archive, "actors.gspr")[0];
            string Resolve(string path) => Path.GetFullPath(path, Path.GetDirectoryName(Output)!);
            Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(Resolve(group.TilesheetSources[0].GtsPath!)).Count);
            Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(Resolve(group.AnimationSources[0].GaniPath!)).Count);
            Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(Resolve(group.SceneSources[0].GscnPath!)).Count);
        }
        Assert.Equal(count, SpriteDefinitionSerializer.LoadAllSprites(archive, "actors.gspr").Count);
        var backend = new Backend();
        AudioResourceManager.Instance.ConfigureBackend(backend);
        Assert.Equal(count, AudioDefinitionSerializer.LoadAllIntoManager(archive, "sounds.gsnd").Count);
        Assert.Equal(count, backend.Loaded.Count);
    }

    /// <summary>Existing entries are reused only by exact packed identity or verified identical leaf bytes.</summary>
    [Fact]
    public void EditorImportReusesCurrentUnsavedEntries()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        using (var stream = File.OpenRead(Path.Combine(Source, "art.png"))) destination.Add(AssetTypes.Image, "art.png", stream);
        var path = Path.Combine(Source, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, Sheet("terrain", "art.png"));
        var plan = AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "renamed.gts")], true);
        Assert.Single(destination.GetAllEntries());
        Assert.Contains(plan.Preview, line => line.StartsWith("Reuse identical"));
        Assert.Equal(1, plan.Apply());
        Assert.Equal("terrain", TilesheetDefinitionSerializer.Load(destination, "renamed.gts").Name);
        Assert.Equal("art.png", TilesheetDefinitionSerializer.Load(destination, "renamed.gts").Image.AssetEntryName);
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "renamed.gts")], true));
        Assert.False(File.Exists(Output));
    }

    /// <summary>Selected loose leaves reused at custom packed names resolve under either dependency policy.</summary>
    /// <param name="dependencies">Whether to import external dependencies.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditorImportResolvesSelectedLeafReusedAtCustomDestination(bool dependencies)
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        using (var image = File.OpenRead(Path.Combine(Source, "art.png")))
            destination.Add(AssetTypes.Image, "images/art.png", image);
        var path = Path.Combine(Source, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, Sheet("terrain", "art.png"));

        var plan = AssetImportPlan.Create(destination,
        [
            new(path, AssetTypes.TilesheetDefinition, "sheet.gts"),
            new(Path.Combine(Source, "art.png"), AssetTypes.Image, "images/art.png")
        ], dependencies);

        Assert.Contains(plan.Preview, line => line.StartsWith("Reuse identical"));
        Assert.Equal(1, plan.AdditionCount);
        Assert.Equal(1, plan.Apply());
        var imageReference = TilesheetDefinitionSerializer.Load(destination, "sheet.gts").Image!;
        Assert.Equal(Output, imageReference.AssetsFilePath);
        Assert.Equal("images/art.png", imageReference.AssetEntryName);
    }

    /// <summary>Reused definitions retain external dependencies even when the same source is imported.</summary>
    [Fact]
    public void EditorImportReuseReportsRetainedExternalDependency()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        var physicalImage = Path.Combine(Path.GetDirectoryName(Output)!, "art.png");
        File.Copy(Path.Combine(Source, "art.png"), physicalImage);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "art.png")))))
            destination.Add(AssetTypes.TilesheetDefinition, "terrain.gts", stream);
        var animationPath = Path.Combine(Source, "walk.gani");
        AnimationDefinitionSerializer.Save(animationPath, new AnimationDefinition
        {
            Key = "walk",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain" }]
        });

        var plan = AssetImportPlan.Create(destination,
        [
            new(physicalImage, AssetTypes.Image, "art.png"),
            new(animationPath, AssetTypes.AnimationDefinition, "walk.gani")
        ], false);

        Assert.Contains(plan.Preview, line => line.StartsWith("External:") && line.Contains("art.png", StringComparison.Ordinal));
        Assert.Equal(2, plan.Apply());
        Assert.Equal("art.png", TilesheetDefinitionSerializer.Load(destination, "terrain.gts").Image.FilePath);
        Assert.False(File.Exists(Output));
    }

    /// <summary>Prepared transactions reject stale previews, preserve current bytes on failure, and honor cancellation.</summary>
    [Fact]
    public void EditorImportRollbackAndStalePlan()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        File.WriteAllBytes(Path.Combine(Source, "one.bin"), [1]);
        File.WriteAllBytes(Path.Combine(Source, "two.bin"), [2]);
        var sources = new[] { new AssetImportSource(Path.Combine(Source, "one.bin"), AssetTypes.Misc, "one"), new AssetImportSource(Path.Combine(Source, "two.bin"), AssetTypes.Misc, "two") };
        var plan = AssetImportPlan.Create(destination, sources, true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => plan.Apply(cancellation.Token));
        int writes = 0;
        Assert.Throws<IOException>(() => plan.ApplyCore((key, bytes) =>
        {
            using var stream = new MemoryStream(bytes);
            destination.Add(key.AssetType, key.AssetName, stream);
            if (++writes == 2) throw new IOException("Injected post-write failure");
        }, default));
        Assert.Empty(destination.GetAllEntries());
        using (var stream = new MemoryStream([9])) destination.Add(AssetTypes.Misc, "another-unsaved-edit", stream);
        Assert.Throws<InvalidOperationException>(() => plan.Apply());
        Assert.Equal("another-unsaved-edit", Assert.Single(destination.GetAllEntries()).AssetName);
    }

    /// <summary>Invalid names, native type overrides, missing sources, and incompatible existing bytes never mutate the archive.</summary>
    [Fact]
    public void EditorImportRejectsConflictsBeforeMutation()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        var path = Path.Combine(Source, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, Sheet("terrain", "art.png"));
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.Misc, "sheet.gts")], true));
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "../escape")], true));
        using (var stream = new MemoryStream([99])) destination.Add(AssetTypes.Image, "art.png", stream);
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "sheet.gts")], true));
        File.Delete(Path.Combine(Source, "art.png"));
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "sheet.gts")], false));
        File.WriteAllText(path, "{ malformed");
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "sheet.gts")], false));
        Assert.Single(destination.GetAllEntries());
    }

    /// <summary>Exact packed references can use unsaved destination entries, without requiring a disk archive.</summary>
    [Fact]
    public void EditorImportUsesExplicitPackedIdentityInMemory()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        using (var stream = File.OpenRead(Path.Combine(Source, "art.png"))) destination.Add(AssetTypes.Image, "images/exact.png", stream);
        var definition = Sheet("terrain", "unused");
        definition.Image = new() { AssetsFilePath = Output, AssetEntryName = "images/exact.png" };
        var path = Path.Combine(Source, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, definition);
        var plan = AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "sheet.gts")], true);
        Assert.Contains(plan.Preview, line => line.StartsWith("Reuse:"));
        Assert.Equal(1, plan.Apply());
        Assert.Equal("images/exact.png", TilesheetDefinitionSerializer.Load(destination, "sheet.gts").Image.AssetEntryName);
        Assert.False(File.Exists(Output));
    }

    /// <summary>External GAF dependencies can be preserved or copied, and URI audio remains unchanged.</summary>
    /// <param name="dependencies">Whether external packed resources are copied.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditorImportExternalArchiveAndUri(bool dependencies)
    {
        Image("art.png");
        var externalPath = Path.Combine(_root, "external.gaf");
        using (var external = AssetsFile.LoadOrCreate(externalPath, null, false, register: false))
        {
            using var stream = File.OpenRead(Path.Combine(Source, "art.png"));
            external.Add(AssetTypes.Image, "images/art.png", stream);
            external.Save();
        }
        var definition = Sheet("terrain", "unused");
        definition.Image = new() { AssetsFilePath = externalPath, AssetEntryName = "images/art.png" };
        var path = Path.Combine(Source, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, definition);
        var sounds = Path.Combine(Source, "sounds.gsnd");
        AudioDefinitionSerializer.Save(sounds, new AudioDefinition { Key = "stream", Resources = [new() { Key = "radio", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.invalid/radio" }] });
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        var destination = AssetsFile.LoadOrCreate(Output);
        var plan = AssetImportPlan.Create(destination, [new(path, AssetTypes.TilesheetDefinition, "sheet.gts"), new(sounds, AssetTypes.AudioDefinition, "sounds.gsnd")], dependencies);
        Assert.Contains(plan.Preview, line => line.StartsWith("External URI"));
        plan.Apply();
        if (dependencies) File.Delete(externalPath);
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionAsset(destination, "sheet.gts").Name);
        Assert.Equal("https://example.invalid/radio", AudioDefinitionSerializer.Load(destination, "sounds.gsnd").Resources[0].SourceUri);
    }

    /// <summary>Logical-only dependencies require a unique candidate in the selected/current inventory.</summary>
    [Fact]
    public void EditorImportRejectsAmbiguousLogicalReferences()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var destination = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        foreach (var name in new[] { "one.gts", "two.gts" })
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(Sheet("terrain", "unused.png"))));
            destination.Add(AssetTypes.TilesheetDefinition, name, stream);
        }
        var path = Path.Combine(Source, "walk.gani");
        AnimationDefinitionSerializer.Save(path, new AnimationDefinition { Key = "walk", ThrottleTime = .2, Frames = [new() { Tilesheet = "terrain" }] });
        var error = Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.AnimationDefinition, "walk.gani")], true));
        Assert.Contains("2 matching", error.Message);
        Assert.Equal(2, destination.GetAllEntries().Count());
        destination.Remove(AssetTypes.TilesheetDefinition, "two.gts");
        Assert.Throws<InvalidDataException>(() => AssetImportPlan.Create(destination, [new(path, AssetTypes.AnimationDefinition, "walk.gani")], true));
        Image("art.png");
        using (var image = File.OpenRead(Path.Combine(Source, "art.png"))) destination.Add(AssetTypes.Image, "art.png", image);
        var sheet = Sheet("terrain", "unused");
        sheet.Image = new() { AssetsFilePath = Output, AssetEntryName = "art.png" };
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet))))
            destination.Add(AssetTypes.TilesheetDefinition, "one.gts", stream);
        var plan = AssetImportPlan.Create(destination, [new(path, AssetTypes.AnimationDefinition, "walk.gani")], true);
        Assert.Equal(1, plan.Apply());
        Assert.Equal("one.gts", AnimationDefinitionSerializer.Load(destination, "walk.gani").TilesheetSources[0].AssetEntryName);
    }
}
