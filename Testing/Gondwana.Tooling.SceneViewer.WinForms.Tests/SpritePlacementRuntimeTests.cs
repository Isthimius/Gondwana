using Gondwana.Assets;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

public sealed partial class ViewerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ViewerMaterializesAuthoredPlacementAndItsSpriteAnimation(bool packed)
    {
        WriteContent();
        var scene = SceneDefinitionSerializer.Load(ScenePath);
        scene.AnimationSources.Clear();
        scene.Layers[0].Tiles.Clear();
        var layer = scene.Layers[1];
        layer.OriginPx = new(17, -9);
        layer.TileWidth = 48;
        layer.TileHeight = 32;
        layer.CoordinateSystemType = CoordinateSystemTypes.HexAxialFlatTop;
        SceneDefinitionSerializer.Save(ScenePath, scene);

        var sprite = Sprite("placed");
        sprite.SceneLayerId = layer.ID;
        sprite.Position = new(-1.25f, 2.5f);
        sprite.AnimationKey = "walk";
        sprite.StartAnimation = true;
        sprite.RenderSize = new(30, 20);
        sprite.Rotation = 30;
        sprite.NudgeX = 3;
        sprite.NudgeY = -4;
        var definition = new SpriteDefinition
        {
            Key = "actors",
            Sprites = [sprite],
            AnimationSources = [SpriteAnimationSourceDefinition.Loose("walk", "animation.gani")],
            SceneSources = [SpriteSceneSourceDefinition.Loose(scene.ID, Path.GetFileName(ScenePath))]
        };
        if (packed)
            Pack("actors.gaf", AssetTypes.SpriteDefinition, "sprites/nested/actors.gspr",
                SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = "decoy" }, definition] }));
        else
            SpriteDefinitionSerializer.SaveAll(Path.Combine(_directory, "actors.gspr"), [new() { Key = "decoy" }, definition]);

        using var runtime = new ViewerSceneLoader().Load(ScenePath);
        var placed = Assert.Single(SpriteManager.Instance.AllSprites);
        Assert.Equal(sprite.Id, placed.Id);
        Assert.Equal(sprite.Position, placed.SceneLayerCoordinates);
        Assert.Equal(layer.ID, placed.SceneLayer.ID);
        Assert.True(placed.TileAnimator.IsCycling);
        Assert.Equal(30, placed.Rotation);
        // Hex anchor (-62, 101), centered horizontally and bottom-aligned, with (3,-4) nudge.
        Assert.Equal(new Rectangle(-50, 109, 30, 20), placed.DrawLocationWorld);
        var runtimeLayer = runtime.SceneLayers.Single(item => item.ID == layer.ID);
        Assert.Equal(sprite.Position, runtimeLayer.WorldPxToContinuousGrid(runtimeLayer.GridToWorldPx(sprite.Position)));
    }
}
