using System.Drawing;
using System.Numerics;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;

namespace Gondwana.Tests.Drawing.Sprites;

public sealed class SpriteCompositionTests : IDisposable
{
    private readonly List<Sprite> _sprites = new();
    private readonly List<Scene> _scenes = new();

    [Fact]
    public void GetPosition_ReturnsCompositeAnchorInGridCoordinates()
    {
        SceneLayer layer = CreateLayer(
            columns: 10,
            rows: 10,
            tileWidth: 32,
            tileHeight: 16);

        Sprite first = CreateSprite(layer, new Vector2(2, 3));
        Sprite second = CreateSprite(layer, new Vector2(4, 5));
        var composite = new CompositeSprite(first, second);

        composite.AnchorMode = CompositeAnchorMode.TopLeft;
        Assert.Equal(new Vector2(2, 3), composite.GetPosition());

        composite.AnchorMode = CompositeAnchorMode.Center;
        Assert.Equal(new Vector2(3.5f, 4.5f), composite.GetPosition());
    }

    [Fact]
    public void SetPosition_MovesChildrenUsingGridSpace()
    {
        SceneLayer layer = CreateLayer(
            columns: 12,
            rows: 12,
            tileWidth: 32,
            tileHeight: 16);

        Sprite first = CreateSprite(layer, new Vector2(2, 3));
        Sprite second = CreateSprite(layer, new Vector2(4, 5));
        var composite = new CompositeSprite(first, second);

        composite.SetPosition(new Vector2(6, 7));

        Assert.Equal(new Vector2(6, 7), first.GetPosition());
        Assert.Equal(new Vector2(8, 9), second.GetPosition());
    }

    [Fact]
    public void AddChildWithOffset_InterpretsOffsetInGridSpace()
    {
        SceneLayer layer = CreateLayer(
            columns: 10,
            rows: 10,
            tileWidth: 32,
            tileHeight: 16);

        Sprite body = CreateSprite(layer, new Vector2(2, 3));
        Sprite child = CreateSprite(layer, Vector2.Zero);
        var composite = new CompositeSprite(body);

        composite.AddChildWithOffset(child, new Vector2(1, -1));

        Assert.Equal(new Vector2(3, 2), child.GetPosition());
    }

    [Fact]
    public void CloneSprite_ToDifferentLayer_BindsMovementToDestinationLayer()
    {
        SceneLayer sourceLayer = CreateLayer(
            columns: 10,
            rows: 1,
            tileWidth: 32,
            tileHeight: 32);

        SceneLayer destinationLayer = CreateLayer(
            columns: 3,
            rows: 1,
            tileWidth: 32,
            tileHeight: 32);

        Sprite source = CreateSprite(sourceLayer, Vector2.Zero);
        Sprite clone = Track(
            SpriteManager.Instance.CloneSprite(
                source,
                destinationLayer));

        clone.SetPosition(new Vector2(2, 0));
        clone.Movement.WrapX = true;
        clone.Movement.SetVelocity(new Vector2(2, 0));

        clone.Movement.AdvanceMovement(1f);

        Assert.Same(destinationLayer, clone.SceneLayer);
        Assert.Equal(new Vector2(1, 0), clone.GetPosition());
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _sprites)
        {
            if (SpriteManager.Instance._spriteList.Remove(sprite))
                sprite.DisposeImmediate();
        }

        foreach (Scene scene in _scenes)
            scene.Dispose();
    }

    private SceneLayer CreateLayer(
        int columns,
        int rows,
        int tileWidth,
        int tileHeight)
    {
        var scene = new Scene();
        _scenes.Add(scene);

        return scene.AddLayer(
            columnCount: columns,
            rowCount: rows,
            width: tileWidth,
            height: tileHeight,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);
    }

    private Sprite CreateSprite(
        SceneLayer layer,
        Vector2 position)
    {
        Sprite sprite = Track(
            SpriteManager.Instance.CreateSprite(
                layer,
                default));

        sprite.RenderSize = new Size(
            layer.TileWidth,
            layer.TileHeight);

        sprite.SetPosition(position);
        return sprite;
    }

    private Sprite Track(Sprite sprite)
    {
        _sprites.Add(sprite);
        return sprite;
    }
}
