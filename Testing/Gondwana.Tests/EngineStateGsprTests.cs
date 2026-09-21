using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class EngineStateGsprTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GsprState-" + Guid.NewGuid());
    public EngineStateGsprTests() { Directory.CreateDirectory(_directory); Cleanup(); }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CollectionStateRoundTrip(bool external, bool compressed)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        foreach (var name in new[] { "player", "guard-1", "guard-2" }) SpriteManager.Instance.CreateSprite(layer, default, name);
        var ids = SpriteManager.Instance.AllSprites.Select(sprite => sprite.Id).ToArray();
        var path = Path.Combine(_directory, "world.state");
        new EngineState().SaveToFile(path, compress: compressed, parts: EngineStateParts.Scenes | EngineStateParts.Sprites, separateGsprFile: external);
        if (external)
        {
            var gspr = Assert.Single(Directory.GetFiles(_directory, "*.gspr"));
            Assert.Equal(3, SpriteDefinitionSerializer.Load(gspr).Sprites.Count);
            Assert.DoesNotContain("$id", File.ReadAllText(gspr));
            Assert.DoesNotContain("$ref", File.ReadAllText(gspr));
            if (!compressed) Assert.Equal("world.sprites.gspr", JObject.Parse(File.ReadAllText(path))["Sprites"]!["GsprPath"]!.Value<string>());
        }
        EngineState.LoadFromFile(path, compressed, EngineStateParts.Scenes | EngineStateParts.Sprites);
        Assert.Equal(ids, SpriteManager.Instance.AllSprites.Select(sprite => sprite.Id));
        var restored = Assert.Single(Scene.GetAllScenes()).SceneLayers.Single();
        Assert.All(SpriteManager.Instance.AllSprites, sprite => Assert.Same(restored, sprite.SceneLayer));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpriteOnlyMergeHonorsOverwriteAndUsesExistingLayer(bool overwrite)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var original = SpriteManager.Instance.CreateSprite(layer, default, "player");
        original.NudgeX = 7;
        var path = Path.Combine(_directory, "sprites.state");
        new EngineState().SaveToFile(path, parts: EngineStateParts.Sprites);
        original.NudgeX = 19;
        EngineState.MergeFromFile(path, overwriteExisting: overwrite, parts: EngineStateParts.Sprites);
        var merged = Assert.Single(SpriteManager.Instance.AllSprites);
        Assert.Equal(overwrite ? 7 : 19, merged.NudgeX);
        Assert.Same(layer, merged.SceneLayer);
        Assert.Same(scene, Assert.Single(Scene.GetAllScenes()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyRawSpriteArraysRemainReadable(bool references)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var original = SpriteManager.Instance.CreateSprite(layer, default, "legacy");
        original.NudgeY = 31;
        var settings = references ? EngineState.JsonSerializerSettings : new JsonSerializerSettings { Converters = { new Gondwana.Drawing.FrameJsonConverter() } };
        var path = Path.Combine(_directory, "legacy.state");
        File.WriteAllText(path, JsonConvert.SerializeObject(new { Sprites = new[] { original } }, settings));
        EngineState.LoadFromFile(path, parts: EngineStateParts.Sprites);
        var restored = Assert.Single(SpriteManager.Instance.AllSprites);
        Assert.Same(layer, restored.SceneLayer);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(31, restored.NudgeY);
    }

    [Fact]
    public void DuplicateIncomingNicknamesDoNotChangeOverwritePolicyForOtherSprites()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        var existing = SpriteManager.Instance.CreateSprite(layer, default, "keep");
        existing.NudgeX = 91;
        var definition = new SpriteDefinition();
        foreach (var (name, x) in new[] { ("duplicate", 1), ("duplicate", 2), ("keep", 3), ("", 4), ("", 5) })
            definition.Sprites.Add(new() { Id = Guid.NewGuid(), Nickname = name, NudgeX = x, SceneId = scene.ID, SceneLayerId = layer.ID });
        var path = Path.Combine(_directory, "merge.state");
        File.WriteAllText(path, JsonConvert.SerializeObject(new { Sprites = new { Definition = definition } }));
        EngineState.MergeFromFile(path, parts: EngineStateParts.Sprites);
        Assert.Equal(4, SpriteManager.Instance.AllSprites.Count);
        Assert.Same(existing, SpriteManager.Instance.GetSpriteByID("keep"));
        Assert.Equal(91, existing.NudgeX);
        Assert.Equal(2, SpriteManager.Instance.GetSpriteByID("duplicate")!.NudgeX);
        Assert.All(SpriteManager.Instance.AllSprites, sprite => Assert.False(string.IsNullOrWhiteSpace(sprite.Nickname)));
        Assert.Equal(4, SpriteManager.Instance.AllSprites.Select(sprite => sprite.Nickname).Distinct().Count());
    }

    private static void Cleanup()
    {
        foreach (var sprite in SpriteManager.Instance.AllSprites) { SpriteManager.Instance._spriteList.Remove(sprite); sprite.DisposeImmediate(); }
        Scene.ClearAllScenes();
    }
    public void Dispose() { Cleanup(); Directory.Delete(_directory, true); }
}
