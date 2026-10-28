using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Studio.WinForms.Panels;

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
        // The obsolete scan still owns the shared reader gate until it actually exits.
        first.SetResult(oldScan);
        Assert.True(AwaitUi(CompleteDependencyTask(oldTask)));
        second.SetResult(newScan);
        Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
        Assert.Same(newScan, studio.DependencyScan);
        studio.ShowDependencies(new(new AssetLocation(AssetTypes.Image, Path.Combine(other, "another.png"))), follow: false);
        Assert.Equal(2, calls); // completed snapshot reused for another query
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DependencySaveWaitsForActualReaderReleaseWithoutBlockingUi(bool parentArchive) => RunSta(root =>
    {
        var archive = NativeArchive(root);
        var snapshot = DefinitionReferenceDiscovery.Inspect(root);
        using var studio = Host(root);
        var document = parentArchive ? studio.OpenDocument(archive) : studio.OpenDocument(NativeLocation(archive, "gsnd"));
        WaitForArchives(studio.Browser);
        if (!parentArchive) RenameFirst(document.Document, "saved-after-scan");
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken scanToken = default;
        studio.InspectDependencies = (_, _, token) => Task.Run(async () =>
        {
            scanToken = token;
            using var heldArchive = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
            opened.SetResult(true);
            await release.Task;
            return snapshot;
        });
        studio.ShowDependencies();
        var scan = studio.DependencyRefresh;
        Assert.True(AwaitUi(opened.Task));
        try
        {
            var save = studio.SaveDocument(document);
            Assert.False(save.IsCompleted);
            Assert.True(scanToken.IsCancellationRequested);
            bool responsive = false;
            studio.BeginInvoke(() => responsive = true);
            Application.DoEvents();
            Assert.True(responsive);
            Assert.False(save.IsCompleted);
            release.SetResult(true);
            Assert.True(AwaitUi(save));
            Assert.True(AwaitUi(CompleteDependencyTask(scan)));
            Assert.Null(studio.DependencyScan);
            Assert.False(document.Document.Dirty());
        }
        finally { release.TrySetResult(true); }
    });

    [Theory]
    [InlineData("Find References", true)]
    [InlineData("Show Dependencies", false)]
    [InlineData("Show Dependents", true)]
    public void DependencyBrowserCommandsSelectRequestedSection(string command, bool dependents) => RunSta(root =>
    {
        var archive = NativeArchive(root);
        var loose = Path.Combine(root, "loose.gsnd");
        File.WriteAllText(loose, NativeJson("gsnd"));
        using var studio = Host(root);
        WaitForArchives(studio.Browser);
        foreach (var node in new[]
        {
            new TreeNode { Tag = loose },
            new TreeNode { Tag = new PackedAssetNodeIdentity(archive, AssetTypes.AudioDefinition, "nested/same.gsnd") }
        })
        {
            Assert.True(studio.Browser.PrepareFileMenu(node));
            studio.Browser.FileMenu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == command).PerformClick();
            Assert.True(AwaitUi(CompleteDependencyTask(studio.DependencyRefresh)));
            var tree = studio.DependencyPanel.Controls.OfType<TreeView>().Single();
            Assert.Same(tree.Nodes[dependents ? 1 : 0], tree.SelectedNode);
            Assert.Empty(studio.Documents);
        }
    });

    [Theory]
    [InlineData("gsnd")]
    [InlineData("gspr")]
    public void DependencyNavigationRefreshesVisibleMemberContentWithoutLosingEdits(string format) => RunSta(root =>
    {
        var file = Path.Combine(root, "visible." + format);
        File.WriteAllText(file, format == "gsnd"
            ? AudioDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions = [
                new() { Key = "first", Resources = [new() { Key = "old", FilePath = "old.wav" }] },
                new() { Key = "second", Resources = [new() { Key = "new", FilePath = "new.wav" }] }]
            })
            : SpriteDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions = [
                new() { Key = "first", Sprites = [new() { Nickname = "old" }] },
                new() { Key = "second", Sprites = [new() { Nickname = "new" }] }]
            }));
        using var studio = Host(root);
        var doc = studio.OpenDocument(file);
        RenameFirst(doc.Document, "edited-first");
        var before = doc.Document.SerializePacked!();
        Assert.True(studio.Navigate(new(doc.Document.Location!, "second")).Exact);
        if (doc.Document.Editor is AudioEditorControl audio)
        {
            var list = (ListView)Private(audio, "_resources");
            Assert.Equal("new", Assert.Single(list.Items.Cast<ListViewItem>()).Text);
            Assert.Same(audio.Document.Definition.Resources[0], list.Items[0].Tag);
        }
        else
        {
            var sprites = (SpriteEditorControl)doc.Document.Editor;
            var list = (ListBox)Private(sprites, "_sprites");
            Assert.Same(sprites.Document.Definition.Sprites[0], Assert.Single(list.Items.Cast<object>()));
        }
        Assert.True(doc.Document.Dirty());
        Assert.Equal(before, doc.Document.SerializePacked!());
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
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        studio.InspectDependencies = (_, _, token) => { captured = token; started.SetResult(true); return pending.Task; };
        studio.ShowDependencies();
        var task = studio.DependencyRefresh;
        Assert.True(AwaitUi(started.Task));
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
