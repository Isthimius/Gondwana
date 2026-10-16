using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Audio.GSND;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    /// <summary>Relative archive source paths are resolved before constructing relocation endpoints.</summary>
    [Fact]
    public void RelocationCopyAcceptsRelativeArchiveSourcePath()
    {
        Image("art.png");
        var relative = "relative-copy-" + Guid.NewGuid().ToString("N") + ".gaf";
        try
        {
            using var archive = AssetsFile.LoadOrCreate(relative, null, false, register: false);
            Assert.False(Path.IsPathFullyQualified(archive.SourcePath));
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(Sheet("terrain", Path.Combine(Source, "art.png")))));
            archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", bytes);
            archive.Save();
            var original = File.ReadAllBytes(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            AssetArchiveCopy.Save(archive, Output, null, false);
            Assert.Equal(original, File.ReadAllBytes(relative));
            var copy = AssetsFile.LoadOrCreate(Output);
            Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionAsset(copy, "tiles.gts").Name);
        }
        finally { File.Delete(relative); }
    }

    /// <summary>Completion staging leaves the prepared recovery record intact until atomic publication.</summary>
    [Fact]
    public void RelocationCompletionManifestFailureRetainsPreparedRecordAndRollsBack()
    {
        var source = Path.Combine(Source, "original.bin");
        var target = Path.Combine(Source, "renamed.bin");
        File.WriteAllBytes(source, [1, 2]);
        var plan = AssetRelocationPlan.Create(Source, [new(new(AssetTypes.Misc, source), new(AssetTypes.Misc, target))]);
        Assert.Throws<IOException>(() => plan.CommitCore((phase, _) =>
        {
            if (phase != "complete-manifest") return;
            var manifest = Assert.Single(Directory.GetFiles(Source, ".gondwana-relocate-*.json"));
            using var prepared = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            using var completed = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest + ".completed"));
            Assert.Equal("Prepared", prepared.RootElement.GetProperty("State").GetString());
            Assert.Equal("Completed", completed.RootElement.GetProperty("State").GetString());
            throw new IOException("Completion publication interrupted");
        }, default));
        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(source));
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(Source, ".gondwana-relocate-*"));
    }

    /// <summary>GAF and ZIP archives cannot bypass reference updates as loose resources.</summary>
    /// <param name="extension">Supported archive extension.</param>
    [Theory]
    [InlineData(".gaf")]
    [InlineData(".ZIP")]
    public void RelocationArchivesRequireWholeArchivePath(string extension)
    {
        var source = Path.Combine(Source, "package" + extension);
        using (var archive = AssetsFile.LoadOrCreate(source, null, false, register: false)) archive.Save();
        Assert.True(AssetArchiveFormat.IsArchive(source));
        var target = Path.Combine(Source, "moved" + extension);
        Assert.Throws<InvalidDataException>(() => AssetRelocationPlan.Create(Source,
            [new(new(AssetTypes.Misc, source), new(AssetTypes.Misc, target))]));
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(target));
        AssetRelocationPlan.MoveArchive(source, target, Source).Commit();
        Assert.False(File.Exists(source));
        AssetsFile.Validate(target);
    }

    /// <summary>Case conflicts and file/directory conflicts are rejected even before destinations exist.</summary>
    /// <param name="first">First planned relative destination.</param>
    /// <param name="second">Conflicting planned relative destination.</param>
    [Theory]
    [InlineData("A.bin", "a.bin")]
    [InlineData("Dir/one.bin", "dir/two.bin")]
    [InlineData("folder", "folder/child.bin")]
    public void RelocationRejectsPlannedPathCollisions(string first, string second)
    {
        var one = Path.Combine(Source, "one.bin");
        var two = Path.Combine(Source, "two.bin");
        File.WriteAllBytes(one, [1]);
        File.WriteAllBytes(two, [2]);
        Assert.Throws<InvalidDataException>(() => AssetRelocationPlan.Create(Source,
            [new(new(AssetTypes.Misc, one), new(AssetTypes.Misc, Path.Combine(Source, first))),
             new(new(AssetTypes.Misc, two), new(AssetTypes.Misc, Path.Combine(Source, second)))]));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(one));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(two));
        Assert.Equal(2, Directory.GetFiles(Source).Length);
    }

    /// <summary>Directory scopes accept trailing separators and root paths without bypassing ancestry checks.</summary>
    [Fact]
    public void RelocationDirectoryScopesNormalizeSeparatorsAndAcceptRoots()
    {
        var source = Path.Combine(Source, "one.bin");
        var target = Path.Combine(Source, "two.bin");
        File.WriteAllBytes(source, [1]);
        var plan = AssetRelocationPlan.Create(Source + Path.DirectorySeparatorChar,
            [new(new(AssetTypes.Misc, source), new(AssetTypes.Misc, target))]);
        Assert.Equal(Source, plan.Scope);
        plan.Commit();
        Assert.True(File.Exists(target));
        var root = Path.GetPathRoot(Source)!;
        DefinitionReferenceDiscovery.CheckPath(root);
        Assert.Equal(Path.Combine(root, "safe.bin"), SafeFilePath.Resolve(root, "safe.bin"));
        Assert.Throws<InvalidDataException>(() => SafeFilePath.Resolve(root, "../escape.bin"));
    }

    /// <summary>Explicit endpoints join discovery before logical references in a limited archive scope are resolved.</summary>
    [Fact]
    public void RelocationIncludesExplicitSourceBeforeResolvingLogicalSelectors()
    {
        Image("art.png");
        var source = Path.Combine(Source, "tiles.gts");
        TilesheetDefinitionSerializer.Save(source, Sheet("terrain", "art.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            var animation = new AnimationDefinition { Key = "walk", ThrottleTime = .2, Frames = [new() { Tilesheet = "terrain" }] };
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(animation)));
            archive.Add(AssetTypes.AnimationDefinition, "walk.gani", bytes);
            archive.Save();
        }
        var target = Path.Combine(Source, "renamed.gts");
        var plan = AssetRelocationPlan.Create(Output, [new(new(AssetTypes.TilesheetDefinition, source), new(AssetTypes.TilesheetDefinition, target))]);
        plan.Commit();
        Assert.False(File.Exists(source));
        TilesheetRegistry.Instance.LoadFromDefinitionFile(target);
        var moved = AssetsFile.LoadOrCreate(Output);
        Assert.Equal("walk", AnimationDefinitionSerializer.Load(moved, "walk.gani").Key);
        Assert.NotNull(AnimationDefinitionSerializer.LoadCycle(moved, "walk.gani"));
    }

    /// <summary>A concurrent destination created before its install is rejected and never deleted by rollback.</summary>
    [Fact]
    public void RelocationRollbackDoesNotDeleteUninstalledConcurrentDestination()
    {
        var one = Path.Combine(Source, "one.bin");
        var two = Path.Combine(Source, "two.bin");
        File.WriteAllBytes(one, [1]);
        File.WriteAllBytes(two, [2]);
        var first = Path.Combine(Source, "a.bin");
        var second = Path.Combine(Source, "b.bin");
        var plan = AssetRelocationPlan.Create(Source, [new(new(AssetTypes.Misc, one), new(AssetTypes.Misc, first)), new(new(AssetTypes.Misc, two), new(AssetTypes.Misc, second))]);
        Assert.Throws<IOException>(() => plan.CommitCore((phase, index) =>
        {
            if (phase == "install" && index == 1) File.WriteAllBytes(second, [99]);
        }, default));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(second));
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(one));
        Assert.True(File.Exists(two));
    }

    /// <summary>CLI previews are side-effect-free and explicit commit uses the same move planner.</summary>
    [Fact]
    public void RelocationCliPreviewAndCommit()
    {
        Image("art.png");
        var source = Path.Combine(Source, "sheet.gts");
        var target = Path.Combine(Source, "renamed.gts");
        TilesheetDefinitionSerializer.Save(source, Sheet("terrain", "art.png"));
        int Move(params string[] args)
        {
            var app = new CommandApp();
            app.Configure(config => config.AddCommand<AssetsMoveCommand>("move"));
            return app.Run(args);
        }
        Assert.Equal(0, Move("move", source, target, "--scope", Source));
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(target));
        Assert.Equal(0, Move("move", source, target, "--scope", Source, "--commit"));
        Assert.False(File.Exists(source));
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionFile(target).Name);
    }

    /// <summary>Source packed entries survive the install phase and archive retirement failures restore every archive.</summary>
    [Fact]
    public void RelocationPackedRetirementRollsBackAndRetainsSourcesUntilReady()
    {
        Content(true, false);
        Assert.Equal(0, Run("pack", Source, Output));
        Directory.Delete(Source, true);
        var original = File.ReadAllBytes(Output);
        var target = Path.Combine(_root, "second.gaf");
        var plan = AssetRelocationPlan.Create(Output, [new(new(AssetTypes.Image, Output, "art0.png"), new(AssetTypes.Image, target, "art.png"))]);
        Assert.Throws<IOException>(() => plan.CommitCore((phase, _) =>
        {
            if (phase == "install")
            {
                using var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false);
                using var image = archive.Get(AssetTypes.Image, "art0.png");
                Assert.NotNull(image);
            }
            if (phase == "retire") throw new IOException("Injected archive retirement failure");
        }, default));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.False(File.Exists(target));
    }

    /// <summary>Discovery covers all known edges and every member without interpreting NextCycleKey as a physical edge.</summary>
    [Fact]
    public void RelocationDiscoveryCoversEveryMemberAndEdge()
    {
        Content(false, false);
        var scan = DefinitionReferenceDiscovery.Scan(Source);
        Assert.Equal(16, scan.References.Count);
        Assert.Equal(8, scan.References.Select(r => (r.Document.AssetType, r.Target.AssetType)).Distinct().Count());
        Assert.Contains(scan.References, r => r.Member == "actors1" && r.Selector == "walk1");
        Assert.All(scan.References, r => Assert.False(string.IsNullOrWhiteSpace(r.ReferencePath)));
        var target = Path.Combine(_root, "renamed.gts");
        var plan = AssetRelocationPlan.Create(Source, [new(new(AssetTypes.TilesheetDefinition, Path.Combine(Source, "tiles.gts")), new(AssetTypes.TilesheetDefinition, target))]);
        plan.Commit();
        Assert.Equal("walk1", AnimationDefinitionSerializer.Load(Path.Combine(Source, "cycles.gani"), "walk0").NextCycleKey);
        Assert.Equal(target, Path.GetFullPath(AnimationDefinitionSerializer.Load(Path.Combine(Source, "cycles.gani"), "walk1").TilesheetSources[0].GtsPath!, Source));
    }

    /// <summary>Implicit same-GAF images follow packed renames while out-of-scope loose references are not claimed.</summary>
    [Fact]
    public void RelocationPackedRenameUsesExactIdentityAndScope()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            archive.Add(AssetTypes.Image, Path.Combine(Source, "art.png"), "images/art.png");
            var sheet = Sheet("terrain", "unused");
            sheet.Image = new() { AssetEntryName = "images/art.png" };
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", bytes);
            archive.Save();
        }
        var outside = Path.Combine(Source, "outside.gts");
        TilesheetDefinitionSerializer.Save(outside, Sheet("outside", "art.png"));
        var original = File.ReadAllBytes(outside);
        var from = new AssetLocation(AssetTypes.Image, Output, "images/art.png");
        var to = new AssetLocation(AssetTypes.Image, Output, "other/renamed.png");
        var plan = AssetRelocationPlan.Create(Output, [new(from, to)]);
        Assert.Single(plan.InboundReferences);
        plan.Commit();
        Assert.Equal(original, File.ReadAllBytes(outside));
        Directory.Delete(Source, true);
        var moved = AssetsFile.LoadOrCreate(Output);
        Assert.Null(moved.Get(AssetTypes.Image, "images/art.png"));
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionAsset(moved, "tiles.gts").Name);
    }

    /// <summary>Copy preserves originals and rebases both internal and external references using the final archive path.</summary>
    [Fact]
    public void RelocationArchiveCopyPreservesOutboundReferences()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false);
        var sheet = Sheet("terrain", Path.Combine(Source, "art.png"));
        using (var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)))) archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", bytes);
        archive.Save();
        var original = File.ReadAllBytes(Output);
        var target = Path.Combine(Source, "copy.gaf");
        AssetArchiveCopy.Save(archive, target, "new-password", true);
        Assert.Equal(original, File.ReadAllBytes(Output));
        var copy = AssetsFile.LoadOrCreate(target, "new-password");
        var definition = TilesheetDefinitionSerializer.Load(copy, "tiles.gts");
        Assert.Equal("art.png", definition.Image.FilePath);
        Assert.Equal("terrain", TilesheetRegistry.Instance.LoadFromDefinitionAsset(copy, "tiles.gts").Name);
    }

    /// <summary>Unreadable encrypted scopes, malformed files, missing dependencies, and new in-scope documents block moves.</summary>
    [Fact]
    public void RelocationRejectsIncompleteDiscovery()
    {
        Content(true, false);
        Assert.Equal(0, Run("pack", Source, Output, "--password", "secret", "--encrypt"));
        Assert.ThrowsAny<Exception>(() => DefinitionReferenceDiscovery.Scan(_root));
        var source = Path.Combine(Source, "art0.png");
        var target = Path.Combine(Source, "new.png");
        var plan = AssetRelocationPlan.Create(_root, [new(new(AssetTypes.Image, source), new(AssetTypes.Image, target))], _ => "secret");
        TilesheetDefinitionSerializer.Save(Path.Combine(Source, "late.gts"), Sheet("late", "art0.png"));
        Assert.Throws<IOException>(() => plan.Commit());
        Assert.True(File.Exists(source));
        File.Delete(source);
        Assert.ThrowsAny<Exception>(() => DefinitionReferenceDiscovery.Scan(Source));
    }

    /// <summary>Extensionless audio entries retain their hints and URI resources are never rewritten.</summary>
    [Fact]
    public void RelocationPreservesAudioHintsAndUris()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            using var sound = new MemoryStream([1, 2, 3]);
            archive.Add(AssetTypes.Audio, "sound", sound);
            var audio = new AudioDefinition
            {
                Key = "audio",
                Resources = [
                new() { Key = "effect", SourceKind = AudioResourceSourceKind.PackedAsset, AssetsFilePath = "game.gaf", AssetEntryName = "sound", SourceExtension = ".wav" },
                new() { Key = "radio", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.invalid/radio" }]
            };
            using var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(AudioDefinitionSerializer.ToJson(audio)));
            archive.Add(AssetTypes.AudioDefinition, "sounds.gsnd", json);
            archive.Save();
        }
        var target = Path.Combine(_root, "renamed.gaf");
        AssetRelocationPlan.MoveArchive(Output, target, Output).Commit();
        using var moved = AssetsFile.LoadOrCreate(target, null, false, register: false);
        var resources = AudioDefinitionSerializer.Load(moved, "sounds.gsnd").Resources;
        Assert.Equal(".wav", resources[0].SourceExtension);
        Assert.Equal("https://example.invalid/radio", resources[1].SourceUri);
        Assert.False(File.Exists(Output));
    }

    /// <summary>All eight dependency edges survive a complete move across storage boundaries.</summary>
    /// <param name="packedSource">Whether sources start in a GAF.</param>
    /// <param name="packedDestination">Whether destinations are GAF entries.</param>
    /// <param name="legacy">Whether sources use legacy singleton JSON.</param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void RelocationMovesCompleteHierarchy(bool packedSource, bool packedDestination, bool legacy)
    {
        Content(true, legacy);
        int count = legacy ? 1 : 2;
        var originalFiles = Directory.GetFiles(Source);
        if (packedSource)
        {
            Assert.Equal(0, Run("pack", Source, Output));
            Directory.Delete(Source, true);
        }
        var finalDirectory = Path.Combine(_root, "moved");
        var finalArchive = Path.Combine(finalDirectory, "final.gaf");
        var mappings = originalFiles.Select(path => new ReferenceRelocation(
            new(AssetImportPlan.InferType(path), packedSource ? Output : path, packedSource ? Path.GetFileName(path) : null),
            new(AssetImportPlan.InferType(path), packedDestination ? finalArchive : Path.Combine(finalDirectory, Path.GetFileName(path)), packedDestination ? Path.GetFileName(path) : null))).ToArray();
        var plan = AssetRelocationPlan.Create(_root, mappings);
        Assert.NotEmpty(plan.InboundReferences);
        plan.Commit();
        Assert.All(originalFiles, path => Assert.False(File.Exists(path)));
        if (packedSource)
        {
            using var old = AssetsFile.LoadOrCreate(Output, null, false, register: false);
            Assert.Empty(old.GetAllEntries());
        }
        var backend = new Backend();
        AudioResourceManager.Instance.ConfigureBackend(backend);
        if (packedDestination)
        {
            var archive = AssetsFile.LoadOrCreate(finalArchive);
            Assert.Equal(finalArchive, TilesheetDefinitionSerializer.Load(archive, "tiles.gts", "terrain0").Source.AssetsFilePath);
            Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionAsset(archive, "tiles.gts").Count);
            Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(archive, "cycles.gani").Count);
            Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(archive, "worlds.gscn").Count);
            Assert.Equal(count, SpriteDefinitionSerializer.LoadAllSprites(archive, "actors.gspr").Count);
            Assert.Equal(count, AudioDefinitionSerializer.LoadAllIntoManager(archive, "sounds.gsnd").Count);
        }
        else
        {
            Assert.Equal(Path.Combine(finalDirectory, "tiles.gts"), TilesheetDefinitionSerializer.Load(Path.Combine(finalDirectory, "tiles.gts"), "terrain0").Source.GtsFilePath);
            Assert.Equal(count, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(Path.Combine(finalDirectory, "tiles.gts")).Count);
            Assert.Equal(count, AnimationDefinitionSerializer.LoadAllCycles(Path.Combine(finalDirectory, "cycles.gani")).Count);
            Assert.Equal(count, SceneDefinitionSerializer.LoadAllScenes(Path.Combine(finalDirectory, "worlds.gscn")).Count);
            Assert.Equal(count, SpriteDefinitionSerializer.LoadAllSprites(Path.Combine(finalDirectory, "actors.gspr")).Count);
            Assert.Equal(count, AudioDefinitionSerializer.LoadAllIntoManager(Path.Combine(finalDirectory, "sounds.gsnd")).Count);
        }
        Assert.Equal(count, backend.Loaded.Count);
    }

    /// <summary>Whole encrypted GAF moves preserve internal entries, external inbound references, and binary bytes.</summary>
    /// <param name="extension">Package extension recognized during directory discovery.</param>
    [Theory]
    [InlineData(".gaf")]
    [InlineData(".zip")]
    public void RelocationMovesWholeEncryptedArchive(string extension)
    {
        Content(true, false);
        var originalArchive = Path.ChangeExtension(Output, extension);
        Assert.Equal(0, Run("pack", Source, originalArchive, "--password", "secret", "--encrypt"));
        var inbound = Path.Combine(Source, "inbound.gani");
        AnimationDefinitionSerializer.Save(inbound, new AnimationDefinition
        {
            Key = "external",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain0" }],
            TilesheetSources = [AnimationTilesheetSourceDefinition.Packed("terrain0", originalArchive, "tiles.gts")]
        });
        var moved = Path.Combine(_root, "elsewhere", "renamed" + extension);
        var plan = AssetRelocationPlan.MoveArchive(originalArchive, moved, _root, _ => "secret");
        plan.Commit();
        Assert.False(File.Exists(originalArchive));
        var source = AnimationDefinitionSerializer.Load(inbound).TilesheetSources[0];
        Assert.Equal(moved, Path.GetFullPath(source.AssetsFilePath!, Path.GetDirectoryName(inbound)!));
        Directory.Delete(Source, true);
        var archive = AssetsFile.LoadOrCreate(moved, "secret");
        Assert.Equal(2, TilesheetRegistry.Instance.LoadAllFromDefinitionAsset(archive, "tiles.gts").Count);
        Assert.Equal(2, AnimationDefinitionSerializer.LoadAllCycles(archive, "cycles.gani").Count);
    }

    /// <summary>Failures after staging, later installs, or retirement restore every original file and inbound reference.</summary>
    /// <param name="phase">Fault checkpoint.</param>
    /// <param name="cancel">Whether the failure is cancellation.</param>
    [Theory]
    [InlineData("stage", false)]
    [InlineData("install", false)]
    [InlineData("retire", false)]
    [InlineData("install", true)]
    [InlineData("retire", true)]
    public void RelocationRollsBackCompleteTransaction(string phase, bool cancel)
    {
        Content(true, false);
        var originals = Directory.GetFiles(Source).ToDictionary(path => path, File.ReadAllBytes);
        var target = Path.Combine(_root, "moved", "tiles.gts");
        var plan = AssetRelocationPlan.Create(_root, [new(new(AssetTypes.TilesheetDefinition, Path.Combine(Source, "tiles.gts")), new(AssetTypes.TilesheetDefinition, target))]);
        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsAny<Exception>(() => plan.CommitCore((checkpoint, index) =>
        {
            if (checkpoint != phase || phase == "install" && index != 2) return;
            if (cancel) cancellation.Cancel();
            else throw new IOException("Injected transaction failure");
        }, cancellation.Token));
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(_root, ".gondwana-relocate-*", SearchOption.AllDirectories));
    }

    /// <summary>Stale snapshots and destination collisions fail without removing the source.</summary>
    [Fact]
    public void RelocationRejectsStalePlansAndConflicts()
    {
        Content(true, false);
        var source = Path.Combine(Source, "tiles.gts");
        var target = Path.Combine(_root, "renamed.gts");
        ReferenceRelocation[] mappings = [new(new(AssetTypes.TilesheetDefinition, source), new(AssetTypes.TilesheetDefinition, target))];
        var plan = AssetRelocationPlan.Create(_root, mappings);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => plan.Commit(cancellation.Token));
            Assert.True(File.Exists(source));
            Assert.False(File.Exists(target));
        }
        File.AppendAllText(Path.Combine(Source, "cycles.gani"), " ");
        Assert.Throws<IOException>(() => plan.Commit());
        Assert.True(File.Exists(source));
        File.WriteAllText(target, "unrelated");
        Assert.ThrowsAny<Exception>(() => AssetRelocationPlan.Create(_root, mappings));
        Assert.Equal("unrelated", File.ReadAllText(target));
    }
}
