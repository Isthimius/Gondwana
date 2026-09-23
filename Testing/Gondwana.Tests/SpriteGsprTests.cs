using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class SpriteGsprTests
{
    [Fact]
    public void CollectionRoundTripPreservesStateAndCanonicalDependencies()
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap($"gspr-{Guid.NewGuid()}", new SKBitmap(32, 16));
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);
        var original = SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame(1, 0), "player");
        original.SetPosition(new Vector2(1.25f, -2.75f));
        original.RenderSize = new Size(27, 39);
        original.Rotation = 17;
        original.NudgeX = 3;
        var definition = SpriteDefinitionSerializer.FromSprites([original]);
        definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose(sheet.Name, "does-not-exist.gts"));
        definition.SceneSources.Add(SpriteSceneSourceDefinition.Loose(scene.ID, "does-not-exist.gscn"));
        var json = SpriteDefinitionSerializer.ToJson(definition);
        Assert.DoesNotContain("$id", json);
        Assert.DoesNotContain("Animator", json);
        var count = SpriteManager.Instance.AllSprites.Count;
        var loaded = SpriteDefinitionSerializer.FromJson(json);
        Assert.Equal(count, SpriteManager.Instance.AllSprites.Count);
        var restored = Assert.Single(SpriteDefinitionSerializer.ToSprites(loaded));
        try
        {
            Assert.Same(layer, restored.SceneLayer);
            Assert.Same(sheet, restored.CurrentFrame.Tilesheet);
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.SceneLayerCoordinates, restored.SceneLayerCoordinates);
            Assert.Equal(original.RenderSize, restored.RenderSize);
            Assert.Equal(original.Rotation, restored.Rotation);
            Assert.Equal(original.NudgeX, restored.NudgeX);
            Assert.Equal(count + 1, SpriteManager.Instance.AllSprites.Count);
        }
        finally
        {
            SpriteManager.Instance._spriteList.Remove(original);
            SpriteManager.Instance._spriteList.Remove(restored);
            original.DisposeImmediate();
            restored.DisposeImmediate();
        }
    }

    [Fact]
    public void FailedCollectionRemovesEarlierMaterializedSprites()
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap($"gspr-{Guid.NewGuid()}", new SKBitmap(16, 16));
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var definition = new SpriteDefinition();
        foreach (var name in new[] { "first", "second" })
            definition.Sprites.Add(new() { Nickname = name, SceneId = scene.ID, SceneLayerId = layer.ID,
                Frame = new() { Tilesheet = sheet.Name, RegionName = sheet.DefaultRegion.Name } });
        definition.Sprites[1].SceneLayerId = "missing";
        var count = SpriteManager.Instance.AllSprites.Count;
        Assert.Throws<InvalidDataException>(() => SpriteDefinitionSerializer.ToSprites(definition));
        Assert.Equal(count, SpriteManager.Instance.AllSprites.Count);
    }
}
