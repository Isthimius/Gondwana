using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using Gondwana.Widgets.Hud;

namespace Gondwana.Tests.Widgets;

[Collection("SpriteManager")]
public sealed class HealthBarWidgetTests : IDisposable
{
    private readonly List<Sprite> _sprites = [];

    [Fact]
    public void Constructor_StartsFullAndCentersBarAboveSprite()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer layer = AddLayer(host);
        Sprite sprite = CreateSprite(layer, new Vector2(3, 4));

        using var bar = new HealthBarWidget(
            host,
            sprite,
            maximum: 100f,
            size: new Size(60, 10));

        Rectangle target = sprite.DrawLocationWorld;

        Assert.Equal(1f, bar.Fraction);
        Assert.Equal(target.Left + (target.Width - 60) / 2, bar.TrackBoundsWorld.Left);
        Assert.Equal(target.Top - 16, bar.TrackBoundsWorld.Top);
        Assert.Equal(56, bar.FillBoundsWorld.Width);
    }

    [Fact]
    public void Value_ClampsAndResizesFill()
    {
        using var host = new TestRenderSurfaceHost();
        Sprite sprite = CreateSprite(AddLayer(host), Vector2.Zero);

        using var bar = new HealthBarWidget(
            host,
            sprite,
            maximum: 100f,
            size: new Size(104, 10));

        bar.Value = 25f;

        Assert.Equal(0.25f, bar.Fraction);
        Assert.Equal(25, bar.FillBoundsWorld.Width);

        bar.Value = 200f;
        Assert.Equal(100f, bar.Value);

        bar.Value = -10f;
        Assert.Equal(0f, bar.Value);
    }

    [Fact]
    public void SpriteMovement_RepositionsBar()
    {
        using var host = new TestRenderSurfaceHost();
        Sprite sprite = CreateSprite(AddLayer(host), Vector2.Zero);

        using var bar = new HealthBarWidget(
            host,
            sprite,
            maximum: 50f,
            size: new Size(40, 9));

        Rectangle before = bar.TrackBoundsWorld;

        sprite.SetPosition(new Vector2(2, 3));

        Assert.NotEqual(before.Location, bar.TrackBoundsWorld.Location);
        Assert.Equal(
            sprite.DrawLocationWorld.Top - 15,
            bar.TrackBoundsWorld.Top);
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _sprites)
        {
            if (SpriteManager.Instance._spriteList.Remove(sprite))
                sprite.DisposeImmediate();
        }
    }

    private static SceneLayer AddLayer(TestRenderSurfaceHost host)
    {
        return host.Scene.AddLayer(
            columnCount: 10,
            rowCount: 10,
            width: 64,
            height: 64,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);
    }

    private Sprite CreateSprite(SceneLayer layer, Vector2 position)
    {
        Sprite sprite = SpriteManager.Instance.CreateSprite(layer, default);
        sprite.RenderSize = new Size(64, 64);
        sprite.SetPosition(position);
        _sprites.Add(sprite);
        return sprite;
    }
}
