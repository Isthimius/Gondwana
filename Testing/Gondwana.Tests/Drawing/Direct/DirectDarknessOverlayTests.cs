using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class DirectDarknessOverlayTests
{
    [Fact]
    public void Draw_WithRevealSource_CarvesDarknessAtCenter()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 32, 32));
        SceneLayer layer = host.Scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 32,
            height: 32);
        using var backbuffer = new BitmapBackbuffer(32, 32);
        using var overlay = new DirectDarknessOverlay(host, view, layer);

        overlay.AddRevealSource(
            centerWorldPx: new PointF(16f, 16f),
            radiusWorldPx: 8f);

        backbuffer.Canvas.Clear(SKColors.White);
        overlay.Draw(backbuffer, new RectangleF(0f, 0f, 32f, 32f));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap result = SKBitmap.FromImage(snapshot);

        SKColor center = result.GetPixel(16, 16);
        SKColor corner = result.GetPixel(0, 0);

        Assert.Equal(SKColors.White, center);
        Assert.True(center.Red > corner.Red);
    }

    [Fact]
    public void TrackLight_WhenLightMoves_SyncsRevealSource()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 64, 64));
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

        using var overlay = new DirectDarknessOverlay(host, view, layer);

        var reveal = overlay.TrackLight(
            light,
            radiusScale: 1.5f,
            intensityScale: 0.5f);

        light.Intensity = 0.8f;
        light.SetRadius(12f);
        light.MoveTo(new PointF(24.25f, 28.5f));

        Assert.Equal(new PointF(24.25f, 28.5f), reveal.CenterWorldPx);
        Assert.InRange(reveal.RadiusWorldPx, 17.999f, 18.001f);
        Assert.InRange(reveal.Intensity, 0.399f, 0.401f);
    }

    [Fact]
    public void TrackLight_WhenLightIsDisposed_RemovesTrackedRevealSource()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 64, 64));
        SceneLayer layer = host.Scene.AddLayer(
            columnCount: 2,
            rowCount: 2,
            width: 32,
            height: 32);

        var light = new DirectRadialLight(
            Color.FromArgb(180, 255, 190, 80),
            host,
            layer,
            new PointF(12f, 14f),
            8f);

        using var overlay = new DirectDarknessOverlay(host, view, layer);

        overlay.TrackLight(light);

        Assert.Single(overlay.RevealSources);

        light.Dispose();

        Assert.Empty(overlay.RevealSources);
    }

    private static View AddView(
        TestRenderSurfaceHost host,
        Rectangle bounds)
    {
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.Viewport.TargetRectPx == bounds);
    }
}
