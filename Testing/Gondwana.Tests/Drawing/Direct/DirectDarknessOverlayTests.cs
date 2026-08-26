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

    private static View AddView(
        TestRenderSurfaceHost host,
        Rectangle bounds)
    {
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.Viewport.TargetRectPx == bounds);
    }
}
