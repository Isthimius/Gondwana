using Gondwana.Scenes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

/// <summary>
/// Regression tests for the serialized Scene -> SceneLayer -> SceneLayerTile graph.
/// </summary>
public sealed class SceneSerializationTests
{
    [Fact]
    public void SceneGraph_SerializesCleanOwnershipShape_AndRoundTrips()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = true;
        layer.OriginPx = new(7, 9);

        var tile = layer[1, 1]!;
        tile.Nickname = "preserved tile";
        tile.Visible = false;

        var json = JsonConvert.SerializeObject(scene, EngineState.JsonSerializerSettings);

        Assert.Contains("\"SceneLayers\":", json);
        Assert.DoesNotContain("\"_sceneLayers\":", json);
        Assert.DoesNotContain("\"Scene\":", json);
        Assert.DoesNotContain("\"parentSceneLayer\":", json);

        var root = JObject.Parse(json);
        Assert.NotNull(root["SceneLayers"]);

        using var restored = JsonConvert.DeserializeObject<Scene>(
            json,
            EngineState.JsonSerializerSettings)!;

        var restoredLayer = Assert.Single(restored.SceneLayers);
        Assert.Same(restored, restoredLayer.Scene);
        Assert.True(restoredLayer.WrapHorizontally);
        Assert.Equal(new System.Drawing.Point(7, 9), restoredLayer.OriginPx);

        var restoredTile = restoredLayer[1, 1]!;
        Assert.Same(restoredLayer, restoredTile.SceneLayer);
        Assert.Equal("preserved tile", restoredTile.Nickname);
        Assert.False(restoredTile.Visible);
    }

    [Fact]
    public void SceneGraph_StillReadsLegacyUnderscoreLayerCollectionName()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        layer[0, 0]!.Nickname = "legacy tile";

        var root = JObject.Parse(
            JsonConvert.SerializeObject(scene, EngineState.JsonSerializerSettings));

        root["_sceneLayers"] = root["SceneLayers"];
        root.Remove("SceneLayers");

        using var restored = JsonConvert.DeserializeObject<Scene>(
            root.ToString(Formatting.None),
            EngineState.JsonSerializerSettings)!;

        var restoredLayer = Assert.Single(restored.SceneLayers);
        Assert.Same(restored, restoredLayer.Scene);
        Assert.Same(restoredLayer, restoredLayer[0, 0]!.SceneLayer);
        Assert.Equal("legacy tile", restoredLayer[0, 0]!.Nickname);
    }
}
