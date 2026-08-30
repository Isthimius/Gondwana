using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class DirectSceneLayerDarknessOverlayTests
{
    [Fact]
    public void Draw_WithRevealSource_CarvesDarknessAtCenter()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer layer = host.Scene.AddLayer(
            columnCount: 2,
            rowCount: 2,
            width: 32,
            height: 32);

        using var backbuffer = new BitmapBackbuffer(64, 64);
        using var overlay = new DirectSceneLayerDarknessOverlay(
            host,
            layer,
            new Rectangle(0, 0, 64, 64));

        overlay.AddRevealSource(
            centerWorldPx: new PointF(32f, 32f),
            radiusWorldPx: 12f);

        backbuffer.Canvas.Clear(SKColors.White);
        overlay.Draw(backbuffer, new RectangleF(0f, 0f, 64f, 64f));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap result = SKBitmap.FromImage(snapshot);

        SKColor center = result.GetPixel(32, 32);
        SKColor corner = result.GetPixel(0, 0);

        Assert.Equal(SKColors.White, center);
        Assert.True(center.Red > corner.Red);
    }

    [Fact]
    public void TrackLight_WhenLightMoves_SyncsRevealSource()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer layer = host.Scene.AddLayer(
            columnCount: 2,
            rowCount: 2,
            width: 32,
            height: 32);

        using var light = new DirectRadialLight(
            Color.FromArgb(180, 255, 190, 80),
            host,
            layer,
            new PointF(12f, 14f),
            8f);

        using var overlay = new DirectSceneLayerDarknessOverlay(
            host,
            layer,
            new Rectangle(0, 0, 64, 64));

        var reveal = overlay.TrackLight(
            light,
            radiusScale: 2f,
            intensityScale: 0.5f);

        light.Intensity = 0.8f;
        light.SetRadius(12f);
        light.MoveTo(new PointF(24.25f, 28.5f));

        Assert.Equal(new PointF(24.25f, 28.5f), reveal.CenterWorldPx);
        Assert.InRange(reveal.RadiusWorldPx, 23.999f, 24.001f);
        Assert.InRange(reveal.Intensity, 0.399f, 0.401f);
    }

    [Fact]
    public void TrackLight_WhenLightIsOnDifferentSceneLayer_Throws()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer darknessLayer = host.Scene.AddLayer(2, 2, 32, 32);
        SceneLayer lightLayer = host.Scene.AddLayer(2, 2, 32, 32);

        using var light = new DirectRadialLight(
            Color.FromArgb(180, 255, 190, 80),
            host,
            lightLayer,
            new PointF(12f, 14f),
            8f);

        using var overlay = new DirectSceneLayerDarknessOverlay(
            host,
            darknessLayer,
            new Rectangle(0, 0, 64, 64));

        Assert.Throws<ArgumentException>(() => overlay.TrackLight(light));
    }

    [Fact]
    public void TrackLightLayer_WhenLightIsRemoved_RemovesTrackedRevealSource()
    {
        using var host = new TestRenderSurfaceHost();
        SceneLayer layer = host.Scene.AddLayer(2, 2, 32, 32);
        using var lights = new DirectLightLayer(host, layer);
        using var overlay = new DirectSceneLayerDarknessOverlay(
            host,
            layer,
            new Rectangle(0, 0, 64, 64));

        overlay.TrackLightLayer(lights);

        DirectRadialLight torch = lights.AddTorchLight(new PointF(12f, 14f), 8f);

        Assert.Single(overlay.RevealSources);

        lights.Remove(torch);

        Assert.Empty(overlay.RevealSources);
    }
}
