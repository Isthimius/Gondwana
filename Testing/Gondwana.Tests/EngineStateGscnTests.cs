using System.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using SkiaSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

/// <summary>
/// Verifies EngineState integration for inline and external GSCN scene definitions.
/// </summary>
[Collection("Global engine state")]
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
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
    }

    public void Dispose()
    {
        SpriteManager.Instance._spriteList.Clear();
        Scene.ClearAllScenes();
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SceneEmpty_IsNotRegisteredAsRuntimeScene()
    {
        Assert.DoesNotContain(Scene.Empty, Scene.GetAllScenes());
        Assert.Empty(Scene.GetAllScenes());
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
        AssertJsonNullOrMissing(entry, "GscnPath");

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

        AssertJsonNullOrMissing(entry, "Definition");
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LoadFromFile_RestoresGaniAssignmentBeforeGscnMaterialization(
        bool separateGaniFiles,
        bool separateGscnFiles)
    {
        const string sheetName = "scene-animation-sheet";
        const string animationKey = "scene.tile.walk";

        var sheet = CreateTilesheet(sheetName);
        _ = CreateCycle(sheet, animationKey);

        using var original = CreateScene();
        var tile = Assert.Single(original.SceneLayers)[2, 1]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);
        tile.EnableAnimator = true;
        tile.TileAnimator.StartAnimation(animationKey);

        var path = Path.Combine(
            _tempDir,
            $"animation-{separateGaniFiles}-{separateGscnFiles}.state");

        new EngineState().SaveToFile(
            path,
            parts:
                EngineStateParts.Tilesheets |
                EngineStateParts.Cycles |
                EngineStateParts.Scenes,
            separateGaniFiles: separateGaniFiles,
            separateGscnFiles: separateGscnFiles);

        var stateJson = File.ReadAllText(path);
        Assert.Contains(animationKey, stateJson);

        Scene.ClearAllScenes();
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();

        // Cycles normalize their GTS dependency and are restored before Scenes.
        // Scenes deliberately do not imply Cycles; the caller selects both because
        // this GSCN contains an animation reference.
        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Cycles | EngineStateParts.Scenes);

        var restoredScene = Assert.Single(Scene.GetAllScenes());
        var restoredTile = Assert.Single(restoredScene.SceneLayers)[2, 1]!;

        Assert.True(restoredTile.EnableAnimator);
        Assert.NotNull(restoredTile.TileAnimator.CurrentCycle);
        Assert.Equal(
            animationKey,
            restoredTile.TileAnimator.CurrentCycle.CycleKey);
        Assert.True(restoredTile.TileAnimator.IsCycling);
        Assert.Equal(
            sheetName,
            restoredTile.CurrentFrame.Tilesheet.Name);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MergeFromFile_GscnSceneHonorsOverwriteExisting(bool separateGscnFiles)
    {
        using var incoming = CreateScene();
        incoming.ID = "shared-scene";
        var incomingLayerId = Assert.Single(incoming.SceneLayers).ID;
        var path = Path.Combine(
            _tempDir,
            separateGscnFiles ? "merge-external.state" : "merge-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Scenes,
            separateGscnFiles: separateGscnFiles);

        Scene.ClearAllScenes();

        using var existing = new Scene
        {
            ID = "shared-scene"
        };
        var existingLayer = existing.AddLayer(1, 1, 8, 8);
        var existingLayerId = existingLayer.ID;

        EngineState.MergeFromFile(
            path,
            overwriteExisting: false,
            parts: EngineStateParts.Scenes);

        var preserved = Assert.Single(Scene.GetAllScenes());
        Assert.Same(existing, preserved);
        Assert.Equal(existingLayerId, Assert.Single(preserved.SceneLayers).ID);

        EngineState.MergeFromFile(
            path,
            overwriteExisting: true,
            parts: EngineStateParts.Scenes);

        var replaced = Assert.Single(Scene.GetAllScenes());
        Assert.NotSame(existing, replaced);
        Assert.Equal("shared-scene", replaced.ID);
        Assert.Equal(incomingLayerId, Assert.Single(replaced.SceneLayers).ID);
        Assert.Equal(3, Assert.Single(replaced.SceneLayers).GridColumnCount);
        Assert.Equal(2, Assert.Single(replaced.SceneLayers).GridRowCount);
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

    private Tilesheet CreateTilesheet(string name)
    {
        var imagePath = Path.Combine(_tempDir, $"{name}.png");

        using (var bitmap = new SKBitmap(32, 16))
        {
            bitmap.Erase(SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(imagePath, data.ToArray());
        }

        var sheet = TilesheetRegistry.Instance.LoadFromImageFile(
            name,
            imagePath);
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        return sheet;
    }

    private static Cycle CreateCycle(
        Tilesheet sheet,
        string key)
    {
        var sequence = new FrameSequence(
            [
                sheet.GetFrame(0, 0),
                sheet.GetFrame(1, 0)
            ])
        {
            SequenceCycleType = CycleType.Repeating
        };

        return new Cycle(sequence, 0.1, key);
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

    private static void AssertJsonNullOrMissing(JObject obj, string propertyName)
    {
        var token = obj[propertyName];
        Assert.True(
            token is null || token.Type == JTokenType.Null,
            $"Expected '{propertyName}' to be absent or JSON null, but found {token?.Type}.");
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
