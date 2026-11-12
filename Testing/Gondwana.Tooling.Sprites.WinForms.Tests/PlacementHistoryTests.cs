using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Sprites.Editing;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

public sealed partial class InteractionTests
{
    [Fact]
    public void PlacementIsAtomicAndRetainsThePackedSceneReferenceAcrossSaveAs() => Sta(root =>
    {
        using var document = SpriteDocument.Create(root);
        var sprite = document.AddSprite();
        string gspr = Path.Combine(root, "actors.gspr");
        document.Save(gspr, allowInvalid: true);
        var member = document.Definition;
        var scene = new ResourceAddress(new AssetLocation(AssetTypes.SceneDefinition, Path.Combine(root, "world.gaf"), "maps/city.gscn"), "city");
        SpriteInstanceDrag Drag() => new(new ResourceAddress(document.Location!, member.Key), sprite.Id, document, document.Revision);

        var first = Drag();
        Assert.True(document.PlaceInstance(first, scene, "front", new(-2.25f, 4.5f)));
        Assert.False(document.PlaceInstance(Drag(), scene, "front", new(-2.25f, 4.5f)));
        Assert.Throws<InvalidOperationException>(() => document.PlaceInstance(first, scene, "front", new(1, 1)));
        var reference = Assert.Single(member.SceneSources);
        Assert.Equal("maps/city.gscn", reference.AssetEntryName);
        var conflicting = scene with { Location = new AssetLocation(AssetTypes.SceneDefinition, Path.Combine(root, "other.gscn")) };
        Assert.Throws<InvalidDataException>(() => document.PlaceInstance(Drag(), conflicting, "other", new(8, 9)));
        Assert.Equal(("city", "front", new PointF(-2.25f, 4.5f)), (sprite.SceneId, sprite.SceneLayerId, sprite.Position));

        document.History.Undo();
        Assert.Empty(member.SceneSources);
        Assert.False(document.IsDirty);
        document.History.Redo();
        Assert.Equal(reference.AssetEntryName, Assert.Single(member.SceneSources).AssetEntryName);

        var destination = Path.Combine(root, "elsewhere", "saved.gspr");
        document.Save(destination);
        Assert.False(document.IsDirty);
        using var saved = SpriteDocument.Open(destination);
        var savedReference = Assert.Single(saved.Definition.SceneSources);
        Assert.Equal(scene.Location.Path, saved.ResolveReferencePath(savedReference.AssetsFilePath!));
        Assert.Equal(scene.Location.AssetEntryName, savedReference.AssetEntryName);
        Assert.Equal(sprite.Position, Assert.Single(saved.Definition.Sprites).Position);
        document.History.Undo();
        Assert.Empty(member.SceneSources);
        Assert.True(document.IsDirty);
        document.History.Redo();
        Assert.Equal(scene.Location.Path, document.ResolveReferencePath(Assert.Single(member.SceneSources).AssetsFilePath!));
        Assert.False(document.IsDirty);
    });
}
