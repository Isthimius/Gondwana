using System.Text;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

/// <summary>Cross-format contracts for pure physical reference transformations and persistence.</summary>
public sealed class ReferenceRelocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Relocation-" + Guid.NewGuid());
    private sealed record Format(string Identity, AssetTypes Type, Func<string, object> Parse,
        Func<object, string> Json, Func<string, object> Load, Action<string, object> Save,
        Func<object, AssetLocation, AssetLocation, IEnumerable<ReferenceRelocation>, bool, object> Relocate);
    private sealed record Reference(string? Collection, string FileProperty, AssetTypes Type, string Extension);

    private static Format Get(string extension) => extension switch
    {
        "gts" => new("Name", AssetTypes.TilesheetDefinition,
            json => TilesheetDefinitionSerializer.FromDocumentJson(json),
            doc => TilesheetDefinitionSerializer.ToDocumentJson((TilesheetDefinitionDocument)doc),
            path => TilesheetDefinitionSerializer.LoadDocument(path),
            (path, doc) => TilesheetDefinitionSerializer.SaveDocument(path, (TilesheetDefinitionDocument)doc),
            (doc, source, destination, mappings, strict) => DefinitionReferenceRelocator.Relocate((TilesheetDefinitionDocument)doc, source, destination, mappings, strict)),
        "gani" => new("Key", AssetTypes.AnimationDefinition,
            json => AnimationDefinitionSerializer.FromDocumentJson(json),
            doc => AnimationDefinitionSerializer.ToDocumentJson((AnimationDefinitionDocument)doc),
            path => AnimationDefinitionSerializer.LoadDocument(path),
            (path, doc) => AnimationDefinitionSerializer.SaveDocument(path, (AnimationDefinitionDocument)doc),
            (doc, source, destination, mappings, strict) => DefinitionReferenceRelocator.Relocate((AnimationDefinitionDocument)doc, source, destination, mappings, strict)),
        "gscn" => new("ID", AssetTypes.SceneDefinition,
            json => SceneDefinitionSerializer.FromDocumentJson(json),
            doc => SceneDefinitionSerializer.ToDocumentJson((SceneDefinitionDocument)doc),
            path => SceneDefinitionSerializer.LoadDocument(path),
            (path, doc) => SceneDefinitionSerializer.SaveDocument(path, (SceneDefinitionDocument)doc),
            (doc, source, destination, mappings, strict) => DefinitionReferenceRelocator.Relocate((SceneDefinitionDocument)doc, source, destination, mappings, strict)),
        "gspr" => new("Key", AssetTypes.SpriteDefinition,
            json => SpriteDefinitionSerializer.FromDocumentJson(json),
            doc => SpriteDefinitionSerializer.ToDocumentJson((SpriteDefinitionDocument)doc),
            path => SpriteDefinitionSerializer.LoadDocument(path),
            (path, doc) => SpriteDefinitionSerializer.SaveDocument(path, (SpriteDefinitionDocument)doc),
            (doc, source, destination, mappings, strict) => DefinitionReferenceRelocator.Relocate((SpriteDefinitionDocument)doc, source, destination, mappings, strict)),
        "gsnd" => new("Key", AssetTypes.AudioDefinition,
            json => AudioDefinitionSerializer.FromDocumentJson(json),
            doc => AudioDefinitionSerializer.ToDocumentJson((AudioDefinitionDocument)doc),
            path => AudioDefinitionSerializer.LoadDocument(path),
            (path, doc) => AudioDefinitionSerializer.SaveDocument(path, (AudioDefinitionDocument)doc),
            (doc, source, destination, mappings, strict) => DefinitionReferenceRelocator.Relocate((AudioDefinitionDocument)doc, source, destination, mappings, strict)),
        _ => throw new ArgumentException(extension)
    };

    private static Reference[] References(string extension) => extension switch
    {
        "gts" => [new(null, "FilePath", AssetTypes.Image, "png")],
        "gsnd" => [new("Resources", "FilePath", AssetTypes.Audio, "wav")],
        "gani" => [new("TilesheetSources", "GtsPath", AssetTypes.TilesheetDefinition, "gts")],
        "gscn" => [new("TilesheetSources", "GtsPath", AssetTypes.TilesheetDefinition, "gts"),
            new("AnimationSources", "GaniPath", AssetTypes.AnimationDefinition, "gani")],
        _ => [new("TilesheetSources", "GtsPath", AssetTypes.TilesheetDefinition, "gts"),
            new("AnimationSources", "GaniPath", AssetTypes.AnimationDefinition, "gani"),
            new("SceneSources", "GscnPath", AssetTypes.SceneDefinition, "gscn")]
    };

    private string Full(string relative) => Path.GetFullPath(Path.Combine(_root, relative));
    private AssetLocation DocumentLocation(Format format, string folder, string extension, bool packed) =>
        new(format.Type, Full(folder + "/" + (packed ? "content.gaf" : "members." + extension)), packed ? "nested/members." + extension : null);
    private AssetLocation Dependency(Reference reference, string folder, bool packed) =>
        new(reference.Type, Full(folder + "/" + (packed ? "resources.gaf" : "resource." + reference.Extension)),
            packed ? "resource." + reference.Extension : null);

    private static JObject Member(Format format, string extension, string key, AssetLocation document, IEnumerable<(Reference Reference, AssetLocation Location)> dependencies)
    {
        var member = new JObject { [format.Identity] = key };
        if (extension == "gani") member["NextCycleKey"] = "untouched-next";
        foreach (var (reference, location) in dependencies)
        {
            var packed = location.AssetEntryName is not null;
            var item = new JObject
            {
                [packed ? "AssetsFilePath" : reference.FileProperty] = Path.GetRelativePath(Path.GetDirectoryName(document.Path)!, location.Path)
            };
            if (packed) item["AssetEntryName"] = location.AssetEntryName;
            if (reference.Collection is null) member["Image"] = item;
            else
            {
                item[extension == "gsnd" ? "SourceKind" : "Kind"] = packed ?
                    (extension == "gsnd" ? "PackedAsset" : "PackedDefinitionFile") :
                    (extension == "gsnd" ? "LooseFile" : "LooseDefinitionFile");
                item[reference.Type == AssetTypes.TilesheetDefinition ? "Tilesheet" :
                    reference.Type == AssetTypes.AnimationDefinition ? "AnimationKey" :
                    reference.Type == AssetTypes.SceneDefinition ? "SceneId" : "Key"] = "logical-member";
                member[reference.Collection] = new JArray(item);
            }
        }
        return member;
    }

    private static JObject ReferenceObject(JToken member, Reference reference) =>
        (JObject)(reference.Collection is null ? member["Image"]! : member[reference.Collection]![0]!);

    /// <summary>Enumerates each format, dependency transition, and containing-document transition.</summary>
    public static IEnumerable<object[]> Transitions()
    {
        foreach (var extension in new[] { "gts", "gani", "gscn", "gspr", "gsnd" })
            foreach (var sourcePacked in new[] { false, true })
                foreach (var targetPacked in new[] { false, true })
                    foreach (var documentPacked in new[] { false, true })
                        yield return [extension, sourcePacked, targetPacked, documentPacked];
    }

    /// <summary>Rewrites all physical forms, keeps siblings and logical fields, and round-trips Save As.</summary>
    /// <param name="extension">The native format.</param>
    /// <param name="sourcePacked">Whether dependencies start packed.</param>
    /// <param name="targetPacked">Whether dependencies end packed.</param>
    /// <param name="documentPacked">Whether the input document is packed; its destination uses the opposite form.</param>
    [Theory]
    [MemberData(nameof(Transitions))]
    public void TransformsCompleteDocuments(string extension, bool sourcePacked, bool targetPacked, bool documentPacked)
    {
        var format = Get(extension);
        var source = DocumentLocation(format, "original", extension, documentPacked);
        var destination = DocumentLocation(format, "destination", extension, !documentPacked);
        var references = References(extension);
        var mappings = references.Select(reference => new ReferenceRelocation(
            Dependency(reference, "resources", sourcePacked), Dependency(reference, "relocated", targetPacked))).ToArray();
        var originalJson = new JObject
        {
            ["Definitions"] = new JArray(
            Member(format, extension, "first", source, references.Select((r, i) => (r, mappings[i].Source))),
            Member(format, extension, "sibling", source, references.Select(r => (r, Dependency(r, "unrelated", sourcePacked)))))
        }.ToString();
        var input = format.Parse(originalJson);
        var before = format.Json(input);
        var output = format.Relocate(input, source, destination, mappings, true);
        Assert.Equal(before, format.Json(input));
        Assert.All(Members(output), member => Assert.Null(Stamp(member)));
        Assert.NotSame(Members(input).First(), Members(output).First());
        var outputJson = JObject.Parse(format.Json(output));
        Assert.Equal(2, outputJson["Definitions"]!.Count());
        Assert.Equal("first", (string?)outputJson["Definitions"]![0]![format.Identity]);
        Assert.Equal("sibling", (string?)outputJson["Definitions"]![1]![format.Identity]);
        for (int memberIndex = 0; memberIndex < 2; memberIndex++)
            for (int i = 0; i < references.Length; i++)
            {
                var expected = memberIndex == 0 ? mappings[i].Destination : Dependency(references[i], "unrelated", sourcePacked);
                var actual = ReferenceObject(outputJson["Definitions"]![memberIndex]!, references[i]);
                var storedPath = (string?)actual[expected.AssetEntryName is null ? references[i].FileProperty : "AssetsFilePath"];
                Assert.Equal(expected.Path, Path.GetFullPath(storedPath!, Path.GetDirectoryName(destination.Path)!));
                Assert.Equal(expected.AssetEntryName, (string?)actual["AssetEntryName"]);
                Assert.Null(actual[expected.AssetEntryName is null ? "AssetsFilePath" : references[i].FileProperty]);
                if (references[i].Collection is not null)
                    Assert.Contains("logical-member", actual.ToString());
            }
        if (extension == "gani") Assert.Equal("untouched-next", (string?)outputJson["Definitions"]![0]!["NextCycleKey"]);
        var repeated = format.Relocate(output, destination, destination, mappings, true);
        Assert.Equal(format.Json(output), format.Json(repeated));
        // Saving a transformed authoring copy must use its new base, even though it has no load stamp.
        var savedPath = Full("save-as/combined." + extension);
        format.Save(savedPath, output);
        var loaded = JObject.Parse(format.Json(format.Load(savedPath)));
        for (int i = 0; i < references.Length; i++)
        {
            var item = ReferenceObject(loaded["Definitions"]![0]!, references[i]);
            Assert.Equal(mappings[i].Destination.Path, Path.GetFullPath(
                (string)item[targetPacked ? "AssetsFilePath" : references[i].FileProperty]!, Path.GetDirectoryName(savedPath)!));
        }
        Assert.DoesNotContain(".tmp", File.ReadAllText(savedPath));
        Assert.All(loaded["Definitions"]!, member => Assert.Null(member["Source"]));
    }

    /// <summary>Missing and ambiguous mappings fail without mutating any member; wider batches can opt out of missing checks.</summary>
    /// <param name="extension">The native format.</param>
    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void FailedBatchLeavesOriginalUnchanged(string extension)
    {
        var format = Get(extension);
        var source = DocumentLocation(format, "original", extension, false);
        var reference = References(extension)[0];
        var from = Dependency(reference, "resources", false);
        var to = Dependency(reference, "relocated", true);
        var input = format.Parse(Member(format, extension, "legacy", source, [(reference, from)]).ToString());
        var before = format.Json(input);
        var mapping = new ReferenceRelocation(from, to);
        var missing = new ReferenceRelocation(Dependency(reference, "missing", false), Dependency(reference, "missing-new", true));
        Assert.Contains("not found", Assert.Throws<InvalidDataException>(() => format.Relocate(input, source, source, [mapping, missing], true)).Message);
        Assert.Equal(before, format.Json(input));
        Assert.Contains("Ambiguous", Assert.Throws<InvalidDataException>(() => format.Relocate(input, source, source, [mapping, mapping], true)).Message);
        Assert.Contains("chains", Assert.Throws<InvalidDataException>(() => format.Relocate(input, source, source,
            [mapping, new(to, Dependency(reference, "third", false))], true)).Message);
        Assert.Equal(before, format.Json(input));
        Assert.NotEqual(before, format.Json(format.Relocate(input, source, source, [mapping, missing], false)));
        // No-op transformations preserve even the stored spelling of unrelated references.
        Assert.Equal(before, format.Json(format.Relocate(input, source, source, [], true)));
    }

    /// <summary>Endpoint construction cannot silently change asset types or resolve against the working directory.</summary>
    [Fact]
    public void RejectsIncompatibleAndIncompleteLocations()
    {
        Assert.Throws<ArgumentException>(() => new AssetLocation(AssetTypes.Image, "relative.png"));
        Assert.Throws<ArgumentException>(() => new AssetLocation(AssetTypes.Image, Full("a.gaf"), " "));
        Assert.Throws<ArgumentException>(() => new ReferenceRelocation(new(AssetTypes.Image, Full("a.png")), new(AssetTypes.Audio, Full("a.wav"))));
    }

    /// <summary>URI resources bypass file transformation, and extensionless packed audio keeps its encoding hint.</summary>
    [Fact]
    public void AudioUrisAndFormatHintsArePreserved()
    {
        var source = new AssetLocation(AssetTypes.AudioDefinition, Full("audio/sounds.gsnd"));
        var input = new AudioDefinitionDocument
        {
            Definitions = [new() { Key = "sounds", Resources = [
            new() { Key = "web", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.com/sound.wav" },
            new() { Key = "local", FilePath = "../sound.wav" }] }]
        };
        var output = DefinitionReferenceRelocator.Relocate(input, source, source,
            [new(new(AssetTypes.Audio, Full("sound.wav")), new(AssetTypes.Audio, Full("archive.gaf"), "sound"))]);
        Assert.Equal("https://example.com/sound.wav", output.Definitions[0].Resources[0].SourceUri);
        Assert.Equal(".wav", output.Definitions[0].Resources[1].SourceExtension);
        Assert.Empty(AudioDefinitionSerializer.ValidateDocument(output));
        Assert.Null(input.Definitions[0].Resources[1].SourceExtension);
    }

    private static IEnumerable<object> Members(object document) => document switch
    {
        TilesheetDefinitionDocument doc => doc.Definitions,
        AnimationDefinitionDocument doc => doc.Definitions,
        SceneDefinitionDocument doc => doc.Definitions,
        SpriteDefinitionDocument doc => doc.Definitions,
        AudioDefinitionDocument doc => doc.Definitions,
        _ => throw new ArgumentException("Unknown document type.", nameof(document))
    };

    private static DefinitionLoadStamp? Stamp(object member) => member switch
    {
        TilesheetDefinition definition => definition.LoadStamp,
        AnimationDefinition definition => definition.LoadStamp,
        SceneDefinition definition => definition.LoadStamp,
        SpriteDefinition definition => definition.LoadStamp,
        AudioDefinition definition => definition.LoadStamp,
        _ => null
    };

    private static string? SelfPath(object member) => member switch
    {
        TilesheetDefinition definition => definition.Source.GtsFilePath,
        AnimationDefinition definition => definition.Source.GaniFilePath,
        SceneDefinition definition => definition.Source.GscnFilePath,
        SpriteDefinition definition => definition.Source.GsprFilePath,
        AudioDefinition definition => definition.Source.GsndFilePath,
        _ => null
    };

    private static object Packed(string extension, AssetsFile archive) => extension switch
    {
        "gts" => TilesheetDefinitionSerializer.LoadDocument(archive, "members.gts"),
        "gani" => AnimationDefinitionSerializer.LoadDocument(archive, "members.gani"),
        "gscn" => SceneDefinitionSerializer.LoadDocument(archive, "members.gscn"),
        "gspr" => SpriteDefinitionSerializer.LoadDocument(archive, "members.gspr"),
        "gsnd" => AudioDefinitionSerializer.LoadDocument(archive, "members.gsnd"),
        _ => throw new ArgumentException(extension)
    };

    /// <summary>Legacy absolute metadata cannot override the actual source, and Save As preserves loose/packed member stamps.</summary>
    /// <param name="extension">The native format.</param>
    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void PersistenceReconstructsActualProvenance(string extension)
    {
        var format = Get(extension);
        var source = DocumentLocation(format, "original", extension, false);
        var reference = References(extension)[0];
        var dependency = Dependency(reference, "resources", false);
        var legacy = Member(format, extension, "legacy", source, [(reference, dependency)]);
        ReferenceObject(legacy, reference)[reference.FileProperty] = dependency.Path;
        legacy["Source"] = new JObject { ["Kind"] = 1, [char.ToUpperInvariant(extension[0]) + extension[1..] + "FilePath"] = Full("stale/.temporary.tmp") };
        Directory.CreateDirectory(Path.GetDirectoryName(source.Path)!);
        File.WriteAllText(source.Path, legacy.ToString());
        var loaded = format.Load(source.Path);
        var member = Assert.Single(Members(loaded));
        Assert.Equal(source.Path, SelfPath(member));
        var provenance = Stamp(member)!.Materialized(member, () => member)!;
        Assert.Equal(source.Path, provenance.FilePath);
        Assert.Equal("legacy", provenance.DefinitionIdentity);
        var saved = Full("saved/final." + extension);
        format.Save(saved, loaded);
        var json = File.ReadAllText(saved);
        Assert.DoesNotContain(".tmp", json);
        Assert.DoesNotContain("FilePath\": \"" + _root.Replace("\\", "\\\\"), json);
        Assert.Null(JObject.Parse(json)["Definitions"]![0]!["Source"]);
        var reloaded = Assert.Single(Members(format.Load(saved)));
        Assert.Equal(saved, SelfPath(reloaded));
        Assert.Equal(saved, Stamp(reloaded)!.Materialized(reloaded, () => reloaded)!.FilePath);
        var archivePath = Full("packed/content.gaf");
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        using var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        archive.Add(format.Type, "members." + extension, stream);
        var packed = Assert.Single(Members(Packed(extension, archive)));
        var packedStamp = Stamp(packed)!.Materialized(packed, () => packed)!;
        Assert.Null(packedStamp.FilePath);
        Assert.Equal(archivePath, packedStamp.AssetsFilePath);
        Assert.Equal("members." + extension, packedStamp.AssetEntryName);
        Assert.Equal("legacy", packedStamp.DefinitionIdentity);
    }

    /// <summary>Platform roots are preserved when a relative dependency cannot be expressed.</summary>
    [Fact]
    public void DifferentVolumesRemainAbsolute()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = new AssetLocation(AssetTypes.TilesheetDefinition, @"C:\project\sheet.gts");
        var destination = new AssetLocation(AssetTypes.TilesheetDefinition, @"D:\project\sheet.gts");
        var input = new TilesheetDefinitionDocument { Definitions = [new() { Name = "terrain", Image = new() { FilePath = "art.png" } }] };
        var output = DefinitionReferenceRelocator.Relocate(input, source, destination, []);
        Assert.Equal(@"C:\project\art.png", output.Definitions[0].Image.FilePath);
    }

    /// <summary>Implicit GTS images stay in the original archive unless their own mapping moves them.</summary>
    [Fact]
    public void ImplicitPackedImageUsesContainingArchive()
    {
        var source = new AssetLocation(AssetTypes.TilesheetDefinition, Full("original/art.gaf"), "tiles.gts");
        var destination = new AssetLocation(AssetTypes.TilesheetDefinition, Full("new/art.gaf"), "renamed.gts");
        var input = new TilesheetDefinitionDocument
        {
            Definitions = [
            new() { Name = "first", Image = new() { AssetEntryName = "first.png" } },
            new() { Name = "sibling", Image = new() { AssetEntryName = "sibling.png" } }]
        };
        var output = DefinitionReferenceRelocator.Relocate(input, source, destination,
            [new(new(AssetTypes.Image, source.Path, "FIRST.PNG"), new(AssetTypes.Image, Full("new/first.png")))]);
        Assert.Equal("first.png", output.Definitions[0].Image.FilePath);
        Assert.Null(output.Definitions[0].Image.AssetEntryName);
        Assert.Equal(source.Path, Path.GetFullPath(output.Definitions[1].Image.AssetsFilePath!, Path.GetDirectoryName(destination.Path)!));
        Assert.Equal("sibling.png", output.Definitions[1].Image.AssetEntryName);
        Assert.Null(input.Definitions[1].Image.AssetsFilePath);
        var missingContext = new AssetLocation(AssetTypes.TilesheetDefinition, Full("loose.gts"));
        Assert.Contains("first", Assert.Throws<InvalidDataException>(() =>
            DefinitionReferenceRelocator.Relocate(input, missingContext, missingContext, [])).Message);
    }

    /// <summary>Ambiguous physical source representations fail after earlier members without altering the original.</summary>
    [Fact]
    public void AmbiguousReferenceFailureIsAtomic()
    {
        var location = new AssetLocation(AssetTypes.TilesheetDefinition, Full("tiles.gts"));
        var input = new TilesheetDefinitionDocument
        {
            Definitions = [
            new() { Name = "first", Image = new() { FilePath = "first.png" } },
            new() { Name = "bad", Image = new() { FilePath = "bad.png", AssetsFilePath = "bad.gaf", AssetEntryName = "bad.png" } }]
        };
        var before = TilesheetDefinitionSerializer.ToDocumentJson(input);
        var error = Assert.Throws<InvalidDataException>(() => DefinitionReferenceRelocator.Relocate(input, location, location,
            [new(new(AssetTypes.Image, Full("first.png")), new(AssetTypes.Image, Full("new.png")))]));
        Assert.Contains("bad", error.Message);
        Assert.Equal(before, TilesheetDefinitionSerializer.ToDocumentJson(input));
    }

    /// <summary>Logical animation chains, frame coordinates, and sprite scene/layer identities survive relocation.</summary>
    [Fact]
    public void LogicalContentIsIndependentOfPhysicalSources()
    {
        var location = new AssetLocation(AssetTypes.AnimationDefinition, Full("animations.gani"));
        var animation = new AnimationDefinition
        {
            Key = "Walk",
            NextCycleKey = "Walk",
            Frames = [
            new() { Tilesheet = "Actors", RegionName = "poses", XTile = 3, YTile = 7, DurationSeconds = .25 }],
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("Actors", "tiles.gts")]
        };
        var mapping = new ReferenceRelocation(new(AssetTypes.TilesheetDefinition, Full("tiles.gts")),
            new(AssetTypes.TilesheetDefinition, Full("bundle.gaf"), "shared.gts"));
        var output = DefinitionReferenceRelocator.Relocate(new AnimationDefinitionDocument { Definitions = [animation] }, location, location, [mapping]);
        Assert.Equal("Walk", output.Definitions[0].NextCycleKey);
        Assert.Equal(JArray.FromObject(animation.Frames).ToString(), JArray.FromObject(output.Definitions[0].Frames).ToString());
        Assert.NotSame(animation.Frames[0], output.Definitions[0].Frames[0]);
        var sprites = new SpriteDefinitionDocument
        {
            Definitions = [new() { Key = "actors", Sprites = [
            new() { Id = Guid.NewGuid(), SceneId = "World", SceneLayerId = "Ground", AnimationKey = "Walk",
                Frame = new() { Tilesheet = "Actors", XTile = 3, YTile = 7 } }],
            TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("Actors", "tiles.gts")] }]
        };
        var spriteLocation = new AssetLocation(AssetTypes.SpriteDefinition, Full("sprites.gspr"));
        var relocated = DefinitionReferenceRelocator.Relocate(sprites, spriteLocation, spriteLocation, [mapping]);
        Assert.Equal(JArray.FromObject(sprites.Definitions[0].Sprites).ToString(), JArray.FromObject(relocated.Definitions[0].Sprites).ToString());
        Assert.NotSame(sprites.Definitions[0].Sprites[0].Frame, relocated.Definitions[0].Sprites[0].Frame);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
