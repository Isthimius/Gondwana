using System.Text;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    [Fact]
    public void ResourceAddressesUseTypedPhysicalAndLogicalComparisonRules()
    {
        var path = Path.Combine(Source, "assets.gaf");
        var a = new ResourceAddress(new AssetLocation(AssetTypes.AnimationDefinition, path, "nested/cycle.gani"), "Cycle");
        var b = new ResourceAddress(new AuthoringResource(path, AssetTypes.AnimationDefinition, "NESTED/CYCLE.GANI"), "cycle");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { Location = new(AssetTypes.TilesheetDefinition, path, "nested/cycle.gani") });
        Assert.NotEqual(a, b with { Location = new(AssetTypes.AnimationDefinition, path) });
        Assert.NotEqual(a, b with { FieldPath = "Frames[0]" });
    }

    [Fact]
    public void DependencyInspectionRepresentsRemoteAudioWithoutLocalTargets()
    {
        var path = Path.Combine(Source, "remote.gsnd");
        File.WriteAllText(path, AudioDefinitionSerializer.ToDocumentJson(new()
        {
            Definitions = [new() { Key = "remote", Resources = [new() { Key = "stream", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.org/audio.ogg" }] }]
        }));
        var scan = DefinitionReferenceDiscovery.Inspect(Source);
        Assert.True(scan.IsComplete, string.Join("\n", scan.Problems));
        Assert.Empty(scan.References);
        var remote = Assert.Single(scan.ExternalReferences);
        Assert.Equal("remote", remote.Source.Member);
        Assert.Equal("Resources[0].SourceUri", remote.Source.FieldPath);
        Assert.Equal("https://example.org/audio.ogg", remote.Uri);
        var archivePath = Path.Combine(Source, "remote.gaf");
        using (var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false))
        {
            archive.Add(AssetTypes.AudioDefinition, path, "nested/remote.gsnd");
            archive.Save();
        }
        var query = new DefinitionReferenceQuery(DefinitionReferenceDiscovery.Inspect(Source));
        Assert.Single(query.ExternalDependencies(new(new AssetLocation(AssetTypes.Misc, archivePath))));
        var packed = new AssetLocation(AssetTypes.AudioDefinition, archivePath, "nested/remote.gsnd");
        Assert.Single(query.ExternalDependencies(new(packed, "remote")));
        Assert.Empty(query.ExternalDependencies(new(packed, "other")));
        Assert.Empty(query.ExternalDependencies(new(new AssetLocation(AssetTypes.Misc, Path.Combine(Source, "other.gaf")))));
    }

    [Fact]
    public void DependencyInspectionReportsInaccessibleFilesAndInvalidDuplicates()
    {
        Content(true, false);
        var path = Path.Combine(Source, "tiles.gts");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var scan = DefinitionReferenceDiscovery.Inspect(Source);
            Assert.False(scan.IsComplete);
            Assert.Contains(scan.Problems, p => p.Kind == ReferenceProblemKind.Inaccessible);
        }
        var json = File.ReadAllText(path);
        File.WriteAllText(path, json.Replace("terrain1", "terrain0"));
        var duplicate = DefinitionReferenceDiscovery.Inspect(Source);
        Assert.False(duplicate.IsComplete);
        Assert.Contains(duplicate.Problems, p => p.Source.Contains("tiles.gts"));
    }

    [Fact]
    public void DependencyQueriesDistinguishEveryLogicalMemberAndPhysicalDocument()
    {
        Content(true, false);
        var scan = DefinitionReferenceDiscovery.Scan(Source);
        var query = new DefinitionReferenceQuery(scan);
        foreach (var group in scan.References.GroupBy(r => r.Document))
        {
            Assert.Equal(group.Count(), query.Dependencies(new(group.Key)).Count);
            foreach (var member in group.Select(r => r.Member).Distinct())
                Assert.All(query.Dependencies(new(group.Key, member)), r => Assert.Equal(member, r.Member));
        }
        var sheet = new AssetLocation(AssetTypes.TilesheetDefinition, Path.Combine(Source, "tiles.gts"));
        Assert.Equal(6, query.Dependents(new(sheet)).Count);
        Assert.Equal(3, query.Dependents(new(sheet, "terrain1")).Count);
        Assert.Empty(query.Dependents(new(sheet, "Terrain1")));
        var animation = new AssetLocation(AssetTypes.AnimationDefinition, Path.Combine(Source, "cycles.gani"));
        Assert.Equal(2, query.Dependents(new(animation, "WALK1")).Count);
        var image = new AssetLocation(AssetTypes.Image, Path.Combine(Source, "art0.png"));
        Assert.Single(query.UnspecifiedDependents(new(image, "invented-member")));
        Assert.Empty(query.Dependents(new(image, "invented-member")));
    }

    [Fact]
    public void DependencyInspectionRetainsValidMembersAndReportsMissingTargets()
    {
        Content(true, false);
        File.Delete(Path.Combine(Source, "art0.png"));
        var scan = DefinitionReferenceDiscovery.Inspect(Source);
        Assert.False(scan.IsComplete);
        Assert.Contains(scan.Problems, p => p.Kind == ReferenceProblemKind.Missing && p.Reference?.Member == "terrain0");
        var query = new DefinitionReferenceQuery(scan);
        Assert.Single(query.UnresolvedDependents(new(new AssetLocation(AssetTypes.Image, Path.Combine(Source, "art0.png")))));
        Assert.Single(query.UnresolvedDependencies(new(new AssetLocation(AssetTypes.TilesheetDefinition, Path.Combine(Source, "tiles.gts")), "terrain0")));
        Assert.Contains(scan.References, r => r.Member == "terrain1" && r.Target.AssetType == AssetTypes.Image);
        Assert.ThrowsAny<Exception>(() => DefinitionReferenceDiscovery.Scan(Source));
        Assert.Empty(scan.Archives);
        Assert.Empty(scan.Contents);
    }

    [Fact]
    public void DependencyInspectionReportsCancellationAndMalformedContent()
    {
        Content(true, false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = DefinitionReferenceDiscovery.Inspect(Source, cancellation: cancellation.Token);
        Assert.True(cancelled.IsCancelled);
        Assert.False(cancelled.IsComplete);
        File.WriteAllText(Path.Combine(Source, "broken.gts"), "{invalid");
        var partial = DefinitionReferenceDiscovery.Inspect(Source);
        Assert.False(partial.IsComplete);
        Assert.NotEmpty(partial.References);
        Assert.Contains(partial.Problems, p => p.Source.Contains("broken.gts"));
    }

    [Fact]
    public void DependencyQueriesKeepCrossArchiveNestedEntriesDistinctAndReportOutsideScope()
    {
        Image("art.png");
        var first = Path.Combine(Source, "one.gaf");
        var second = Path.Combine(_root, "two.gaf");
        foreach (var path in new[] { first, second })
        {
            using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
            archive.Add(AssetTypes.Image, Path.Combine(Source, "art.png"), "nested/images/art.png");
            var sheet = Sheet("terrain", "unused");
            sheet.Image = new() { AssetsFilePath = second, AssetEntryName = "nested/images/art.png" };
            using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "nested/definitions/tiles.gts", bytes);
            archive.Save();
        }
        var scan = DefinitionReferenceDiscovery.Inspect(first);
        Assert.True(scan.IsComplete, string.Join("\n", scan.Problems));
        var reference = Assert.Single(scan.References);
        Assert.Equal("nested/definitions/tiles.gts", reference.Document.AssetEntryName, ignoreCase: true);
        Assert.Equal(second, reference.Target.Path);
        Assert.False(scan.IsWithinScope(reference.Target));
        var query = new DefinitionReferenceQuery(scan);
        Assert.Single(query.Dependents(new(reference.Target)));
        Assert.Empty(query.Dependents(new(new AssetLocation(AssetTypes.Image, first, reference.Target.AssetEntryName))));
        Assert.Empty(query.Dependents(new(new AssetLocation(AssetTypes.Image, second))));
        File.Delete(first);
        File.Delete(second); // inspection retains no archive handles
    }

    [Fact]
    public void DependencyInspectionAuthenticatesArchivesIndependently()
    {
        Image("art.png");
        var path = Path.Combine(Source, "encrypted.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, "secret", true, register: false))
        {
            archive.Add(AssetTypes.Image, Path.Combine(Source, "art.png"), "art.png");
            archive.Save();
        }
        Assert.Contains(DefinitionReferenceDiscovery.Inspect(Source).Problems, p => p.Kind == ReferenceProblemKind.Authentication);
        Assert.Contains(DefinitionReferenceDiscovery.Inspect(Source, _ => "wrong").Problems, p => p.Kind == ReferenceProblemKind.Authentication);
        Assert.True(DefinitionReferenceDiscovery.Inspect(Source, _ => "secret").IsComplete);
    }

    [Fact]
    public void DependencyInspectionRejectsAmbiguousLogicalSources()
    {
        Content(false, false);
        File.Copy(Path.Combine(Source, "tiles.gts"), Path.Combine(Source, "duplicate.gts"));
        var scan = DefinitionReferenceDiscovery.Inspect(Source);
        Assert.Contains(scan.Problems, p => p.Kind == ReferenceProblemKind.Ambiguous);
        Assert.False(scan.IsComplete);
    }
}
