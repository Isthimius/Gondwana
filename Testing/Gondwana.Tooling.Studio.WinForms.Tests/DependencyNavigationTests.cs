using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void DependencyNavigationSelectsNonFirstMemberInLooseAndPackedDocuments(string format) => RunSta(root =>
    {
        var archive = NativeArchive(root);
        var loose = Path.Combine(root, "loose." + format);
        File.WriteAllText(loose, NativeJson(format));
        using var studio = Host(root);
        foreach (var location in new[] { NativeLocation(archive, format), new AssetLocation(StudioDocument.TypeFor(format), loose) })
        {
            var result = studio.Navigate(new(location, "sibling"));
            Assert.True(result.Exact, result.Message);
            Assert.Equal("sibling", studio.ActiveDocument!.Document.SelectedMember);
            Assert.False(studio.ActiveDocument.Document.Dirty());
        }
        Assert.Equal(2, studio.Documents.Count);
    });

    private static async Task<bool> CompleteDependencyTask(Task task) { await task; return true; }

    [Fact]
    public void DependencyExplorerDiscardsObsoleteScansAndPresentsOnUiThread() => RunSta(root =>
    {
        var other = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;
        var oldScan = DefinitionReferenceDiscovery.Inspect(root);
        var newScan = DefinitionReferenceDiscovery.Inspect(other);
        using var studio = Host(root);
        var first = new TaskCompletionSource<DefinitionReferenceDiscovery>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<DefinitionReferenceDiscovery>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldToken = default;
        int calls = 0;
        studio.InspectDependencies = (_, _, token) =>
        {
            if (++calls == 1) { oldToken = token; return first.Task; }
            return second.Task;
        };
        int uiThread = Environment.CurrentManagedThreadId;
        var status = studio.DependencyPanel.Controls.OfType<Label>().Single();
        status.TextChanged += (_, _) => Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
        studio.ShowDependencies(new(new AssetLocation(AssetTypes.Image, Path.Combine(root, "image.png"))), follow: false);
        var oldTask = studio.DependencyRefresh;
        studio.SetWorkingDirectory(other);
        Assert.True(oldToken.IsCancellationRequested);
        studio.ShowDependencies(new(new AssetLocation(AssetTypes.Image, Path.Combine(other, "image.png"))), follow: false);
        second.SetResult(newScan);
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        first.SetResult(oldScan);
        Assert.True(AwaitUi(CompleteDependencyTask(oldTask)));
        Assert.Same(newScan, studio.DependencyScan);
        studio.ShowDependencies(new(new AssetLocation(AssetTypes.Image, Path.Combine(other, "another.png"))), follow: false);
        Assert.Equal(2, calls); // completed snapshot reused for another query
    });

    [Fact]
    public void DependencyExplorerInvalidatesAfterSaveAndCancelsOnDispose() => RunSta(root =>
    {
        var archive = NativeArchive(root);
        using var studio = Host(root);
        var document = studio.OpenDocument(NativeLocation(archive, "gsnd"));
        studio.ShowDependencies();
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        Assert.NotNull(studio.DependencyScan);
        RenameFirst(document.Document, "changed");
        Assert.Contains("unsaved", studio.DependencyPanel.Status);
        Assert.True(AwaitUi(studio.SaveDocument(document)));
        Assert.Null(studio.DependencyScan);
        Assert.True(AwaitUi(CompleteDependencyTask(studio.RefreshDependenciesAsync())));
        Assert.NotNull(studio.DependencyScan);
        studio.SetWorkingDirectory(root);
        var pending = new TaskCompletionSource<DefinitionReferenceDiscovery>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        studio.InspectDependencies = (_, _, token) => { captured = token; return pending.Task; };
        studio.ShowDependencies();
        var task = studio.DependencyRefresh;
        studio.Dispose();
        Assert.True(captured.IsCancellationRequested);
        pending.SetResult(DefinitionReferenceDiscovery.Inspect(root));
        Assert.True(AwaitUi(CompleteDependencyTask(task)));
    });

    [Theory]
    [InlineData("gts", "terrain")]
    [InlineData("gani", "water.flow")]
    [InlineData("gscn", "island")]
    public void DependencyNavigationOpensExactPackedMemberAndPreservesDirtyEditor(string format, string member) => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using var studio = Host(root);
        string entry = format switch { "gts" => "definitions/world.gts", "gani" => "definitions/water.gani", _ => "definitions/island.gscn" };
        var address = new ResourceAddress(new AssetLocation(StudioDocument.TypeFor(format), path, entry), member);
        Assert.True(studio.Navigate(address).Exact);
        var document = Assert.Single(studio.Documents);
        Assert.Equal(member, document.Document.SelectedMember);
        RenameFirst(document.Document, "edited");
        var bytes = document.Document.SerializePacked!();
        var result = studio.Navigate(address with { Member = "edited", FieldPath = "known.source[1]" });
        Assert.True(result.Opened);
        Assert.False(result.Exact);
        Assert.Contains("known.source[1]", result.Message);
        Assert.Same(document, Assert.Single(studio.Documents));
        Assert.True(document.Document.Dirty());
        Assert.Equal(bytes, document.Document.SerializePacked!());
        var missing = studio.Navigate(address with { Member = "missing" });
        Assert.True(missing.Opened);
        Assert.False(missing.Exact);
    });

    [Fact]
    public void DependencyNavigationUsesSaveAsIdentityAndReportsMissingTargets() => RunSta(root =>
    {
        var path = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using var studio = Host(root);
        var location = new AssetLocation(AssetTypes.TilesheetDefinition, path, "definitions/world.gts");
        Assert.True(studio.Navigate(new(location, "terrain")).Exact);
        var document = Assert.Single(studio.Documents);
        var destination = Path.Combine(root, "copy.gts");
        Assert.True(AwaitUi(studio.SaveDocument(document, saveAs: true, destination: destination)));
        Assert.True(studio.Navigate(new(new AssetLocation(AssetTypes.TilesheetDefinition, destination), "terrain")).Exact);
        Assert.Same(document, Assert.Single(studio.Documents));
        Assert.False(studio.Navigate(new(new AssetLocation(AssetTypes.TilesheetDefinition, Path.Combine(root, "missing.gts")))).Opened);
        Assert.False(studio.Navigate(new(new AssetLocation(AssetTypes.AnimationDefinition, destination))).Opened);
    });
}
