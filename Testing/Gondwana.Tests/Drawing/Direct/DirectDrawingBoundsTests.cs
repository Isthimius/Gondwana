using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Verifies that view-mode drawings retain layout bounds independently of viewport clipping.
/// </summary>
[Collection("Global engine state")]
public sealed class DirectDrawingBoundsTests : IDisposable
{
    private readonly float _originalRenderScale;

    public DirectDrawingBoundsTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        _originalRenderScale = Engine.Instance.Configuration.RenderScale;
        Engine.Instance.Configuration.RenderScale = 1f;
    }

    public void Dispose()
    {
        Engine.Instance.Configuration.RenderScale = _originalRenderScale;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ViewDrawingsRetainBoundsAndRenderWithinGrowingViewport(bool gpu)
    {
        var adapter = new ViewportScalingTests.Adapter(64, 64);
        using var host = gpu
            ? (RenderSurfaceHostBase)new RenderSurfaceHost<GpuBackbuffer>(adapter)
            : new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        host.Bind(scene, false);
        buffer.ClearColor = SKColors.Black;
        var view = Assert.Single(host.ViewManager.Views);

        // Simulate overlays created while the browser still exposes a 1x1 viewport.
        view.Viewport.TargetRectPx = new Rectangle(0, 0, 1, 1);
        var bounds = new Rectangle(16, 16, 32, 32);

        using var drawing = new DirectRectangle(Color.Red, host, view, bounds)
            .SetFilled(true)
            .SetStrokeWidth(0f);
        using var text = new TextBlock(host, view, new Rectangle(8, 8, 40, 12));

        Assert.Equal(bounds, drawing.ScreenBounds);
        Assert.Equal(new Rectangle(8, 8, 40, 12), text.ScreenBounds);
        text.Visible = false;

        // A partially visible drawing must keep its original layout, while the
        // renderer clips pixels outside the active viewport.
        view.Viewport.TargetRectPx = new Rectangle(0, 0, 32, 32);
        host.RenderToBackbuffer(1);

        using (var image = buffer.Snapshot())
        using (var pixels = SKBitmap.FromImage(image))
        {
            Assert.Equal(SKColors.Red, pixels.GetPixel(20, 20));
            Assert.Equal(SKColors.Black, pixels.GetPixel(40, 40));
        }

        Assert.Equal(bounds, drawing.ScreenBounds);

        // Growing the viewport reveals the previously clipped part without
        // recreating the drawing or assigning its ScreenBounds again.
        view.Viewport.TargetRectPx = new Rectangle(0, 0, 64, 64);
        host.RenderToBackbuffer(2);

        using (var image = buffer.Snapshot())
        using (var pixels = SKBitmap.FromImage(image))
        {
            Assert.Equal(SKColors.Red, pixels.GetPixel(20, 20));
            Assert.Equal(SKColors.Red, pixels.GetPixel(40, 40));
            Assert.Equal(SKColors.Black, pixels.GetPixel(55, 55));
        }

        Assert.Equal(bounds, drawing.ScreenBounds);
        Assert.Equal(new Rectangle(8, 8, 40, 12), text.ScreenBounds);
    }

    [Fact]
    public void ConstructorAndSetterPreservePartiallyOutsideBounds()
    {
        using var host = new TestRenderSurfaceHost();
        var viewport = new Rectangle(10, 10, 40, 40);
        host.ViewManager.AddView(viewport, zOrder: 0);
        var view = Assert.Single(host.ViewManager.Views, v => v.Viewport.TargetRectPx == viewport);
        var bounds = new Rectangle(0, 20, 30, 40);

        using var drawing = new DirectRectangle(Color.White, host, view, bounds);
        Assert.Equal(bounds, drawing.ScreenBounds);

        var movedBounds = new Rectangle(35, 35, 30, 30);
        drawing.ScreenBounds = movedBounds;
        Assert.Equal(movedBounds, drawing.ScreenBounds);
    }
}
