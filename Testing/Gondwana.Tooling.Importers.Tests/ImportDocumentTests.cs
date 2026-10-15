using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Importers.Tests;

public sealed partial class ImportPipelineTests
{
    private static void Native(ImportPlan plan, string file, string kind, string key)
    {
        switch (kind)
        {
            case "GTS": plan.Add(file, new TilesheetDefinition { Name = key, Image = new() { FilePath = "../atlas.png" } }); break;
            case "GANI": plan.Add(file, new AnimationDefinition { Key = key, Frames = [new() { Tilesheet = "terrain" }] }); break;
            case "GSCN": plan.Add(file, new SceneDefinition { ID = key }); break;
            case "GSPR": plan.Add(file, new SpriteDefinition { Key = key }); break;
            case "GSND": plan.Add(file, new AudioDefinition { Key = key }); break;
            default: throw new ArgumentException(kind);
        }
    }

    private static string[] ReadKeys(string kind, string path) => kind switch
    {
        "GTS" => TilesheetDefinitionSerializer.LoadAll(path).Select(member => member.Name).ToArray(),
        "GANI" => AnimationDefinitionSerializer.LoadAll(path).Select(member => member.Key).ToArray(),
        "GSCN" => SceneDefinitionSerializer.LoadAll(path).Select(member => member.ID).ToArray(),
        "GSPR" => SpriteDefinitionSerializer.LoadAll(path).Select(member => member.Key).ToArray(),
        "GSND" => AudioDefinitionSerializer.LoadAll(path).Select(member => member.Key).ToArray(),
        _ => throw new ArgumentException(kind)
    };

    /// <summary>Typed members become one canonical file and analysis reports all identities without writing.</summary>
    /// <param name="kind">The native format.</param>
    [Theory]
    [InlineData("GTS")]
    [InlineData("GANI")]
    [InlineData("GSCN")]
    [InlineData("GSPR")]
    [InlineData("GSND")]
    public void TypedGroupsRoundTripAndReportAllMembers(string kind)
    {
        string file = "group." + kind.ToLowerInvariant();
        var provider = new Provider(plan => { Native(plan, file, kind, "one"); Native(plan, file, kind, "two"); });
        var analysis = provider.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        var artifact = Assert.Single(analysis.Artifacts);
        Assert.Equal(new[] { "one", "two" }, artifact.LogicalKeys);
        Assert.Equal("one", artifact.LogicalKey);
        Assert.False(Directory.Exists(request.OutputDirectory));
        var result = provider.Import(request);
        var path = Assert.Single(result.WrittenFiles);
        Assert.Equal(new[] { "one", "two" }, ReadKeys(kind, path));
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
        Assert.Equal(2, root["Definitions"]!.Count());
        if (kind is "GSPR" or "GSND") Assert.All(root["Definitions"]!, member => Assert.Null(member["Source"]));
        else Assert.All(root["Definitions"]!, member => Assert.Equal(3, (int)member["Source"]!["Kind"]!));
        // Overwrite replaces the full document; it does not append to its prior members.
        var replacement = new Provider(plan => Native(plan, file, kind, "replacement"));
        Assert.Empty(replacement.Import(request).WrittenFiles);
        Assert.Single(replacement.Import(request with { Overwrite = true }).WrittenFiles);
        Assert.Equal(new[] { "replacement" }, ReadKeys(kind, path));
    }

    /// <summary>Every member participates in duplicate and existing-document conflict checks.</summary>
    /// <param name="kind">The native format.</param>
    [Theory]
    [InlineData("GTS")]
    [InlineData("GANI")]
    [InlineData("GSCN")]
    [InlineData("GSPR")]
    [InlineData("GSND")]
    public void AllMembersParticipateInIdentityChecks(string kind)
    {
        string file = "group." + kind.ToLowerInvariant();
        foreach (bool sameFile in new[] { true, false })
        {
            var provider = new Provider(plan =>
            {
                Native(plan, file, kind, "one");
                Native(plan, file, kind, "two");
                Native(plan, sameFile ? file : "other." + kind.ToLowerInvariant(), kind, "two");
            });
            var result = provider.Import(request);
            Assert.False(result.Analysis.CanImport);
            Assert.Contains(result.Analysis.Diagnostics, diagnostic => diagnostic.Code == "key.duplicate" && diagnostic.Message.Contains("two"));
            Assert.False(Directory.Exists(request.OutputDirectory));
        }
        new Provider(plan => { Native(plan, file, kind, "one"); Native(plan, file, kind, "two"); }).Import(request);
        var bytes = File.ReadAllBytes(Path.Combine(request.OutputDirectory, file));
        var conflict = new Provider(plan => Native(plan, "other." + kind.ToLowerInvariant(), kind, "two"))
            .Import(request with { Overwrite = true });
        Assert.Contains(conflict.Analysis.Diagnostics, diagnostic => diagnostic.Code == "key.exists" && diagnostic.Message.Contains("two"));
        Assert.Empty(conflict.WrittenFiles);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(request.OutputDirectory, file)));
    }

    /// <summary>Grouped documents use the existing cancellation and rollback transaction.</summary>
    /// <param name="kind">The native format.</param>
    [Theory]
    [InlineData("GTS")]
    [InlineData("GANI")]
    [InlineData("GSCN")]
    [InlineData("GSPR")]
    [InlineData("GSND")]
    public void GroupedDocumentsCancelAndRollback(string kind)
    {
        string file = "group." + kind.ToLowerInvariant();
        using var cancellation = new CancellationTokenSource();
        var canceled = new Provider(plan => { Native(plan, file, kind, "one"); Native(plan, file, kind, "two"); cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => canceled.Import(request, cancellation.Token));
        Assert.False(Directory.Exists(request.OutputDirectory));
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(request.OutputDirectory);
        var first = Path.Combine(request.OutputDirectory, file);
        var second = Path.Combine(request.OutputDirectory, "image.png");
        File.WriteAllText(first, "original definition");
        File.WriteAllText(second, "original image");
        var provider = new Provider(plan =>
        {
            Native(plan, file, kind, "one"); Native(plan, file, kind, "two");
            plan.Add("image.png", "PNG", null, [1, 2, 3]);
        });
        using (var locked = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => provider.Import(request with { Overwrite = true }));
        Assert.Equal("original definition", File.ReadAllText(first));
        Assert.Equal("original image", File.ReadAllText(second));
        Assert.Empty(Directory.GetDirectories(request.OutputDirectory));
    }

    /// <summary>Validation sees completed typed members and reports the offending logical member.</summary>
    [Fact]
    public void TypedMembersAreValidatedAfterPlanConstruction()
    {
        var sprite = new SpriteDefinition { Key = "actors" };
        Assert.True(new Provider(plan => plan.Add("actors.gspr", sprite)).Analyze(request).CanImport);
        Assert.Equal(SpriteDefinitionSourceKind.Generated, sprite.Source.Kind);
        var audio = new AudioDefinition { Key = "music" };
        Assert.True(new Provider(plan => plan.Add("audio.gsnd", audio)).Analyze(request).CanImport);
        Assert.Equal(AudioDefinitionSourceKind.Generated, audio.Source.Kind);
        var definition = new AnimationDefinition { Key = "walk" };
        var provider = new Provider(plan =>
        {
            plan.Add("animations.gani", definition);
            definition.Frames = [new() { Tilesheet = "terrain" }];
            Native(plan, "animations.gani", "GANI", "run");
        });
        Assert.True(provider.Analyze(request).CanImport);
        var invalid = new Provider(plan =>
        {
            Native(plan, "animations.gani", "GANI", "walk");
            plan.Add("animations.gani", new AnimationDefinition { Key = "broken" });
        }).Import(request);
        Assert.Contains(invalid.Analysis.Diagnostics, diagnostic => diagnostic.Code == "native.validation" && diagnostic.Message.Contains("broken"));
        Assert.Empty(invalid.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Grouped outputs retain member selectors, relative paths, and generated provenance.</summary>
    [Fact]
    public void GroupedReferencesRetainPhysicalPathsAndLogicalSelectors()
    {
        var provider = new Provider(plan =>
        {
            Native(plan, "tiles.gts", "GTS", "terrain");
            Native(plan, "tiles.gts", "GTS", "actors");
            foreach (string key in new[] { "walk", "run" })
                plan.Add("animations.gani", new AnimationDefinition
                {
                    Key = key,
                    NextCycleKey = key == "walk" ? "run" : "walk",
                    Frames = [new() { Tilesheet = "actors" }],
                    TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("actors", "tiles.gts")]
                });
            plan.Add("scenes.gscn", new SceneDefinition
            {
                ID = "level",
                AnimationSources = [SceneAnimationSourceDefinition.Loose("run", "animations.gani")]
            });
            plan.Add("sprites.gspr", new SpriteDefinition
            {
                Key = "player",
                TilesheetSources = [new() { Tilesheet = "actors", GtsPath = "tiles.gts" }],
                SceneSources = [new() { SceneId = "level", GscnPath = "scenes.gscn" }]
            });
            plan.Add("audio.gsnd", new AudioDefinition
            {
                Key = "music",
                Resources = [new() { Key = "theme", FilePath = "../theme.ogg" }]
            });
        });
        var result = provider.Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        string Output(string filename) => Path.Combine(request.OutputDirectory, filename);
        var animation = AnimationDefinitionSerializer.Load(Output("animations.gani"), "walk");
        Assert.Equal("run", animation.NextCycleKey);
        Assert.Equal("actors", animation.Frames[0].Tilesheet);
        Assert.Equal("actors", animation.TilesheetSources[0].Tilesheet);
        Assert.Equal("tiles.gts", animation.TilesheetSources[0].GtsPath);
        Assert.Equal("../atlas.png", TilesheetDefinitionSerializer.Load(Output("tiles.gts"), "actors").Image.FilePath);
        var scene = SceneDefinitionSerializer.Load(Output("scenes.gscn"));
        Assert.Equal("run", scene.AnimationSources[0].AnimationKey);
        Assert.Equal("animations.gani", scene.AnimationSources[0].GaniPath);
        var sprites = SpriteDefinitionSerializer.Load(Output("sprites.gspr"));
        Assert.Equal("level", sprites.SceneSources[0].SceneId);
        Assert.Equal("scenes.gscn", sprites.SceneSources[0].GscnPath);
        Assert.Equal("actors", sprites.TilesheetSources[0].Tilesheet);
        Assert.Equal("../theme.ogg", AudioDefinitionSerializer.Load(Output("audio.gsnd")).Resources[0].FilePath);
        Assert.False(File.Exists(Path.Combine(request.OutputDirectory, "theme.ogg")));
    }

    /// <summary>Providers explicitly choose grouping by output filename, and key comparisons remain ordinal.</summary>
    [Fact]
    public void SeparateDestinationsStaySeparate()
    {
        var result = new Provider(plan =>
        {
            Native(plan, "one.gts", "GTS", "terrain");
            Native(plan, "two.gts", "GTS", "Terrain");
        }).Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal(2, result.WrittenFiles.Count);
        Assert.All(result.Analysis.Artifacts, artifact => Assert.Single(artifact.LogicalKeys));
        Assert.Equal("terrain", TilesheetDefinitionSerializer.Load(result.WrittenFiles[0]).Name);
        Assert.Equal("Terrain", TilesheetDefinitionSerializer.Load(result.WrittenFiles[1]).Name);
    }

    /// <summary>Raw artifacts never merge with typed documents or change their byte content.</summary>
    [Fact]
    public void RawAndDifferentTypedOutputsDoNotMerge()
    {
        var mixed = new Provider(plan =>
        {
            Native(plan, "same.gts", "GTS", "one");
            plan.Add("same.gts", "GTS", "two", [1, 2]);
        }).Import(request);
        Assert.Contains(mixed.Analysis.Diagnostics, diagnostic => diagnostic.Code == "output.duplicate");
        var types = new Provider(plan =>
        {
            Native(plan, "same.gts", "GTS", "one");
            Native(plan, "same.gts", "GSND", "two");
        }).Import(request);
        Assert.Contains(types.Analysis.Diagnostics, diagnostic => diagnostic.Code == "output.duplicate");
        var raw = new Provider(plan => plan.Add("image.png", "PNG", null, [1, 2, 3])).Import(request);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Assert.Single(raw.WrittenFiles)));
        Assert.Empty(Assert.Single(raw.Analysis.Artifacts).LogicalKeys);
        Assert.Equal(new[] { "legacy" }, new ExternalImportArtifact("GTS", "old.gts", "legacy").LogicalKeys);
    }
}
