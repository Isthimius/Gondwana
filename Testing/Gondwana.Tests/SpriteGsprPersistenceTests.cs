using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class SpriteGsprPersistenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CollisionAndVisualParityIgnoreCreationSizeDefault(bool byFrame, bool enabled)
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap("parity-" + Guid.NewGuid(), new SKBitmap(32, 16));
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var original = SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame(1, 0), "parity", CollisionProfileNames.Sensor);
        var oldDefault = SpriteManager.Instance.SizeNewSpritesToSceneLayer;
        Sprite? restored = null;
        try
        {
            original.Visible = false;
            original.ZOrder = 8;
            original.HorizAlign = HorizontalAlignment.Right;
            original.VertAlign = VerticalAlignment.Top;
            original.RenderSize = new Size(19, 47);
            original.Rotation = 32;
            original.EnableFog = true;
            original.NudgeX = -3; original.NudgeY = 9;
            original.AdjustCollisionArea = new CollisionAdjust(1, 2, -3, 4);
            original.CollisionType = TileCollisionType.Trigger;
            original.AdjustCollisionAreaByFrame = byFrame;
            original.CollisionTypeByFrame = byFrame;
            original.CollisionsEnabled = enabled;
            var definition = SpriteDefinitionSerializer.FromSprite(original);
            SpriteManager.Instance.SizeNewSpritesToSceneLayer = !oldDefault;
            restored = SpriteDefinitionSerializer.ToSprite(definition);
            Assert.Equal(SpriteDefinitionSerializer.ToJson(new() { Sprites = [definition] }),
                SpriteDefinitionSerializer.ToJson(SpriteDefinitionSerializer.FromSprites([restored])));
        }
        finally
        {
            SpriteManager.Instance.SizeNewSpritesToSceneLayer = oldDefault;
            SpriteManager.Instance._spriteList.Remove(original); original.DisposeImmediate();
            if (restored is not null) { SpriteManager.Instance._spriteList.Remove(restored); restored.DisposeImmediate(); }
        }
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("layer")]
    [InlineData("tilesheet")]
    [InlineData("region")]
    [InlineData("coordinate")]
    [InlineData("profile")]
    public void MissingRuntimeDependenciesFailClearly(string missing)
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap("missing-" + Guid.NewGuid(), new SKBitmap(16, 16));
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var entry = Valid(); entry.SceneId = scene.ID; entry.SceneLayerId = layer.ID;
        entry.Frame = new() { Tilesheet = sheet.Name, RegionName = sheet.DefaultRegion.Name };
        switch (missing)
        {
            case "scene": entry.SceneId = "missing"; break;
            case "layer": entry.SceneLayerId = "missing"; break;
            case "tilesheet": entry.Frame.Tilesheet = "missing"; break;
            case "region": entry.Frame.RegionName = "missing"; break;
            case "coordinate": entry.Frame.XTile = int.MaxValue; break;
            case "profile": entry.CollisionProfileName = "missing"; break;
        }
        var before = SpriteManager.Instance.AllSprites.ToArray();
        Assert.Throws<InvalidDataException>(() => SpriteDefinitionSerializer.ToSprite(entry));
        Assert.Equal(before, SpriteManager.Instance.AllSprites);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("layer")]
    [InlineData("position")]
    [InlineData("rotation")]
    [InlineData("size")]
    [InlineData("z")]
    [InlineData("enum")]
    [InlineData("guid")]
    [InlineData("nickname")]
    [InlineData("null")]
    [InlineData("sources")]
    public void InvalidDefinitionsAreRejectedWithoutMutation(string invalid)
    {
        var entry = Valid();
        var definition = new SpriteDefinition { Sprites = [entry] };
        switch (invalid)
        {
            case "scene": entry.SceneId = ""; break;
            case "layer": entry.SceneLayerId = ""; break;
            case "position": entry.Position = new(float.NaN, 0); break;
            case "rotation": entry.Rotation = float.PositiveInfinity; break;
            case "size": entry.RenderSize = new(-1, 1); break;
            case "z": entry.ZOrder = 0; break;
            case "enum": entry.CollisionType = (TileCollisionType)999; break;
            case "guid": var duplicate = Valid(); duplicate.Id = entry.Id; definition.Sprites.Add(duplicate); break;
            case "nickname": var sameName = Valid(); sameName.Nickname = entry.Nickname; definition.Sprites.Add(sameName); break;
            case "null": definition.Sprites.Add(null!); break;
            case "sources": definition.SceneSources.Add(new()); break;
        }
        var before = SpriteDefinitionSerializer.ToJson(definition);
        Assert.NotEmpty(SpriteDefinitionValidator.Validate(definition));
        Assert.Equal(before, SpriteDefinitionSerializer.ToJson(definition));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"Sprites\":[null]}")]
    [InlineData("{\"TilesheetSources\":[null]}")]
    [InlineData("{\"SceneSources\":[null]}")]
    public void MalformedJsonFailsCleanly(string json) => Assert.Throws<InvalidDataException>(() => SpriteDefinitionSerializer.FromJson(json));

    [Fact]
    public void ThrowingCreationCallbackRollsBackWholeIncomingCollection()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var definition = new SpriteDefinition { Sprites = [Valid(), Valid()] };
        foreach (var entry in definition.Sprites) { entry.SceneId = scene.ID; entry.SceneLayerId = layer.ID; }
        var before = SpriteManager.Instance.AllSprites.ToArray();
        int calls = 0;
        void FailSecond(Sprite sprite) { if (++calls == 2) throw new InvalidOperationException("Creation failed"); }
        SpriteManager.Instance.SpriteCreated += FailSecond;
        try
        {
            Assert.Throws<InvalidOperationException>(() => SpriteDefinitionSerializer.ToSprites(definition));
            Assert.Equal(before, SpriteManager.Instance.AllSprites);
        }
        finally { SpriteManager.Instance.SpriteCreated -= FailSecond; }
    }

    [Fact]
    public void LoosePackedStreamAndSaveAsPreserveCollectionAndPortableMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GsprPersistence-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var definition = new SpriteDefinition { Sprites = [Valid(), Valid()] };
            definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose("actors", "actors.gts"));
            definition.SceneSources.Add(SpriteSceneSourceDefinition.Packed("level", "content.gaf", "level.gscn"));
            var path = Path.Combine(directory, "actors.gspr");
            SpriteDefinitionSerializer.Save(path, definition);
            var loaded = SpriteDefinitionSerializer.Load(path);
            Assert.Equal(SpriteDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
            Assert.Equal(definition.Sprites.Select(sprite => sprite.Id), loaded.Sprites.Select(sprite => sprite.Id));
            var moved = Path.Combine(directory, "moved", "actors.gspr");
            SpriteDefinitionSerializer.Save(moved, loaded);
            var rebased = SpriteDefinitionSerializer.Load(moved);
            Assert.Equal(Path.Combine(directory, "actors.gts"), Path.GetFullPath(Assert.Single(rebased.TilesheetSources).GtsPath!, Path.GetDirectoryName(moved)!));
            Assert.Equal("level.gscn", Assert.Single(rebased.SceneSources).AssetEntryName);
            Assert.Equal("actors.gts", loaded.TilesheetSources[0].GtsPath);
            using var archive = AssetsFile.LoadOrCreate(Path.Combine(directory, "packed.gaf"), null, false, register: false);
            using var input = File.OpenRead(path);
            archive.Add(AssetTypes.SpriteDefinition, "actors.gspr", input);
            var packed = SpriteDefinitionSerializer.Load(archive, "actors.gspr");
            Assert.Equal(SpriteDefinitionSourceKind.PackedDefinitionFile, packed.Source.Kind);
            Assert.Equal("actors.gspr", packed.Source.AssetEntryName);
            Assert.Equal(2, packed.Sprites.Count);
            input.Position = 0;
            Assert.Equal(2, SpriteDefinitionSerializer.Load(input).Sprites.Count);
            Assert.True(input.CanRead);
            Assert.Equal(11, (int)AssetTypes.SpriteDefinition);
            Assert.Equal(Enumerable.Range(0, 12), Enum.GetValues<AssetTypes>().Select(value => (int)value));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static SpriteInstanceDefinition Valid() => new() { Id = Guid.NewGuid(), Nickname = Guid.NewGuid().ToString(), SceneId = "level", SceneLayerId = "actors" };
}

