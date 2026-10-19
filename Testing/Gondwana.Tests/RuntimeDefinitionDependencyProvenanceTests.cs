using System.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Ensures runtime-to-definition capture carries only valid, known physical dependency sources.
/// </summary>
[Collection("Global engine state")]
public sealed class RuntimeDefinitionDependencyProvenanceTests
{
    [Fact]
    public void FromScene_CapturesDistinctLooseAndPackedSourcesAndRebasesOnSave()
    {
        using var looseSheet = NewSheet();
        using var packedSheet = NewSheet();
        var root = Path.Combine(Path.GetTempPath(), $"GondwanaSources_{Guid.NewGuid():N}");
        var looseGts = Path.Combine(root, "tiles", "loose.gts");
        var archive = Path.Combine(root, "assets", "world.gaf");
        var gani = Path.Combine(root, "animations", "walk.gani");
        var output = Path.Combine(root, "scenes", "world.gscn");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            looseSheet.DefinitionProvenance = CaptureSheet(looseSheet, looseGts);
            packedSheet.DefinitionProvenance = CaptureSheet(packedSheet, archive: archive, entry: "packed-tiles");

            var key = $"walk.{Guid.NewGuid():N}";
            using var cycle = new Cycle(new FrameSequence([looseSheet.GetFrame(0, 0)]), 0.1, key);
            cycle.DefinitionProvenance = DefinitionProvenance.Capture(
                gani, null, null, () => AnimationDefinitionSerializer.FromCycle(cycle), key);

            using var scene = new Scene();
            var layer = scene.AddLayer(3, 1, 16, 16);
            layer[0, 0]!.CurrentFrame = looseSheet.GetFrame(0, 0);
            layer[1, 0]!.CurrentFrame = packedSheet.GetFrame(0, 0);
            layer[2, 0]!.CurrentFrame = looseSheet.GetFrame(0, 0);
            layer[0, 0]!.EnableAnimator = true;
            layer[0, 0]!.TileAnimator.SetCurrentCycle(key);

            var projected = SceneDefinitionSerializer.FromScene(scene);
            Assert.Equal(2, projected.TilesheetSources.Count);
            Assert.Equal(looseGts, Assert.Single(projected.TilesheetSources, s => s.Tilesheet == looseSheet.Name).GtsPath);
            var packed = Assert.Single(projected.TilesheetSources, s => s.Tilesheet == packedSheet.Name);
            Assert.Equal(SceneTilesheetSourceKind.PackedDefinitionFile, packed.Kind);
            Assert.Equal(archive, packed.AssetsFilePath);
            Assert.Equal("packed-tiles", packed.AssetEntryName);
            Assert.Equal(gani, Assert.Single(projected.AnimationSources).GaniPath);

            SceneDefinitionSerializer.Save(output, scene);
            var saved = SceneDefinitionSerializer.Load(output);
            var outputDirectory = Path.GetDirectoryName(output)!;
            Assert.Equal(Path.GetRelativePath(outputDirectory, looseGts),
                Assert.Single(saved.TilesheetSources, s => s.Tilesheet == looseSheet.Name).GtsPath);
            Assert.Equal(Path.GetRelativePath(outputDirectory, archive),
                Assert.Single(saved.TilesheetSources, s => s.Tilesheet == packedSheet.Name).AssetsFilePath);
            Assert.Equal(Path.GetRelativePath(outputDirectory, gani),
                Assert.Single(saved.AnimationSources).GaniPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FromScene_OmitsUnknownOrChangedSources()
    {
        using var modifiedSheet = NewSheet();
        using var generatedSheet = NewSheet();
        var gts = Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}.gts");
        modifiedSheet.DefinitionProvenance = CaptureSheet(modifiedSheet, gts);
        modifiedSheet.DefaultRegion.TileSize = new Size(8, 8);

        using var scene = new Scene();
        var layer = scene.AddLayer(2, 1, 16, 16);
        layer[0, 0]!.CurrentFrame = modifiedSheet.GetFrame(0, 0);
        layer[1, 0]!.CurrentFrame = generatedSheet.GetFrame(0, 0);

        var projected = SceneDefinitionSerializer.FromScene(scene);
        Assert.Empty(projected.TilesheetSources);
        Assert.Equal(2, projected.Layers.Single().Tiles.Count(t => t.Frame is not null));
    }

    [Fact]
    public void FromCycle_RecordsValidTilesheetSourceAndDropsStaleSource()
    {
        using var sheet = NewSheet();
        var archive = Path.Combine(Path.GetTempPath(), $"cycles-{Guid.NewGuid():N}.gaf");
        sheet.DefinitionProvenance = CaptureSheet(sheet, archive: archive, entry: "terrain");

        using var cycle = new Cycle(
            new FrameSequence([sheet.GetFrame(0, 0), sheet.GetFrame(1, 0)]),
            0.1, $"cycle.{Guid.NewGuid():N}");
        var source = Assert.Single(AnimationDefinitionSerializer.FromCycle(cycle).TilesheetSources);
        Assert.Equal(AnimationTilesheetSourceKind.PackedDefinitionFile, source.Kind);
        Assert.Equal(archive, source.AssetsFilePath);
        Assert.Equal("terrain", source.AssetEntryName);

        sheet.DefaultRegion.TileSize = new Size(8, 8);
        Assert.Empty(AnimationDefinitionSerializer.FromCycle(cycle).TilesheetSources);
    }

    [Fact]
    public void FromSprites_CapturesSceneTilesheetAndAnimationSources()
    {
        using var sheet = NewSheet();
        var baseDirectory = Path.Combine(Path.GetTempPath(), $"spr-{Guid.NewGuid():N}");
        var gts = Path.Combine(baseDirectory, "terrain.gts");
        var gscn = Path.Combine(baseDirectory, "scene.gscn");
        var ganiArchive = Path.Combine(baseDirectory, "cycles.gaf");
        sheet.DefinitionProvenance = CaptureSheet(sheet, gts);

        var key = $"sprite.run.{Guid.NewGuid():N}";
        using var cycle = new Cycle(new FrameSequence([sheet.GetFrame(0, 0)]), 0.1, key);
        cycle.DefinitionProvenance = DefinitionProvenance.Capture(
            null, ganiArchive, "movement", () => AnimationDefinitionSerializer.FromCycle(cycle), key);

        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        scene.DefinitionProvenance = DefinitionProvenance.Capture(
            gscn, null, null, () => SceneDefinitionSerializer.FromScene(scene), scene.ID);

        var sprite = SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame(0, 0), $"sprite-{Guid.NewGuid():N}");
        try
        {
            sprite.TileAnimator.SetCurrentCycle(key);
            var definition = SpriteDefinitionSerializer.FromSprites([sprite]);
            Assert.Equal(gts, Assert.Single(definition.TilesheetSources).GtsPath);
            Assert.Equal(gscn, Assert.Single(definition.SceneSources).GscnPath);
            var animation = Assert.Single(definition.AnimationSources);
            Assert.Equal(SpriteAnimationSourceKind.PackedDefinitionFile, animation.Kind);
            Assert.Equal(ganiArchive, animation.AssetsFilePath);
            Assert.Equal("movement", animation.AssetEntryName);
        }
        finally
        {
            SpriteManager.Instance._spriteList.Remove(sprite);
            sprite.DisposeImmediate();
        }
    }

    private static Tilesheet NewSheet()
    {
        var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            $"source.{Guid.NewGuid():N}", new SKBitmap(32, 16));
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        return sheet;
    }

    private static DefinitionProvenance CaptureSheet(
        Tilesheet sheet, string? loose = null, string? archive = null, string? entry = null) =>
        DefinitionProvenance.Capture(loose, archive, entry,
            () => TilesheetDefinitionSerializer.FromTilesheet(sheet), sheet.Name)!;
}
