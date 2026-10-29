using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tests;

public sealed class AssetEditSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gaf-history-" + Guid.NewGuid().ToString("N"));
    public AssetEditSessionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private AssetsFile Archive(string name = "game.gaf", bool encrypt = false) => AssetsFile.LoadOrCreate(Path.Combine(_root, name), encrypt ? "secret" : null, encrypt, register: false);
    private static void Add(AssetsFile archive, AssetTypes type, string name, byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        archive.Add(type, name, stream);
    }
    private static byte[] Bytes(AssetsFile archive, AssetTypes type, string name)
    {
        using var stream = archive.Get(type, name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
    private AssetImportPlan Plan(AssetsFile archive, params (AssetTypes Type, string Name, byte[] Bytes)[] entries)
    {
        var sources = entries.Select((e, i) =>
        {
            var path = Path.Combine(_root, Guid.NewGuid() + "-" + i + ".bin");
            File.WriteAllBytes(path, e.Bytes);
            return new AssetImportSource(path, e.Type, e.Name);
        }).ToArray();
        return AssetImportPlan.Create(archive, sources, false);
    }

    [Fact]
    public void ImportRetainsExactTypedNestedDeltasAndReusedEntries()
    {
        using var archive = Archive();
        Add(archive, AssetTypes.Misc, "same", [1]);
        using var edits = new AssetEditSession(archive, true);
        var plan = Plan(archive, (AssetTypes.Misc, "same", [1]), (AssetTypes.Image, "nested/shared", [2, 3]), (AssetTypes.Audio, "nested/shared", [4, 5]));
        Assert.Equal(2, edits.Import(plan));
        Assert.Equal("Import 2 asset(s)", edits.History.UndoDescription);
        edits.History.Undo();
        Assert.Single(archive.GetAllEntries()); Assert.Equal(new byte[] { 1 }, Bytes(archive, AssetTypes.Misc, "same"));
        Assert.False(edits.History.IsDirty);
        // Sources are never reopened on redo.
        foreach (var file in Directory.GetFiles(_root, "*.bin")) File.Delete(file);
        edits.History.Redo();
        Assert.Equal(new byte[] { 2, 3 }, Bytes(archive, AssetTypes.Image, "nested/shared"));
        Assert.Equal(new byte[] { 4, 5 }, Bytes(archive, AssetTypes.Audio, "nested/shared"));
        Assert.True(edits.History.IsDirty);
    }

    [Fact]
    public void DependencyClosureIsOneStepAndReusedImageSurvives()
    {
        using var archive = Archive();
        var imagePath = Path.Combine(_root, "art.png");
        File.WriteAllBytes(imagePath, [1, 2, 3]); // pack import copies opaque image bytes
        Add(archive, AssetTypes.Image, "art.png", [1, 2, 3]);
        var sheetPath = Path.Combine(_root, "tiles.gts");
        TilesheetDefinitionSerializer.Save(sheetPath, new TilesheetDefinition
        {
            Name = "terrain",
            Image = new() { FilePath = "art.png" },
            Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 1, 1), TileSize = new Size(1, 1) }]
        });
        using var edits = new AssetEditSession(archive, true);
        var plan = AssetImportPlan.Create(archive, [new(sheetPath, AssetTypes.TilesheetDefinition, "nested/tiles.gts")], true);
        Assert.Equal(1, edits.Import(plan));
        var definition = Bytes(archive, AssetTypes.TilesheetDefinition, "nested/tiles.gts");
        edits.History.Undo(); Assert.Single(archive.GetAllEntries()); Assert.False(edits.History.CanUndo);
        edits.History.Redo(); Assert.Equal(definition, Bytes(archive, AssetTypes.TilesheetDefinition, "nested/tiles.gts"));
        Assert.Equal(new byte[] { 1, 2, 3 }, Bytes(archive, AssetTypes.Image, "art.png"));
        // A fresh archive imports both definition and dependency in the same step.
        using var fresh = Archive("fresh.gaf");
        using var all = new AssetEditSession(fresh, true);
        Assert.Equal(2, all.Import(AssetImportPlan.Create(fresh, [new(sheetPath, AssetTypes.TilesheetDefinition, "tiles.gts")], true)));
        all.History.Undo(); Assert.Empty(fresh.GetAllEntries()); Assert.False(all.History.CanUndo);
        all.History.Redo(); Assert.Equal(2, fresh.GetAllEntries().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementIsByteExactAndSuppressesNoOp(bool encrypted)
    {
        using var archive = Archive(encrypt: encrypted);
        Add(archive, AssetTypes.Audio, "nested/file", [0, 255, 1]);
        Add(archive, AssetTypes.Misc, "nested/file", [7]);
        archive.Save();
        using var edits = new AssetEditSession(archive, true);
        Assert.False(edits.Replace(AssetTypes.Audio, "nested/file", [0, 255, 1])); Assert.False(edits.History.CanUndo);
        byte[] replacement = [4, 3, 2, 1];
        Assert.True(edits.Replace(AssetTypes.Audio, "nested/file", replacement));
        replacement[0] = 99; // caller cannot change retained bytes
        edits.History.Undo(); Assert.Equal(new byte[] { 0, 255, 1 }, Bytes(archive, AssetTypes.Audio, "nested/file"));
        edits.History.Redo(); Assert.Equal(new byte[] { 4, 3, 2, 1 }, Bytes(archive, AssetTypes.Audio, "nested/file"));
        Assert.Equal(new byte[] { 7 }, Bytes(archive, AssetTypes.Misc, "nested/file"));
        archive.Save();
        using var loaded = AssetsFile.LoadOrCreate(archive.SourcePath!, encrypted ? "secret" : null, encrypted, register: false);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, Bytes(loaded, AssetTypes.Audio, "nested/file"));
        Assert.Equal(encrypted, loaded.UseEncryption);
    }

    [Fact]
    public void CancelFailureAndStalePlanLeaveNoPartialCommand()
    {
        using var archive = Archive();
        Add(archive, AssetTypes.Misc, "existing", [1]);
        using var edits = new AssetEditSession(archive, true);
        edits.Replace(AssetTypes.Misc, "existing", [2]);
        var description = edits.History.UndoDescription;
        var plan = Plan(archive, (AssetTypes.Misc, "one", [3]), (AssetTypes.Misc, "two", [4]));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => edits.Import(plan, cancellation: cancelled.Token));
        int writes = 0;
        Assert.Throws<IOException>(() => edits.ImportCore(plan, null, default, (entry, bytes) =>
        {
            Add(archive, entry.AssetType, entry.AssetName, bytes);
            if (++writes == 2) throw new IOException("after partial write");
        }));
        Assert.Single(archive.GetAllEntries()); Assert.Equal(description, edits.History.UndoDescription);
        Assert.Equal(new byte[] { 2 }, Bytes(archive, AssetTypes.Misc, "existing"));
        edits.Replace(AssetTypes.Misc, "existing", [5]);
        Assert.Throws<InvalidOperationException>(() => edits.Import(plan));
        Assert.Single(archive.GetAllEntries());
        var noOp = Plan(archive, (AssetTypes.Misc, "existing", [5]));
        description = edits.History.UndoDescription;
        Assert.Equal(0, edits.Import(noOp)); Assert.Equal(description, edits.History.UndoDescription);
    }

    [Fact]
    public void FailedReplacementRestoresOriginalAndCheckpoint()
    {
        using var archive = Archive(); Add(archive, AssetTypes.Misc, "nested/entry", [1, 2]);
        using var edits = new AssetEditSession(archive, true);
        Assert.Throws<IOException>(() => edits.ReplaceCore(AssetTypes.Misc, "nested/entry", [3, 4], null, (entry, bytes) =>
        {
            Add(archive, entry.AssetType, entry.AssetName, bytes); throw new IOException("after replacement");
        }));
        Assert.Equal(new byte[] { 1, 2 }, Bytes(archive, AssetTypes.Misc, "nested/entry"));
        Assert.False(edits.History.CanUndo); Assert.False(edits.History.IsDirty);
    }

    [Fact]
    public void OversizedConfirmationRechecksAdoptedArchiveAndPreservesOtherDocument()
    {
        using var archive = Archive(); using var adopted = Archive("adopted.gaf");
        using var edits = new AssetEditSession(archive, true, byteLimit: 1);
        var plan = Plan(archive, (AssetTypes.Misc, "large", [1, 2]));
        Assert.Throws<InvalidOperationException>(() => edits.Import(plan, _ =>
        {
            edits.AcceptSavedArchive(adopted); return true;
        }));
        Assert.Empty(archive.GetAllEntries()); Assert.Empty(adopted.GetAllEntries());
        Assert.False(edits.History.CanUndo); Assert.False(edits.History.IsDirty);
    }

    [Fact]
    public void SaveCheckpointsBranchingExternalMutationAndIndependentArchives()
    {
        using var first = Archive(); using var second = Archive("second.gaf");
        using var edits = new AssetEditSession(first, false); using var other = new AssetEditSession(second, true);
        Assert.True(edits.History.IsDirty);
        edits.Import(Plan(first, (AssetTypes.Misc, "entry", [1])));
        first.Save(); edits.AcceptSavedArchive(first);
        edits.Replace(AssetTypes.Misc, "entry", [2]);
        edits.History.Undo(); Assert.False(edits.History.IsDirty);
        edits.History.Undo(); Assert.True(edits.History.IsDirty);
        Assert.Empty(first.GetAllEntries()); Assert.False(other.History.CanUndo);
        edits.History.Redo(); Assert.False(edits.History.IsDirty);
        edits.Replace(AssetTypes.Misc, "entry", [3]); Assert.False(edits.History.CanRedo);
        Add(first, AssetTypes.Misc, "external", [4]); edits.History.MarkUntracked();
        Assert.True(edits.History.IsDirty); Assert.False(edits.History.CanUndo); Assert.False(edits.History.CanRedo);
        edits.AcceptSavedArchive(first, externalRefresh: true); Assert.False(edits.History.IsDirty);
    }

    [Fact]
    public void ByteQuotaPrunesBothDirectionsAndOversizedFallbackRequiresApproval()
    {
        using var archive = Archive();
        using var edits = new AssetEditSession(archive, true, byteLimit: 4);
        edits.Import(Plan(archive, (AssetTypes.Misc, "a", [1, 2, 3])));
        edits.Import(Plan(archive, (AssetTypes.Misc, "b", [4, 5, 6])));
        Assert.Equal(3, edits.History.RetainedBytes);
        edits.History.Undo(); Assert.False(edits.History.CanUndo); Assert.True(edits.History.IsDirty);
        Assert.NotNull(archive.Get(AssetTypes.Misc, "a"));
        var huge = Plan(archive, (AssetTypes.Misc, "large", [0, 1, 2, 3, 4]));
        Assert.Throws<InvalidOperationException>(() => edits.Import(huge));
        Assert.Throws<OperationCanceledException>(() => edits.Import(huge, _ => false));
        Assert.True(edits.History.CanRedo); Assert.Single(archive.GetAllEntries());
        Assert.Equal(1, edits.Import(huge, bytes => { Assert.Equal(5, bytes); return true; }));
        Assert.False(edits.History.CanUndo); Assert.False(edits.History.CanRedo); Assert.True(edits.History.IsDirty);
        Assert.Equal(0, edits.History.RetainedBytes);
        Assert.Throws<OperationCanceledException>(() => edits.Replace(AssetTypes.Misc, "large", [7], _ => false));
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, Bytes(archive, AssetTypes.Misc, "large"));
        Assert.True(edits.Replace(AssetTypes.Misc, "large", [7], _ => true));
        Assert.Equal(0, edits.History.RetainedBytes);
    }

    [Fact]
    public void OversizedFailurePreservesCheckpointAndEarlierHistory()
    {
        using var archive = Archive();
        using var edits = new AssetEditSession(archive, true, byteLimit: 2);
        edits.Import(Plan(archive, (AssetTypes.Misc, "a", [1]))); edits.History.MarkSaved();
        var huge = Plan(archive, (AssetTypes.Misc, "large", [2, 3, 4]));
        Assert.Throws<IOException>(() => edits.ImportCore(huge, _ => true, default, (entry, bytes) =>
        {
            Add(archive, entry.AssetType, entry.AssetName, bytes); throw new IOException("partial");
        }));
        Assert.Single(archive.GetAllEntries()); Assert.True(edits.History.CanUndo); Assert.False(edits.History.IsDirty);
    }

    [Fact]
    public void SaveAsAdoptsCurrentArchiveAndRebasesRetainedNativePayloads()
    {
        var image = Path.Combine(_root, "art.png"); File.WriteAllBytes(image, [1]);
        var sheetPath = Path.Combine(_root, "tiles.gts");
        TilesheetDefinitionSerializer.Save(sheetPath, new TilesheetDefinition { Name = "tiles", Image = new() { FilePath = "art.png" } });
        using var source = Archive(); source.Save();
        using var edits = new AssetEditSession(source, true);
        edits.Import(AssetImportPlan.Create(source, [new(sheetPath, AssetTypes.TilesheetDefinition, "nested/tiles.gts")], false));
        var destination = Path.Combine(_root, "copy", "copy.gaf"); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        AssetArchiveCopy.Save(source, destination, null, false);
        using var adopted = AssetsFile.LoadOrCreate(destination, null, false, register: false);
        edits.AcceptSavedArchive(adopted);
        edits.History.Undo(); Assert.Empty(adopted.GetAllEntries()); Assert.Single(source.GetAllEntries());
        edits.History.Redo(); Assert.False(edits.History.IsDirty);
        var definition = TilesheetDefinitionSerializer.Load(adopted, "nested/tiles.gts");
        Assert.Equal(image, Path.GetFullPath(definition.Image.FilePath!, Path.GetDirectoryName(destination)!));
        Assert.Equal(destination, edits.Archive.SourcePath);
    }

    [Fact]
    public void DisposalAndPruningReleaseRetainedPayloads()
    {
        using var archive = Archive();
        var edits = new AssetEditSession(archive, true);
        var weak = CapturePayload(edits);
        edits.History.Clear();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        edits.Import(Plan(archive, (AssetTypes.Misc, "next", [3])));
        Assert.NotEqual(0, edits.History.RetainedBytes);
        edits.Dispose(); Assert.Equal(0, edits.History.RetainedBytes); Assert.False(edits.History.CanUndo);
        Assert.Throws<ObjectDisposedException>(() => _ = edits.Archive);
        Assert.Equal(2, archive.GetAllEntries().Count()); // borrowed ownership retained
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference CapturePayload(AssetEditSession edits)
    {
        var plan = Plan(edits.Archive, (AssetTypes.Misc, "one", [1, 2]));
        var weak = new WeakReference(plan.Additions.Single().Value);
        edits.Import(plan); return weak;
    }
}
