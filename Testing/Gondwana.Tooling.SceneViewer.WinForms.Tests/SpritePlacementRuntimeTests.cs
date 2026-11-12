using Gondwana.Assets;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpriteAnimationFallbackRejectsConflictingExternalSources(bool packed)
    {
        WriteContent();
        var scene = SceneDefinitionSerializer.Load(ScenePath);
        scene.AnimationSources.Clear();
        scene.Layers[0].Tiles.Clear();
        SceneDefinitionSerializer.Save(ScenePath, scene);

        Directory.CreateDirectory(Path.Combine(_directory, "external"));
        string animationPath = Path.Combine(_directory, "animation.gani");
        string json = File.ReadAllText(animationPath);
        File.Delete(animationPath);

        foreach (string actor in new[] { "one", "two" })
        {
            string loose = $"external/{actor}.gani";
            string entry = $"cycles/{actor}.gani";
            var source = packed
                ? SpriteAnimationSourceDefinition.Packed("walk", "external/walk.gaf", entry)
                : SpriteAnimationSourceDefinition.Loose("walk", loose);
            if (packed) Pack("external/walk.gaf", AssetTypes.AnimationDefinition, entry, json);
            else File.WriteAllText(Path.Combine(_directory, loose), json);

            var sprite = Sprite(actor);
            sprite.AnimationKey = "walk";
            SpriteDefinitionSerializer.Save(Path.Combine(_directory, actor + ".gspr"),
                new SpriteDefinition { Sprites = [sprite], AnimationSources = [source] });
        }

        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains("Conflicting sources for animation 'walk'", error.Message);
        Assert.Contains("one.gani", error.Message);
        Assert.Contains("two.gani", error.Message);
    }

    [Fact]
    public void SpriteSourceCompletesTheSceneAnimationCycleChain()
    {
        WriteContent();
        string walkPath = Path.Combine(_directory, "animation.gani");
        var walk = AnimationDefinitionSerializer.Load(walkPath);
        walk.NextCycleKey = "return";
        AnimationDefinitionSerializer.Save(walkPath, walk);

        Directory.CreateDirectory(Path.Combine(_directory, "external"));
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "external", "return.gani"), new AnimationDefinition
        {
            Key = "return",
            NextCycleKey = "walk",
            ThrottleTime = .1,
            Frames = [new AnimationFrameDefinition { Tilesheet = "sheet" }]
        });
        var sprite = Sprite("walker");
        sprite.AnimationKey = "walk";
        sprite.StartAnimation = true;
        SpriteDefinitionSerializer.Save(Path.Combine(_directory, "walker.gspr"), new SpriteDefinition
        {
            Sprites = [sprite],
            AnimationSources = [SpriteAnimationSourceDefinition.Loose("return", "external/return.gani")]
        });

        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(Assert.Single(SpriteManager.Instance.AllSprites).TileAnimator.IsCycling);
        var first = Assert.IsType<Cycle>(Cycle.GetAnimationCycle("walk"));
        Assert.Equal("return", first.NextCycle?.CycleKey);
        Assert.Equal("walk", first.NextCycle?.NextCycle?.CycleKey);
    }
}
