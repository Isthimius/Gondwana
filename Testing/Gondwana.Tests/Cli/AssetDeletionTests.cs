using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    /// <summary>Implicit nested same-GAF images and non-first members are reported, while outside-scope references remain unknown.</summary>
    [Fact]
    public void DeletionImplicitPackedReferencesRespectScope()
    {
        Image("art.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false))
        {
            archive.Add(AssetTypes.Image, Path.Combine(Source, "art.png"), "images/art.png");
            var sheet = Sheet("terrain", "unused");
            sheet.Image = new() { AssetEntryName = "images/art.png" };
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "nested/tiles.gts", bytes);
            archive.Save();
        }
        var outside = Path.Combine(Source, "outside.gani");
        AnimationDefinitionSerializer.Save(outside, new AnimationDefinition()
        {
            Key = "walk",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain" }],
            TilesheetSources = [AnimationTilesheetSourceDefinition.Packed("terrain", Output, "nested/tiles.gts")]
        });
        var targets = new AssetLocation[] { new(AssetTypes.Image, Output, "images/art.png"), new(AssetTypes.TilesheetDefinition, Output, "nested/tiles.gts") };
        var limited = AssetDeletionPlan.Create(Output, targets);
        Assert.Single(limited.InternalReferences);
        Assert.False(limited.ForceRequired);
        var full = AssetDeletionPlan.Create(_root, targets);
        Assert.Single(full.BlockingReferences);
        Assert.Equal(outside, full.BlockingReferences[0].Document.Path);
        var emptyScope = Path.Combine(_root, "empty");
        Directory.CreateDirectory(emptyScope);
        var explicitTarget = AssetDeletionPlan.Create(emptyScope, [targets[0]]);
        Assert.Single(explicitTarget.BlockingReferences);
        Assert.Contains(Output, explicitTarget.InspectedFiles);
        Assert.DoesNotContain(outside, explicitTarget.InspectedFiles);
    }

    /// <summary>Whole-archive deletion removes internal-only graphs and empty archives without force.</summary>
    /// <param name="empty">Whether the archive contains no entries.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletionWholeArchiveWithoutSurvivorsIsSafe(bool empty)
    {
        if (empty)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            using var archive = AssetsFile.LoadOrCreate(Output, null, false, register: false);
            archive.Save();
        }
        else { Content(true, false); Assert.Equal(0, Run("pack", Source, Output)); Directory.Delete(Source, true); }
        var plan = AssetDeletionPlan.Create(Output, [], [Output]);
        Assert.False(plan.ForceRequired);
        Assert.Equal(empty ? 0 : 16, plan.InternalReferences.Count);
        plan.Commit();
        Assert.False(File.Exists(Output));
    }

    /// <summary>Credentials, cancellation, and concurrency checks cannot be bypassed by force.</summary>
    [Fact]
    public void DeletionCancellationAndCredentialsProtectOriginals()
    {
        Content(true, false);
        Assert.Equal(0, Run("pack", Source, Output, "--password", "secret", "--encrypt"));
        Directory.Delete(Source, true);
        var target = new AssetLocation(AssetTypes.Image, Output, "art0.png");
        var original = File.ReadAllBytes(Output);
        Assert.ThrowsAny<Exception>(() => AssetDeletionPlan.Create(Output, [target], credentials: _ => "wrong"));
        var plan = AssetDeletionPlan.Create(Output, [target], credentials: _ => "secret");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => plan.Commit(true, cancellation.Token));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.Throws<IOException>(() => plan.CommitCore(true, (phase, _) =>
        {
            if (phase == "stage") File.AppendAllText(Output, "concurrent change");
        }, default));
        Assert.Equal(original.Concat(System.Text.Encoding.UTF8.GetBytes("concurrent change")), File.ReadAllBytes(Output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Output)!, ".gondwana-relocate-*"));
    }

    /// <summary>URI audio is not a file edge and dangling logical next-cycle keys are not physical deletion blockers.</summary>
    [Fact]
    public void DeletionIgnoresUrisAndNextCycleKeys()
    {
        Image("art.png");
        TilesheetDefinitionSerializer.Save(Path.Combine(Source, "tiles.gts"), Sheet("terrain", "art.png"));
        AnimationDefinitionSerializer.Save(Path.Combine(Source, "walk.gani"), new AnimationDefinition()
        {
            Key = "walk",
            NextCycleKey = "not-a-file",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain" }],
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("terrain", "tiles.gts")]
        });
        AudioDefinitionSerializer.Save(Path.Combine(Source, "sounds.gsnd"), new AudioDefinition() { Key = "sounds", Resources = [new() { Key = "remote", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.test/sound.ogg" }] });
        var leaf = Path.Combine(Source, "unused.wav");
        File.WriteAllBytes(leaf, [1]);
        var plan = AssetDeletionPlan.Create(Source, [new(AssetTypes.Audio, leaf)]);
        Assert.False(plan.ForceRequired);
        plan.Commit();
        Assert.False(File.Exists(leaf));
        Assert.Equal("not-a-file", AnimationDefinitionSerializer.Load(Path.Combine(Source, "walk.gani")).NextCycleKey);
    }

    /// <summary>Typed CLI entry and whole-package deletion preserve preview and credential rules.</summary>
    [Fact]
    public void DeletionCliSupportsPackedAndWholeArchiveTargets()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, "secret", true, register: false))
        {
            using var bytes = new MemoryStream([1]);
            archive.Add(AssetTypes.Misc, "nested/item", bytes);
            archive.Save();
        }
        var credentials = Path.Combine(_root, "credentials.json");
        File.WriteAllText(credentials, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { [Output] = "secret" }));
        int Delete(params string[] args)
        {
            var app = new CommandApp();
            app.Configure(config => config.AddCommand<AssetsDeleteCommand>("delete"));
            return app.Run(args);
        }
        var original = File.ReadAllBytes(Output);
        Assert.Equal(1, Delete("delete", Output, "--scope", _root, "--force", "--commit"));
        Assert.Equal(1, Delete("delete", Output, "--scope", _root, "--source-entry", "nested/item", "--commit"));
        Assert.Equal(0, Delete("delete", Output, "--scope", _root, "--source-entry", "nested/item", "--type", "Misc", "--credentials", credentials, "--force"));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.Equal(0, Delete("delete", Output, "--scope", _root, "--source-entry", "nested/item", "--type", "Misc", "--credentials", credentials, "--commit"));
        using (var archive = AssetsFile.LoadOrCreate(Output, "secret", true, register: false)) Assert.Empty(archive.GetAllEntries());
        Assert.Equal(0, Delete("delete", Output, "--scope", _root, "--archive", "--credentials", credentials));
        Assert.True(File.Exists(Output));
        Assert.Equal(0, Delete("delete", Output, "--scope", _root, "--archive", "--credentials", credentials, "--commit"));
        Assert.False(File.Exists(Output));
    }

    /// <summary>Nonstandard package extensions cannot bypass archive analysis or overlap entry selections.</summary>
    [Fact]
    public void DeletionRejectsOverlappingPhysicalTargets()
    {
        var package = Path.Combine(Source, "package.data");
        using (var archive = AssetsFile.LoadOrCreate(package, null, false, register: false))
        {
            using var bytes = new MemoryStream([1]);
            archive.Add(AssetTypes.Misc, "item", bytes);
            archive.Save();
        }
        var loose = new AssetLocation(AssetTypes.Misc, package);
        var packed = new AssetLocation(AssetTypes.Misc, package, "item");
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [loose, packed]));
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [packed, loose]));
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [packed], [package]));
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [], [package, package]));
        AssetsFile.Validate(package);
    }

    /// <summary>Every known edge blocks deletion of its exact target across all members and source representations.</summary>
    /// <param name="packed">Whether resources live in one GAF.</param>
    /// <param name="explicitSources">Whether dependency metadata is explicit.</param>
    /// <param name="legacy">Whether files contain legacy singleton JSON.</param>
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void DeletionBlocksAllEightRelationships(bool packed, bool explicitSources, bool legacy)
    {
        Content(explicitSources, legacy);
        if (packed) { Assert.Equal(0, Run("pack", Source, Output)); Directory.Delete(Source, true); }
        var scope = packed ? Output : Source;
        var scan = DefinitionReferenceDiscovery.Scan(scope);
        Assert.Equal(8, scan.References.Select(r => (r.Document.AssetType, r.Target.AssetType)).Distinct().Count());
        Assert.Equal(legacy ? 8 : 16, scan.References.Count);
        var originals = scan.InspectedFiles.ToDictionary(p => p, File.ReadAllBytes);
        foreach (var target in scan.References.Select(r => r.Target).Distinct())
        {
            var plan = AssetDeletionPlan.Create(scope, [target]);
            Assert.True(plan.ForceRequired);
            Assert.Equal(scan.FindInbound(target).Count, plan.BlockingReferences.Count);
            Assert.All(plan.BlockingReferences, r => Assert.Contains(r.Member, string.Join("\n", plan.Preview)));
            Assert.Throws<InvalidOperationException>(() => plan.Commit());
        }
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    /// <summary>Deleting only unreferenced documents leaves remaining dependencies loadable and removes no implicit dependents.</summary>
    /// <param name="packed">Whether targets are typed archive entries.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletionSafeDocumentsPreserveSurvivors(bool packed)
    {
        Content(true, false);
        if (packed) { Assert.Equal(0, Run("pack", Source, Output)); Directory.Delete(Source, true); }
        AssetLocation Target(AssetTypes type, string name) => new(type, packed ? Output : Path.Combine(Source, name), packed ? name : null);
        var scope = packed ? Output : Source;
        var plan = AssetDeletionPlan.Create(scope, [Target(AssetTypes.SpriteDefinition, "actors.gspr")]);
        Assert.False(plan.ForceRequired);
        plan.Commit();
        if (packed)
        {
            var archive = AssetsFile.LoadOrCreate(Output);
            Assert.Null(archive.Get(AssetTypes.SpriteDefinition, "actors.gspr"));
            Assert.Equal(2, TilesheetRegistry.Instance.LoadAllFromDefinitionAsset(archive, "tiles.gts").Count);
            Assert.Equal(2, AnimationDefinitionSerializer.LoadAllCycles(archive, "cycles.gani").Count);
            Assert.Equal(2, SceneDefinitionSerializer.LoadAllScenes(archive, "worlds.gscn").Count);
        }
        else
        {
            Assert.False(File.Exists(Path.Combine(Source, "actors.gspr")));
            Assert.Equal(2, TilesheetRegistry.Instance.LoadAllFromDefinitionFile(Path.Combine(Source, "tiles.gts")).Count);
            Assert.Equal(2, AnimationDefinitionSerializer.LoadAllCycles(Path.Combine(Source, "cycles.gani")).Count);
            Assert.Equal(2, SceneDefinitionSerializer.LoadAllScenes(Path.Combine(Source, "worlds.gscn")).Count);
        }
        Clear();
        foreach (var (type, name) in new[] { (AssetTypes.SceneDefinition, "worlds.gscn"), (AssetTypes.AnimationDefinition, "cycles.gani"),
                     (AssetTypes.TilesheetDefinition, "tiles.gts"), (AssetTypes.AudioDefinition, "sounds.gsnd"), (AssetTypes.Image, "art0.png"),
                     (AssetTypes.Image, "art1.png"), (AssetTypes.Audio, "sound0.wav"), (AssetTypes.Audio, "sound1.wav") })
        {
            plan = AssetDeletionPlan.Create(scope, [Target(type, name)]);
            Assert.False(plan.ForceRequired);
            plan.Commit();
            DefinitionReferenceDiscovery.Scan(scope);
        }
        if (packed)
        {
            Assert.True(File.Exists(Output));
            using var empty = AssetsFile.LoadOrCreate(Output, null, false, register: false);
            Assert.Empty(empty.GetAllEntries());
        }
        else Assert.Empty(Directory.GetFiles(Source));
    }

    /// <summary>Batch-owned references are internal; encrypted archives preserve exact untouched bytes.</summary>
    /// <param name="packed">Whether the batch removes entries from an encrypted archive or loose files.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletionBatchInternalReferencesDoNotBlock(bool packed)
    {
        Content(true, false);
        if (!packed)
        {
            var looseTargets = Directory.GetFiles(Source).Select(path => new AssetLocation(AssetImportPlan.InferType(path), path)).ToArray();
            var untouched = Path.Combine(Source, "untouched.bin");
            File.WriteAllBytes(untouched, [19, 20]);
            var loosePlan = AssetDeletionPlan.Create(Source, looseTargets);
            Assert.False(loosePlan.ForceRequired);
            Assert.Equal(16, loosePlan.InternalReferences.Count);
            loosePlan.Commit();
            Assert.Equal(untouched, Assert.Single(Directory.GetFiles(Source)));
            Assert.Equal(new byte[] { 19, 20 }, File.ReadAllBytes(untouched));
            return;
        }
        Assert.Equal(0, Run("pack", Source, Output, "--password", "secret", "--encrypt"));
        Directory.Delete(Source, true);
        using (var archive = AssetsFile.LoadOrCreate(Output, "secret", true, register: false))
        {
            using var bytes = new MemoryStream([19, 20]);
            archive.Add(AssetTypes.Misc, "untouched", bytes);
            archive.Save();
        }
        var scan = DefinitionReferenceDiscovery.Scan(Output, _ => "secret");
        var targets = scan.References.Select(r => r.Target).Concat(scan.References.Select(r => r.Document)).Distinct().ToArray();
        var plan = AssetDeletionPlan.Create(Output, targets, credentials: _ => "secret");
        Assert.False(plan.ForceRequired);
        Assert.Equal(16, plan.InternalReferences.Count);
        plan.Commit();
        Assert.ThrowsAny<Exception>(() => AssetsFile.Validate(Output, "wrong"));
        using var remaining = AssetsFile.LoadOrCreate(Output, "secret", true, register: false);
        Assert.Single(remaining.GetAllEntries());
        Assert.Equal(new byte[] { 19, 20 }, AssetImportPlan.Snapshot(remaining).Single().Value);
    }

    /// <summary>Forced deletion preserves every surviving byte and makes only the reviewed dependency unavailable.</summary>
    /// <param name="packed">Whether the deleted image is packed.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletionForceLeavesReviewedDanglingReferences(bool packed)
    {
        Content(true, false);
        if (packed) { Assert.Equal(0, Run("pack", Source, Output)); Directory.Delete(Source, true); }
        var scope = packed ? Output : Source;
        var target = new AssetLocation(AssetTypes.Image, packed ? Output : Path.Combine(Source, "art0.png"), packed ? "art0.png" : null);
        var plan = AssetDeletionPlan.Create(scope, [target]);
        var reference = Assert.Single(plan.BlockingReferences);
        Assert.Equal("terrain0", reference.Member);
        Assert.Empty(plan.InternalReferences);
        Dictionary<AssetsFileEntry, byte[]>? entries = null;
        if (packed)
        {
            using var original = AssetsFile.LoadOrCreate(Output, null, false, register: false);
            entries = AssetImportPlan.Snapshot(original);
        }
        var files = packed ? [] : Directory.GetFiles(Source).Where(p => p != target.Path).ToDictionary(p => p, File.ReadAllBytes);
        plan.Commit(force: true);
        if (packed)
        {
            var archive = AssetsFile.LoadOrCreate(Output);
            var current = AssetImportPlan.Snapshot(archive);
            Assert.Equal(entries!.Count - 1, current.Count);
            foreach (var pair in current) Assert.Equal(entries[pair.Key], pair.Value);
            Assert.ThrowsAny<Exception>(() => TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "tiles.gts", "terrain0"));
            Assert.Equal("terrain1", TilesheetRegistry.Instance.LoadFromDefinitionAsset(archive, "tiles.gts", "terrain1").Name);
        }
        else
        {
            Assert.False(File.Exists(target.Path));
            foreach (var pair in files) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.ThrowsAny<Exception>(() => TilesheetRegistry.Instance.LoadFromDefinitionFile(Path.Combine(Source, "tiles.gts"), "terrain0"));
            Assert.Equal("terrain1", TilesheetRegistry.Instance.LoadFromDefinitionFile(Path.Combine(Source, "tiles.gts"), "terrain1").Name);
        }
    }

    /// <summary>Whole archives distinguish internal edges from surviving loose and cross-archive references.</summary>
    /// <param name="extension">Supported archive filename extension.</param>
    [Theory]
    [InlineData(".gaf")]
    [InlineData(".zip")]
    public void DeletionWholeArchiveFindsExternalReferences(string extension)
    {
        Content(true, false);
        var package = Path.ChangeExtension(Output, extension);
        Assert.Equal(0, Run("pack", Source, package, "--password", "one", "--encrypt"));
        Directory.Delete(Source, true);
        var animation = new AnimationDefinition
        {
            Key = "external",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain1" }],
            TilesheetSources = [AnimationTilesheetSourceDefinition.Packed("terrain1", package, "tiles.gts")]
        };
        var loose = Path.Combine(_root, "external.gani");
        AnimationDefinitionSerializer.Save(loose, animation);
        var other = Path.Combine(_root, "other.zip");
        using (var archive = AssetsFile.LoadOrCreate(other, "two", true, register: false))
        {
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(animation)));
            archive.Add(AssetTypes.AnimationDefinition, "nested/external.gani", bytes);
            archive.Save();
        }
        var original = File.ReadAllBytes(package);
        var looseBytes = File.ReadAllBytes(loose);
        var otherBytes = File.ReadAllBytes(other);
        var plan = AssetDeletionPlan.Create(_root, [], [package], path => path == package ? "one" : "two");
        Assert.Equal(16, plan.InternalReferences.Count);
        Assert.Equal(2, plan.BlockingReferences.Count);
        Assert.Throws<InvalidOperationException>(() => plan.Commit());
        Assert.Equal(original, File.ReadAllBytes(package));
        plan.Commit(true);
        Assert.False(File.Exists(package));
        Assert.Equal(looseBytes, File.ReadAllBytes(loose));
        Assert.Equal(otherBytes, File.ReadAllBytes(other));
    }

    /// <summary>Force never bypasses malformed scope, missing dependencies, invalid types, or stale snapshots.</summary>
    [Fact]
    public void DeletionRejectsIncompleteInvalidAndStalePlans()
    {
        Content(true, false);
        var target = new AssetLocation(AssetTypes.Image, Path.Combine(Source, "art0.png"));
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [new(AssetTypes.Misc, target.Path)]));
        Assert.Throws<InvalidDataException>(() => AssetDeletionPlan.Create(Source, [target, target]));
        Assert.ThrowsAny<Exception>(() => AssetDeletionPlan.Create(Source, [new(AssetTypes.Image, Path.Combine(Source, "missing.png"))]));
        var plan = AssetDeletionPlan.Create(Source, [target]);
        File.AppendAllText(Path.Combine(Source, "cycles.gani"), " ");
        Assert.Throws<IOException>(() => plan.Commit(true));
        Assert.True(File.Exists(target.Path));
        File.WriteAllText(Path.Combine(Source, "broken.gts"), "not json");
        Assert.ThrowsAny<Exception>(() => AssetDeletionPlan.Create(Source, [target]));
        File.Delete(Path.Combine(Source, "broken.gts"));
        File.Delete(Path.Combine(Source, "art1.png"));
        Assert.ThrowsAny<Exception>(() => AssetDeletionPlan.Create(Source, [target]));
    }

    /// <summary>Staging, replacement, retirement, cancellation, and completion publication failures restore the complete batch.</summary>
    /// <param name="phase">Fault checkpoint.</param>
    /// <param name="cancel">Whether cancellation triggers rollback.</param>
    [Theory]
    [InlineData("stage", false)]
    [InlineData("install", false)]
    [InlineData("retire", false)]
    [InlineData("retire", true)]
    [InlineData("install", true)]
    [InlineData("complete-manifest", false)]
    public void DeletionRollsBackCompleteBatch(string phase, bool cancel)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using (var archive = AssetsFile.LoadOrCreate(Output, "secret", true, register: false))
        {
            using var stream = new MemoryStream([1, 2]);
            archive.Add(AssetTypes.Misc, "remove", stream);
            archive.Save();
        }
        var first = Path.Combine(Source, "first.bin");
        var second = Path.Combine(Source, "second.bin");
        File.WriteAllBytes(first, [3]);
        File.WriteAllBytes(second, [4]);
        var original = File.ReadAllBytes(Output);
        var plan = AssetDeletionPlan.Create(_root, [new(AssetTypes.Misc, first), new(AssetTypes.Misc, second), new(AssetTypes.Misc, Output, "remove")], credentials: _ => "secret");
        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsAny<Exception>(() => plan.CommitCore(false, (at, _) =>
        {
            if (at != phase) return;
            if (at == "complete-manifest")
            {
                var manifest = Assert.Single(Directory.GetFiles(_root, ".gondwana-relocate-*.json"));
                Assert.Contains("Prepared", File.ReadAllText(manifest));
                Assert.Contains("Completed", File.ReadAllText(manifest + ".completed"));
            }
            if (cancel) cancellation.Cancel();
            else throw new IOException("Injected deletion failure");
        }, cancellation.Token));
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(first));
        Assert.Equal(new byte[] { 4 }, File.ReadAllBytes(second));
        Assert.Equal(original, File.ReadAllBytes(Output));
        Assert.Empty(Directory.GetFiles(_root, ".gondwana-relocate-*", SearchOption.AllDirectories));
    }

    /// <summary>CLI previews and force-only reviews never mutate; safe blocks and execution failures have distinct exits.</summary>
    [Fact]
    public void DeletionCliRequiresCommitAndExplicitForce()
    {
        Image("art.png");
        var image = Path.Combine(Source, "art.png");
        var document = Path.Combine(Source, "tiles.gts");
        TilesheetDefinitionSerializer.Save(document, Sheet("terrain", "art.png"));
        int Delete(params string[] args)
        {
            var app = new CommandApp();
            app.Configure(config => config.AddCommand<AssetsDeleteCommand>("delete"));
            return app.Run(args);
        }
        Assert.Equal(2, Delete("delete", image, "--scope", Source));
        Assert.Equal(2, Delete("delete", image, "--scope", Source, "--commit"));
        Assert.Equal(0, Delete("delete", image, "--scope", Source, "--force"));
        Assert.True(File.Exists(image));
        Assert.Equal(0, Delete("delete", image, "--scope", Source, "--commit", "--force"));
        Assert.False(File.Exists(image));
        Assert.True(File.Exists(document));
        Assert.Equal(1, Delete("delete", image, "--scope", Source, "--commit", "--force"));
    }
}
