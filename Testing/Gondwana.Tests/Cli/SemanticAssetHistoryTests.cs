using Gondwana.Assets;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tests.Cli;

public sealed partial class SemanticAssetPackTests
{
    [Fact]
    public void FiveNativePayloadKindsReplaySavedReferenceTargetsAfterAdoption()
    {
        Content(true, false);
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        using var source = AssetsFile.LoadOrCreate(Output, "secret", true, register: false);
        using var edits = new AssetEditSession(source, false);
        var plan = AssetImportPlan.Create(source,
            new[] { "actors.gspr", "sounds.gsnd" }.Select(name => new AssetImportSource(Path.Combine(Source, name), AssetImportPlan.InferType(name), name)), true);
        edits.Import(plan);
        var copy = Path.Combine(Path.GetDirectoryName(Output)!, "copy.gaf");
        AssetArchiveCopy.Save(source, copy, "secret", true);
        using var adopted = AssetsFile.LoadOrCreate(copy, "secret", true, register: false);
        edits.AcceptSavedArchive(adopted);
        var checkpoint = AssetImportPlan.Snapshot(adopted);
        Assert.Equal(5, checkpoint.Keys.Where(e => PackDefinitionDocument.IsNative(e.AssetType)).Select(e => e.AssetType).Distinct().Count());
        edits.History.Undo(); Assert.Empty(adopted.GetAllEntries());
        edits.History.Redo(); Assert.False(edits.History.IsDirty);
        var restored = AssetImportPlan.Snapshot(adopted);
        foreach (var pair in checkpoint) Assert.Equal(pair.Value, restored[pair.Key]);
    }
}
