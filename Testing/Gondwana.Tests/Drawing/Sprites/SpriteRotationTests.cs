using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;

namespace Gondwana.Tests.Drawing.Sprites;

[Collection("SpriteManager")]
public sealed class SpriteRotationTests : IDisposable
{
    private readonly List<Sprite> _sprites = [];
    private readonly List<Scene> _scenes = [];

    [Fact]
    public void Rotation_ExpandsVisualBoundsAroundRenderCenter()
    {
        Sprite sprite = CreateSprite(new Size(64, 32));
        Rectangle unrotated = sprite.DrawLocationWorld;

        sprite.Rotation = 90f;

        Rectangle rotated = sprite.VisualBoundsWorld;

        Assert.Equal(unrotated.Width, rotated.Height);
        Assert.Equal(unrotated.Height, rotated.Width);
        Assert.Equal(unrotated.Left + unrotated.Width / 2f, rotated.Left + rotated.Width / 2f);
        Assert.Equal(unrotated.Top + unrotated.Height / 2f, rotated.Top + rotated.Height / 2f);
    }

    [Fact]
    public void CloneSprite_PreservesRotation()
    {
        Sprite source = CreateSprite(new Size(64, 64));
        source.Rotation = 137.5f;

        Sprite clone = Track(SpriteManager.Instance.CloneSprite(source));

        Assert.Equal(137.5f, clone.Rotation);
        Assert.Equal(source.VisualBoundsWorld, clone.VisualBoundsWorld);
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

    private Sprite CreateSprite(Size renderSize)
    {
        var scene = new Scene();
        _scenes.Add(scene);

        SceneLayer layer = scene.AddLayer(
            columnCount: 10,
            rowCount: 10,
            width: 64,
            height: 64,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        Sprite sprite = Track(SpriteManager.Instance.CreateSprite(layer, default));
        sprite.RenderSize = renderSize;
        sprite.SetPosition(new Vector2(3, 4));
        return sprite;
    }

    private Sprite Track(Sprite sprite)
    {
        _sprites.Add(sprite);
        return sprite;
    }
}
