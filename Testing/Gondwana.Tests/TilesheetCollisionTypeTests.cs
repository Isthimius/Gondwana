using System.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("SpriteManager")]
public sealed class TilesheetCollisionTypeTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"GondwanaCollisionTypeTests_{Guid.NewGuid():N}");

    public TilesheetCollisionTypeTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RegionCollisionType_UpdatesInheritedFramesAndPreservesOverrides()
    {
        using var tilesheet = CreateRuntimeTilesheet();
        var region = tilesheet.DefaultRegion;
        region.CollisionType = TileCollisionType.Blocking;

        var inheritedFrame = tilesheet.GetFrame(0, 0);
        var triggerFrame = tilesheet.GetFrame(1, 0);
        var explicitBlockingFrame = tilesheet.GetFrame(2, 0);

        triggerFrame.CollisionType = TileCollisionType.Trigger;
        explicitBlockingFrame.CollisionType = TileCollisionType.Blocking;

        Assert.False(inheritedFrame.HasCollisionTypeOverride);
        Assert.True(triggerFrame.HasCollisionTypeOverride);
        Assert.True(explicitBlockingFrame.HasCollisionTypeOverride);

        region.CollisionType = TileCollisionType.None;

        Assert.Equal(TileCollisionType.None, inheritedFrame.CollisionType);
        Assert.Equal(TileCollisionType.Trigger, triggerFrame.CollisionType);
        Assert.Equal(TileCollisionType.Blocking, explicitBlockingFrame.CollisionType);

        Assert.True(triggerFrame.ClearCollisionTypeOverride());
        Assert.Equal(TileCollisionType.None, triggerFrame.CollisionType);
    }

    [Fact]
    public void TileCollisionType_CanRemainStaticOrFollowFrames()
    {
        using var tilesheet = CreateRuntimeTilesheet();
        var region = tilesheet.DefaultRegion;
        region.CollisionType = TileCollisionType.Blocking;
        region.SetFrameCollisionType(1, 0, TileCollisionType.Trigger);
        region.SetFrameCollisionType(2, 0, TileCollisionType.None);

        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var sprite = SpriteManager.Instance.CreateSprite(
            layer,
            tilesheet.GetFrame(0, 0),
            collisionProfileName: CollisionProfileNames.Actor);

        try
        {
            var collider = Assert.IsAssignableFrom<ICollider>(sprite.Collider);

            Assert.Equal(TileCollisionType.Blocking, sprite.CollisionType);
            Assert.True(sprite.CollisionsEnabled);
            Assert.Equal(CollisionResponseType.Solid, collider.ResponseType);
            Assert.Equal(scene.CollisionGroups.Actors, collider.CollisionGroup);
            Assert.Contains(collider, layer.ColliderRegistry.DynamicColliders);

            sprite.CurrentFrame = tilesheet.GetFrame(1, 0);
            Assert.Equal(TileCollisionType.Blocking, sprite.CollisionType);

            sprite.CollisionTypeByFrame = true;
            Assert.Equal(TileCollisionType.Trigger, sprite.CollisionType);
            Assert.Equal(CollisionResponseType.Trigger, collider.ResponseType);

            sprite.CurrentFrame = tilesheet.GetFrame(2, 0);
            Assert.Equal(TileCollisionType.None, sprite.CollisionType);
            Assert.False(sprite.CollisionsEnabled);
            Assert.DoesNotContain(collider, layer.ColliderRegistry.DynamicColliders);
        }
        finally
        {
            if (SpriteManager.Instance._spriteList.Remove(sprite))
                sprite.DisposeImmediate();
        }
    }

    [Fact]
    public void Gts_RoundTripPreservesInheritedAndExplicitCollisionTypes()
    {
        var imagePath = WriteImage("collision-types.png");
        var definition = new TilesheetDefinition
        {
            Name = "CollisionTypes",
            Image = new TilesheetImageDefinition { FilePath = imagePath },
            Regions =
            [
                new TilesheetRegionDefinition
                {
                    Name = TilesheetRegion.DefaultRegionName,
                    Area = new Rectangle(0, 0, 48, 16),
                    TileSize = new Size(16, 16),
                    CollisionType = TileCollisionType.Blocking,
                    Frames =
                    [
                        new TilesheetFrameDefinition { XTile = 0, YTile = 0 },
                        new TilesheetFrameDefinition
                        {
                            XTile = 1,
                            YTile = 0,
                            CollisionType = TileCollisionType.None
                        },
                        new TilesheetFrameDefinition
                        {
                            XTile = 2,
                            YTile = 0,
                            CollisionType = TileCollisionType.Blocking
                        }
                    ]
                }
            ]
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);
        var jsonRoundTrip = TilesheetDefinitionSerializer.FromJson(json);
        var jsonRegion = Assert.Single(jsonRoundTrip.Regions);

        Assert.Equal(TileCollisionType.Blocking, jsonRegion.CollisionType);
        Assert.Null(jsonRegion.Frames[0].CollisionType);
        Assert.Equal(
            TileCollisionType.None,
            jsonRegion.Frames[1].CollisionType.GetValueOrDefault());
        Assert.Equal(
            TileCollisionType.Blocking,
            jsonRegion.Frames[2].CollisionType.GetValueOrDefault());

        using var tilesheet = TilesheetFactory.FromDefinition(jsonRoundTrip);
        var runtimeRegion = tilesheet.DefaultRegion;

        Assert.False(runtimeRegion.TryGetFrameCollisionTypeOverride(0, 0, out _));
        Assert.True(runtimeRegion.TryGetFrameCollisionTypeOverride(1, 0, out var noneOverride));
        Assert.Equal(TileCollisionType.None, noneOverride);
        Assert.True(runtimeRegion.TryGetFrameCollisionTypeOverride(2, 0, out var equalOverride));
        Assert.Equal(TileCollisionType.Blocking, equalOverride);

        var serialized = TilesheetDefinitionSerializer.FromTilesheet(tilesheet);
        var serializedRegion = Assert.Single(serialized.Regions);

        Assert.Null(serializedRegion.Frames[0].CollisionType);
        Assert.Equal(
            TileCollisionType.None,
            serializedRegion.Frames[1].CollisionType.GetValueOrDefault());
        Assert.Equal(
            TileCollisionType.Blocking,
            serializedRegion.Frames[2].CollisionType.GetValueOrDefault());
    }

    private static Tilesheet CreateRuntimeTilesheet()
    {
        var bitmap = new SKBitmap(48, 16);
        var tilesheet = TilesheetFactory.FromBitmap("CollisionTypes", bitmap);
        tilesheet.DefaultRegion.TileSize = new Size(16, 16);
        return tilesheet;
    }

    private string WriteImage(string fileName)
    {
        var imagePath = Path.Combine(_tempDir, fileName);
        using var bitmap = new SKBitmap(48, 16);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(imagePath, data.ToArray());
        return imagePath;
    }
}
