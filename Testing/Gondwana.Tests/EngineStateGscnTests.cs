using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

/// <summary>
/// Verifies EngineState integration for inline and external GSCN scene definitions.
/// </summary>
public sealed class EngineStateGscnTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"GondwanaEngineStateGscn_{Guid.NewGuid():N}");

    public EngineStateGscnTests()
    {
        Directory.CreateDirectory(_tempDir);
        SpriteManager.Instance._spriteList.Clear();
        Scene.ClearAllScenes();
    }

    public void Dispose()
    {
        SpriteManager.Instance._spriteList.Clear();
        Scene.ClearAllScenes();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SaveToFile_Default_EmbedsInlineGscnDefinition()
    {
        using var scene = CreateScene();
        var path = Path.Combine(_tempDir, "inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Scenes);

        var root = JObject.Parse(File.ReadAllText(path));
        var entry = FirstSceneEntry(root);

        Assert.NotNull(entry["Definition"]);
        Assert.Null(entry["GscnPath"]);

        var definition = (JObject)entry["Definition"]!;
        Assert.Equal(scene.ID, definition.Value<string>("ID"));
        Assert.NotNull(definition["Layers"]);
        Assert.Null(definition["SceneLayers"]);
        Assert.Null(definition["_sceneLayers"]);
    }

    [Fact]
    public void SaveToFile_SeparateGscnFiles_WritesReferenceAndCleanDefinition()
    {
        using var scene = CreateScene();
        var path = Path.Combine(_tempDir, "external.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Scenes,
            separateGscnFiles: true);

        var root = JObject.Parse(File.ReadAllText(path));
        var entry = FirstSceneEntry(root);

        Assert.Null(entry["Definition"]);
        var relativePath = Assert.IsType<JValue>(entry["GscnPath"]).Value<string>();
        Assert.False(string.IsNullOrWhiteSpace(relativePath));

        var gscnPath = Path.GetFullPath(Path.Combine(_tempDir, relativePath!));
        Assert.True(File.Exists(gscnPath));
        Assert.Equal(
            Path.Combine(_tempDir, "external.scenes"),
            Path.GetDirectoryName(gscnPath));

        var gscnJson = File.ReadAllText(gscnPath);
        Assert.DoesNotContain("\"$id\"", gscnJson);
        Assert.DoesNotContain("\"$ref\"", gscnJson);
        Assert.Contains("\"Layers\"", gscnJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadFromFile_RestoresInlineOrExternalGscnScene(bool separateGscnFiles)
    {
        using var original = CreateScene();
        var sceneId = original.ID;
        var layer = Assert.Single(original.SceneLayers);
        var layerId = layer.ID;
        var path = Path.Combine(
            _tempDir,
            separateGscnFiles ? "restore-external.state" : "restore-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Scenes,
            separateGscnFiles: separateGscnFiles);

        Scene.ClearAllScenes();

        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Scenes);

        var restored = Assert.Single(Scene.GetAllScenes());
        Assert.Equal(sceneId, restored.ID);

        var restoredLayer = Assert.Single(restored.SceneLayers);
        Assert.Equal(layerId, restoredLayer.ID);
        Assert.Equal(3, restoredLayer.GridColumnCount);
        Assert.Equal(2, restoredLayer.GridRowCount);
        Assert.True(restoredLayer.WrapHorizontally);
        Assert.True(restoredLayer.ShowGridLines);
        Assert.Same(restored, restoredLayer.Scene);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullState_RestoresSpriteAgainstCanonicalGscnLayer(bool separateGscnFiles)
    {
        using var scene = CreateScene();
        var layer = Assert.Single(scene.SceneLayers);
        var sprite = SpriteManager.Instance.CreateSprite(
            layer,
            default,
            id: "hero");

        var sceneId = scene.ID;
        var layerId = layer.ID;
        var path = Path.Combine(
            _tempDir,
            separateGscnFiles ? "sprite-external.state" : "sprite-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Scenes | EngineStateParts.Sprites,
            separateGscnFiles: separateGscnFiles);

        var json = File.ReadAllText(path);
        Assert.Contains("\"SceneId\"", json);
        Assert.Contains("\"SceneLayerId\"", json);
        Assert.DoesNotContain("\"SceneLayer\":", json);

        SpriteManager.Instance._spriteList.Clear();
        Scene.ClearAllScenes();

        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Scenes | EngineStateParts.Sprites);

        var restoredScene = Assert.Single(Scene.GetAllScenes());
        var restoredLayer = Assert.Single(restoredScene.SceneLayers);
        var restoredSprite = Assert.Single(SpriteManager.Instance.AllSprites);

        Assert.Equal(sceneId, restoredScene.ID);
        Assert.Equal(layerId, restoredLayer.ID);
        Assert.Equal("hero", restoredSprite.Nickname);
        Assert.Same(restoredLayer, restoredSprite.SceneLayer);
    }

    [Fact]
    public void LoadFromFile_AcceptsLegacyRawSceneEntries()
    {
        using var original = CreateScene();
        var sceneId = original.ID;
        var layerId = Assert.Single(original.SceneLayers).ID;
        var path = Path.Combine(_tempDir, "legacy.state");

        var legacyJson = JsonConvert.SerializeObject(
            new
            {
                Scenes = Scene.GetAllScenes().ToList()
            },
            EngineState.JsonSerializerSettings);

        File.WriteAllText(path, legacyJson);
        Scene.ClearAllScenes();

        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Scenes);

        var restored = Assert.Single(Scene.GetAllScenes());
        Assert.Equal(sceneId, restored.ID);
        Assert.Equal(layerId, Assert.Single(restored.SceneLayers).ID);
    }

    private static Scene CreateScene()
    {
        var scene = new Scene();
        var layer = scene.AddLayer(3, 2, 24, 20, zOrder: 4, parallax: 0.75f);
        layer.WrapHorizontally = true;
        layer.ShowGridLines = true;
        layer[2, 1]!.Nickname = "corner";
        return scene;
    }

    private static JObject FirstSceneEntry(JObject root)
    {
        var scenes = root["Scenes"]
            ?? throw new Xunit.Sdk.XunitException("EngineState JSON did not contain Scenes.");

        JArray values = scenes switch
        {
            JArray array => array,
            JObject wrapper when wrapper["$values"] is JArray array => array,
            _ => throw new Xunit.Sdk.XunitException(
                $"Unexpected Scenes JSON shape: {scenes.Type}.")
        };

        return Assert.IsType<JObject>(Assert.Single(values));
    }
}
