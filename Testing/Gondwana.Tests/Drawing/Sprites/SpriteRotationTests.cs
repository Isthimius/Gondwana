using System.Drawing;
using System.Numerics;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

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

    [Fact]
    public void BitmapBackbuffer_Draw_RotatesSpriteAroundDestinationCenter()
    {
        using var backbuffer = new BitmapBackbuffer(12, 12);

        AssertRotationRendered(backbuffer);
    }

    [Fact]
    public void GpuBackbuffer_Draw_RotatesSpriteAroundDestinationCenter()
    {
        using var backbuffer = new GpuBackbuffer(12, 12);

        AssertRotationRendered(backbuffer);
    }

    [Fact]
    public void Rotation_DoesNotChangeAxisAlignedCollisionArea()
    {
        Sprite sprite = CreateSprite(new Size(64, 32));
        Rectangle collisionArea = sprite.CollisionArea;

        sprite.Rotation = 45f;

        Assert.Equal(collisionArea, sprite.CollisionArea);
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

    private Sprite CreateSprite(Size renderSize, Frame frame = default)
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

        Sprite sprite = Track(SpriteManager.Instance.CreateSprite(layer, frame));
        sprite.RenderSize = renderSize;
        sprite.SetPosition(new Vector2(3, 4));
        return sprite;
    }

    private Sprite Track(Sprite sprite)
    {
        _sprites.Add(sprite);
        return sprite;
    }

    private void AssertRotationRendered(BackbufferBase backbuffer)
    {
        var bitmap = new SKBitmap(4, 2);
        bitmap.Erase(SKColors.Red);
        using var tilesheet = TilesheetFactory.FromBitmap(
            $"rotation-{backbuffer.GetType().Name}",
            bitmap);
        tilesheet.DefaultRegion.TileSize = new Size(4, 2);

        Sprite sprite = CreateSprite(
            new Size(4, 2),
            tilesheet.GetFrame(0, 0));
        sprite.Rotation = 90f;

        backbuffer.Canvas.Clear(SKColors.Transparent);
        sprite.Draw(backbuffer, new RectangleF(4f, 5f, 4f, 2f));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap rendered = SKBitmap.FromImage(snapshot);

        Assert.Equal(SKColors.Red, rendered.GetPixel(5, 4));
        Assert.Equal((byte)0, rendered.GetPixel(4, 5).Alpha);
        Assert.True(backbuffer.Canvas.TotalMatrix.IsIdentity);
    }
}
