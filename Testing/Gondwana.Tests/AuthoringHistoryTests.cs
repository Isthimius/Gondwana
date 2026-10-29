using System.Drawing;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tests;

public sealed class AuthoringHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AuthoringHistory_" + Guid.NewGuid().ToString("N"));
    public AuthoringHistoryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ImportsUndoAsOneStepAndRestoreOriginalMemberSelection()
    {
        string gts = Path.Combine(_root, "import.gts"), gscn = Path.Combine(_root, "import.gscn");
        TilesheetDefinitionSerializer.SaveAll(gts, [new() { Name = "a" }, new() { Name = "b" }]);
        SceneDefinitionSerializer.SaveAll(gscn, [new() { ID = "a" }, new() { ID = "b" }]);
        using var tiles = TilesheetDocument.Create(_root);
        var originalTiles = tiles.Definition;
        Assert.Equal(2, tiles.ImportDefinitions(gts));
        var importedTiles = tiles.Definitions.Skip(1).ToArray();
        tiles.History.Undo(); Assert.Same(originalTiles, tiles.Definition); Assert.Single(tiles.Definitions);
        tiles.History.Redo(); Assert.Equal(importedTiles, tiles.Definitions.Skip(1)); Assert.Same(importedTiles[0], tiles.Definition);
        using var scene = SceneDocument.Create(_root);
        var originalScene = scene.Definition;
        Assert.Equal(2, scene.ImportDefinitions(gscn));
        var importedScenes = scene.Definitions.Skip(1).ToArray();
        scene.History.Undo(); Assert.Same(originalScene, scene.Definition); Assert.Single(scene.Definitions);
        scene.History.Redo(); Assert.Equal(importedScenes, scene.Definitions.Skip(1)); Assert.Same(importedScenes[0], scene.Definition);
    }

    [Fact]
    public void GtsPropertiesBoxedGeometryAndSparseFramesAreReversible()
    {
        using var document = TilesheetDocument.Create(_root);
        var member = document.Definition;
        var region = new TilesheetRegionDefinition { Name = "region", Area = new(3, 4, 32, 32), TileSize = new(16, 16) };
        document.AddRegion(region);
        document.Save(Path.Combine(_root, "tiles.gts"), allowInvalid: true);
        var fields = new PropertyFields(document);
        fields.AddModel(region, document.MarkChanged, "Frames");
        fields.GetProperties()["Area"]!.SetValue(fields, new Size(64, 48));
        Assert.Equal(new Rectangle(3, 4, 64, 48), region.Area);
        document.History.Undo(); Assert.Equal(new Rectangle(3, 4, 32, 32), region.Area); Assert.False(document.IsDirty);
        document.History.Redo(); Assert.Equal(64, region.Area.Width);
        var frameFields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        frameFields.GetProperties()["CollisionType"]!.SetValue(frameFields, FrameCollisionChoice.Blocking);
        var frame = Assert.Single(region.Frames);
        document.History.Undo(); Assert.Empty(region.Frames);
        document.History.Redo(); Assert.Same(frame, Assert.Single(region.Frames));
        Assert.Same(member, document.Definition);
        Assert.False(document.EditFrame(region, 0, 0, "Same", f => f.CollisionType = TileCollisionType.Blocking));
        Assert.Equal("Change frame collision type", document.History.UndoDescription);
    }

    [Fact]
    public void GtsRegionsPruningAndMemberOrderingRetainInstances()
    {
        using var d = TilesheetDocument.Create(_root);
        var first = d.Definition;
        var region = new TilesheetRegionDefinition { Name = "region", Area = new(0, 0, 16, 16), TileSize = new(16, 16) };
        d.AddRegion(region);
        d.EditFrame(region, 2, 0, "Collision", f => f.CollisionType = TileCollisionType.Blocking);
        var frame = region.Frames.Single();
        Assert.Equal(1, d.RemoveOutOfGridMetadata(region)); d.History.Undo(); Assert.Same(frame, region.Frames.Single());
        d.RemoveRegion(region); d.History.Undo(); Assert.Same(region, first.Regions.Single());
        var copy = d.DuplicateDefinition(); d.RenameDefinition("copy");
        d.RemoveDefinition(copy); Assert.Same(first, d.Definition);
        d.History.Undo(); Assert.Same(copy, d.Definition); Assert.Equal(new[] { first, copy }, d.Definitions);
        d.History.Undo(); Assert.NotEqual("copy", copy.Name);
        d.History.Undo(); Assert.Same(first, d.Definition);
        d.History.Redo(); Assert.Same(copy, d.Definition);
    }

    [Fact]
    public void GtsSaveAsRebasesDormantImageReferencesWithoutUndoingLocation()
    {
        using var d = TilesheetDocument.Create(_root);
        d.SetImage("old.png");
        var original = d.Definition.Image;
        d.Save(Path.Combine(_root, "a.gts"), allowInvalid: true);
        d.SetImage("new.png");
        string saved = Path.Combine(_root, "sub", "b.gts");
        d.Save(saved, allowInvalid: true);
        d.History.Undo(); Assert.Same(original, d.Definition.Image);
        Assert.Equal(Path.Combine(_root, "old.png"), d.ResolveImagePath());
        Assert.Equal(saved, d.FilePath); Assert.True(d.IsDirty);
        d.History.Redo(); Assert.False(d.IsDirty); Assert.Equal(Path.Combine(_root, "new.png"), d.ResolveImagePath());
    }

    [Fact]
    public void SceneSparseTileEditsPreserveUnrelatedMetadataAndGroupHundredsOfCells()
    {
        using var d = SceneDocument.Create(_root);
        var layer = d.AddLayer(); layer.Columns = 30;
        d.Save(Path.Combine(_root, "scene.gscn"));
        int changes = 0; d.Changed += (_, _) => changes++;
        using (var group = d.History.BeginTransaction("Many tiles"))
        {
            for (int x = 0; x < 30; x++) for (int y = 0; y < 10; y++)
                    d.EditTile(layer, x, y, "Assign", tile => { tile.AnimationKey = "walk"; tile.EnableAnimator = true; tile.Transform = TileTransform.Rotate90; });
            group.Commit();
        }
        Assert.Equal(1, changes); Assert.Equal(300, layer.Tiles.Count);
        d.History.Undo(); Assert.Empty(layer.Tiles); Assert.False(d.IsDirty);
        d.History.Redo(); var original = d.FindTile(layer, 0, 0)!;
        d.EditTile(layer, 0, 0, "Assign frame", tile => tile.Frame = new() { Tilesheet = "terrain", XTile = 3 });
        Assert.Equal("walk", original.AnimationKey); Assert.Equal(TileTransform.Rotate90, original.Transform);
        d.History.Undo(); Assert.Null(original.Frame); Assert.Equal("walk", original.AnimationKey);
        d.RemoveTile(layer, 0, 0); d.History.Undo(); Assert.Same(original, d.FindTile(layer, 0, 0));
        Assert.False(d.EditTile(layer, 0, 0, "No-op", tile => tile.AnimationKey = "walk"));
    }

    [Fact]
    public void SceneLayerAndMemberOperationsRetainOrderAndSelection()
    {
        using var d = SceneDocument.Create(_root);
        var scene = d.Definition;
        var a = d.AddLayer(); var b = d.AddLayer();
        d.Edit(b, "Layer order", () => b.ZOrder = -1); d.History.Undo(); Assert.Equal(1, b.ZOrder);
        d.RemoveLayer(a); d.History.Undo(); Assert.Equal(new[] { a, b }, scene.Layers);
        var copy = d.DuplicateDefinition(); d.RenameDefinition("second");
        d.RemoveDefinition(copy); d.History.Undo(); Assert.Same(copy, d.Definition);
        d.History.Undo(); d.History.Undo(); Assert.Same(scene, d.Definition);
        d.History.Redo(); Assert.Same(copy, d.Definition);
    }

    [Fact]
    public void SceneReferenceReplacementAndCollectionEditsSurviveSaveAs()
    {
        using var d = SceneDocument.Create(_root);
        d.SetLooseTilesheetSource("terrain", Path.Combine(_root, "old.gts"));
        var original = d.Definition.TilesheetSources.Single();
        d.Save(Path.Combine(_root, "a.gscn"));
        d.SetLooseTilesheetSource("terrain", Path.Combine(_root, "new.gts"));
        d.Save(Path.Combine(_root, "sub", "b.gscn"));
        d.History.Undo(); Assert.Same(original, d.Definition.TilesheetSources.Single());
        Assert.Equal(Path.Combine(_root, "old.gts"), d.ResolveReferencePath(original.GtsPath!));
        d.History.Redo(); Assert.False(d.IsDirty);
        var source = d.Definition.TilesheetSources.Single();
        var snapshot = new ModelSnapshot(d.Definition.TilesheetSources);
        source.GtsPath = "../other.gts";
        d.RecordCollectionEdit(snapshot, "Change source in collection");
        d.Save(Path.Combine(_root, "another", "c.gscn"));
        d.History.Undo(); Assert.Equal(Path.Combine(_root, "new.gts"), d.ResolveReferencePath(source.GtsPath!));
        d.History.Redo(); Assert.Equal(Path.Combine(_root, "other.gts"), d.ResolveReferencePath(source.GtsPath!));
    }

    [Fact]
    public void SaveFailuresAndLegacyMutationsCannotAdvanceCheckpoint()
    {
        using var d = SceneDocument.Create(_root);
        Assert.True(d.IsDirty);
        d.Save(Path.Combine(_root, "scene.gscn")); Assert.False(d.IsDirty);
        var layer = d.AddLayer();
        Assert.ThrowsAny<Exception>(() => d.Save(_root)); Assert.True(d.IsDirty);
        d.History.Undo(); Assert.False(d.IsDirty); d.History.Redo();
        d.Save(Path.Combine(_root, "scene.gscn"));
        d.Edit(layer, "Width", () => layer.Columns = 20);
        d.History.Undo(); Assert.False(d.IsDirty);
        d.History.Undo(); Assert.True(d.IsDirty);
        d.Definition.ID = "untracked"; d.MarkChanged();
        Assert.False(d.History.CanUndo); Assert.False(d.History.CanRedo); Assert.True(d.IsDirty);
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gscn")]
    public void PackedCheckpointsAndPackedToLooseSaveAsRetainHistory(string format)
    {
        string archivePath = Path.Combine(_root, "content.gaf");
        using var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        string json = format == "gts" ? TilesheetDefinitionSerializer.ToJson(new() { Name = "tiles", Image = new() { AssetEntryName = "images/tile.png" } })
            : SceneDefinitionSerializer.ToJson(new SceneDefinition { ID = "scene" });
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(json));
        archive.Add(format == "gts" ? AssetTypes.TilesheetDefinition : AssetTypes.SceneDefinition, "nested/document." + format, bytes); archive.Save();
        if (format == "gts")
        {
            using var d = TilesheetDocument.Open(archive, "nested/document.gts");
            Assert.False(d.IsDirty); d.RenameDefinition("new"); d.AcceptPackedSave();
            d.History.Undo(); Assert.True(d.IsDirty); d.History.Redo(); Assert.False(d.IsDirty);
            d.SetImage("other.png"); d.Save(Path.Combine(_root, "sub", "loose.gts"), allowInvalid: true);
            d.History.Undo(); Assert.Equal(archivePath, d.ResolveAssetsFilePath()); Assert.Null(d.PackedLocation);
        }
        else
        {
            using var d = SceneDocument.Open(archive, "nested/document.gscn");
            Assert.False(d.IsDirty); d.RenameDefinition("new"); d.AcceptPackedSave();
            d.History.Undo(); Assert.True(d.IsDirty); d.History.Redo(); Assert.False(d.IsDirty);
            d.Save(Path.Combine(_root, "sub", "loose.gscn"));
            d.History.Undo(); Assert.Equal("scene", d.Definition.ID); Assert.Null(d.PackedLocation); Assert.True(d.IsDirty);
        }
    }
}
