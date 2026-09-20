using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies the GSCN scene-definition model and runtime parity.
/// </summary>
[Collection("Global engine state")]
public sealed class SceneGscnTests
{
    [Fact]
    public void RuntimeScene_RoundTripsPersistentGraphThroughDefinition()
    {
        var sheetName = $"GSCN_{Guid.NewGuid():N}";
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            sheetName,
            new SKBitmap(32, 16));

        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var original = new Scene();
        original.CollisionGroups.Define("Enemies");
        original.CollisionProfiles.Define(
            "EnemySensor",
            "Enemies",
            ["Actors"]);

        var layer = original.AddLayer(
            2,
            2,
            16,
            16,
            zOrder: 7,
            parallax: 0.5f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        layer.WrapHorizontally = true;
        layer.ShowGridLines = true;
        layer.ShowCollisionBoxes = true;
        layer.OriginPx = new Point(11, 13);
        layer.DefaultTileCollisionProfile = CollisionProfileNames.Sensor;

        var tile = layer[1, 0]!;
        tile.Nickname = "door";
        tile.Visible = false;
        tile.CurrentFrame = sheet.GetFrame(1, 0);
        tile.EnableFog = true;
        tile.AdjustCollisionArea = new CollisionAdjust(1, 2, 3, 4);
        tile.CollisionType = TileCollisionType.Trigger;
        tile.SetCollisionProfile("EnemySensor");
        tile.EnableAnimator = true;

        var definition = SceneDefinitionSerializer.FromScene(original);
        var json = SceneDefinitionSerializer.ToJson(definition);
        var jsonDefinition = SceneDefinitionSerializer.FromJson(json);

        Assert.Equal(SceneDefinitionSourceKind.Generated, definition.Source.Kind);
        Assert.DoesNotContain("$id", json);
        Assert.DoesNotContain("$ref", json);
        Assert.DoesNotContain("parentSceneLayer", json);
        Assert.DoesNotContain("SceneLayerTileArray", json);
        Assert.DoesNotContain("\"CollisionsEnabled\"", json);

        using var restored = SceneDefinitionSerializer.ToScene(jsonDefinition);

        Assert.Equal(original.ID, restored.ID);
        var restoredLayer = Assert.Single(restored.SceneLayers);
        Assert.Equal(layer.ID, restoredLayer.ID);
        Assert.Same(restored, restoredLayer.Scene);
        Assert.Equal(2, restoredLayer.GridColumnCount);
        Assert.Equal(2, restoredLayer.GridRowCount);
        Assert.Equal(7, restoredLayer.ZOrder);
        Assert.Equal(0.5f, restoredLayer.Parallax);
        Assert.True(restoredLayer.WrapHorizontally);
        Assert.True(restoredLayer.ShowGridLines);
        Assert.True(restoredLayer.ShowCollisionBoxes);
        Assert.Equal(new Point(11, 13), restoredLayer.OriginPx);
        Assert.Equal(CollisionProfileNames.Sensor, restoredLayer.DefaultTileCollisionProfile);

        Assert.Equal(
            original.CollisionGroups.Get("Enemies"),
            restored.CollisionGroups.Get("Enemies"));

        var restoredProfile = restored.CollisionProfiles.Get("EnemySensor");
        Assert.Equal("Enemies", restoredProfile.CollisionGroup);
        Assert.Equal(["Actors"], restoredProfile.CollidesWith);

        var restoredTile = restoredLayer[1, 0]!;
        Assert.Equal(tile.Id, restoredTile.Id);
        Assert.Same(restoredLayer, restoredTile.SceneLayer);
        Assert.Equal("door", restoredTile.Nickname);
        Assert.False(restoredTile.Visible);
        Assert.Equal(sheetName, restoredTile.CurrentFrame.Tilesheet.Name);
        Assert.Equal(1, restoredTile.CurrentFrame.XTile);
        Assert.Equal(0, restoredTile.CurrentFrame.YTile);
        Assert.True(restoredTile.EnableFog);
        Assert.Equal(new CollisionAdjust(1, 2, 3, 4), restoredTile.AdjustCollisionArea);
        Assert.Equal(TileCollisionType.Trigger, restoredTile.CollisionType);
        Assert.Equal("EnemySensor", restoredTile.CollisionProfileName);
        Assert.True(restoredTile.CollisionsEnabled);
        Assert.True(restoredTile.EnableAnimator);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeScene_RoundTripsGaniAssignmentWithoutEmbeddingCycleGraph(bool startAnimation)
    {
        var sheetName = $"GSCN_Animation_{Guid.NewGuid():N}";
        var animationKey = $"actor.walk.{Guid.NewGuid():N}";

        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            sheetName,
            new SKBitmap(32, 16));

        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var cycle = new Cycle(
            new FrameSequence(
                [
                    sheet.GetFrame(0, 0),
                    sheet.GetFrame(1, 0)
                ])
            {
                SequenceCycleType = CycleType.Repeating
            },
            0.1,
            animationKey);

        using var original = new Scene();
        var tile = original.AddLayer(1, 1, 16, 16)[0, 0]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);
        tile.EnableAnimator = true;

        if (startAnimation)
            tile.TileAnimator.StartAnimation(animationKey);
        else
            tile.TileAnimator.SetCurrentCycle(animationKey);

        var definition = SceneDefinitionSerializer.FromScene(original);
        var tileDefinition = Assert.Single(Assert.Single(definition.Layers).Tiles);

        Assert.True(tileDefinition.EnableAnimator);
        Assert.Equal(animationKey, tileDefinition.AnimationKey);
        Assert.Equal(startAnimation, tileDefinition.StartAnimation);

        var json = SceneDefinitionSerializer.ToJson(definition);
        Assert.Contains($"\"AnimationKey\": \"{animationKey}\"", json);
        Assert.DoesNotContain("\"CurrentCycle\"", json);
        Assert.DoesNotContain("\"Sequence\"", json);

        using var restored = SceneDefinitionSerializer.ToScene(
            SceneDefinitionSerializer.FromJson(json));

        var restoredTile = Assert.Single(restored.SceneLayers)[0, 0]!;
        Assert.True(restoredTile.EnableAnimator);
        Assert.NotNull(restoredTile.TileAnimator.CurrentCycle);
        Assert.Equal(
            animationKey,
            restoredTile.TileAnimator.CurrentCycle.CycleKey);
        Assert.Equal(
            startAnimation,
            restoredTile.TileAnimator.IsCycling);
    }

    [Fact]
    public void Definition_StartAnimationRequiresAnimationKey()
    {
        var definition = new SceneDefinition
        {
            Layers =
            [
                new SceneLayerDefinition
                {
                    Columns = 1,
                    Rows = 1,
                    Tiles =
                    [
                        new SceneLayerTileDefinition
                        {
                            X = 0,
                            Y = 0,
                            StartAnimation = true
                        }
                    ]
                }
            ]
        };

        var errors = SceneDefinitionValidator.Validate(definition);

        Assert.Contains(
            errors,
            error => error.Contains(
                "StartAnimation requires an AnimationKey",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Definition_AnimationKeyMustResolveWhenSceneIsMaterialized()
    {
        var key = $"missing.animation.{Guid.NewGuid():N}";

        var definition = new SceneDefinition
        {
            Layers =
            [
                new SceneLayerDefinition
                {
                    Columns = 1,
                    Rows = 1,
                    Tiles =
                    [
                        new SceneLayerTileDefinition
                        {
                            X = 0,
                            Y = 0,
                            AnimationKey = key
                        }
                    ]
                }
            ]
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => SceneDefinitionSerializer.ToScene(definition));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "no matching GANI/cycle is registered",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_ByFrameFlagsOverrideOmittedExplicitCollisionDefaults()
    {
        var sheetName = $"GSCN_ByFrame_{Guid.NewGuid():N}";
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            sheetName,
            new SKBitmap(16, 16));

        sheet.DefaultRegion.TileSize = new Size(16, 16);
        var frameAdjust = new CollisionAdjust(1, 2, 3, 4);
        sheet.DefaultRegion.CollisionAdjust = frameAdjust;
        sheet.DefaultRegion.CollisionType = TileCollisionType.Trigger;

        var definition = new SceneDefinition
        {
            Layers =
            [
                new SceneLayerDefinition
                {
                    Columns = 1,
                    Rows = 1,
                    TileWidth = 16,
                    TileHeight = 16,
                    Tiles =
                    [
                        new SceneLayerTileDefinition
                        {
                            X = 0,
                            Y = 0,
                            Frame = new SceneFrameDefinition
                            {
                                Tilesheet = sheetName,
                                RegionName = TilesheetRegion.DefaultRegionName,
                                XTile = 0,
                                YTile = 0
                            },
                            AdjustCollisionAreaByFrame = true,
                            CollisionTypeByFrame = true
                        }
                    ]
                }
            ]
        };

        using var scene = SceneDefinitionSerializer.ToScene(definition);
        var tile = Assert.Single(scene.SceneLayers)[0, 0]!;

        Assert.Equal(frameAdjust, tile.AdjustCollisionArea);
        Assert.Equal(TileCollisionType.Trigger, tile.CollisionType);
        Assert.True(tile.CollisionsEnabled);
    }

    [Fact]
    public void Definition_AllowsSparseTilesAndMaterializesDefaultsForOmittedCells()
    {
        var definition = new SceneDefinition
        {
            Layers =
            [
                new SceneLayerDefinition
                {
                    Columns = 2,
                    Rows = 2,
                    Tiles =
                    [
                        new SceneLayerTileDefinition
                        {
                            X = 1,
                            Y = 1,
                            Nickname = "only explicit tile",
                            Visible = false
                        }
                    ]
                }
            ]
        };

        using var scene = SceneDefinitionSerializer.ToScene(definition);
        var layer = Assert.Single(scene.SceneLayers);

        Assert.Null(layer[0, 0]!.Nickname);
        Assert.True(layer[0, 0]!.Visible);
        Assert.Equal("only explicit tile", layer[1, 1]!.Nickname);
        Assert.False(layer[1, 1]!.Visible);
    }

    [Fact]
    public void SaveAndLoad_StampLooseDefinitionSourceWithoutMutatingOriginal()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"GondwanaGscn_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "scene.gscn");
            var definition = new SceneDefinition();

            SceneDefinitionSerializer.Save(path, definition);
            var loaded = SceneDefinitionSerializer.Load(path);

            Assert.Equal(SceneDefinitionSourceKind.None, definition.Source.Kind);
            Assert.Equal(SceneDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
            Assert.Equal(Path.GetFullPath(path), loaded.Source.GscnFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validator_ReportsDuplicateAndOutOfRangeTileEntries()
    {
        var definition = new SceneDefinition
        {
            Layers =
            [
                new SceneLayerDefinition
                {
                    Columns = 1,
                    Rows = 1,
                    Tiles =
                    [
                        new SceneLayerTileDefinition { X = 0, Y = 0 },
                        new SceneLayerTileDefinition { X = 0, Y = 0 },
                        new SceneLayerTileDefinition { X = 2, Y = 0 }
                    ]
                }
            ]
        };

        var errors = SceneDefinitionValidator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("duplicate tile entry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("outside the layer grid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JsonModel_UsesNamedListsRatherThanRuntimeReferenceGraph()
    {
        using var scene = new Scene();
        scene.AddLayer(1, 1);

        var json = SceneDefinitionSerializer.ToJson(scene);
        var root = JObject.Parse(json);

        Assert.NotNull(root["Layers"]);
        Assert.NotNull(root["CollisionGroups"]);
        Assert.NotNull(root["CollisionProfiles"]);
        Assert.Null(root["_sceneLayers"]);
    }
}
