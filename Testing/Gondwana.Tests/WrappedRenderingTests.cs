using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Widgets;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Effects rendering")]
public sealed class WrappedRenderingTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Render_RepeatsLayerDrawings_RefreshesAllCopies_AndKeepsViewUiFixed(bool gpu, bool horizontal, bool vertical)
    {
        if (gpu) Render<GpuBackbuffer>(horizontal, vertical);
        else Render<BitmapBackbuffer>(horizontal, vertical);
    }

    private static void Render<T>(bool horizontal, bool vertical) where T : BackbufferBase
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = horizontal;
        layer.WrapVertically = vertical;
        using var host = new RenderSurfaceHost<T>(new Adapter());
        host.Bind(scene, limitCameraToWorldBoundPx: false);
        var view = Assert.Single(host.ViewManager.Views);
        view.Camera.SnapTo(new(-32, -32));
        using var drawing = new DirectRectangle(Color.Red, host, layer, new(2, 2, 8, 8)).SetFilled(true);
        using var overlay = new DirectRectangle(Color.Blue, host, view, new(0, 0, 4, 4)).SetFilled(true);
        host.RenderToBackbuffer(0);
        host.Backbuffer.EndFrame();
        using (var image = host.Backbuffer.Snapshot())
        using (var bitmap = SKBitmap.FromImage(image))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(1, 1));
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    Assert.Equal((horizontal || x == 1) && (vertical || y == 1) ? SKColors.Red : SKColors.Black,
                        bitmap.GetPixel(x * 32 + 5, y * 32 + 5));
            Assert.Equal(SKColors.Black, bitmap.GetPixel(33, 33));
        }
        host.Backbuffer.BeginFrame();
        drawing.Visible = false;
        host.RenderToBackbuffer(1);
        host.Backbuffer.EndFrame();
        using (var image = host.Backbuffer.Snapshot())
        using (var bitmap = SKBitmap.FromImage(image))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(1, 1));
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    Assert.Equal(SKColors.Black, bitmap.GetPixel(x * 32 + 5, y * 32 + 5));
        }
        host.Backbuffer.BeginFrame();
    }

    [Fact]
    public void Widget_HitTestsWrappedCopies_AndKeepsCanonicalOwner()
    {
        using var host = new TestRenderSurfaceHost();
        var layer = host.Scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = layer.WrapVertically = true;
        host.ViewManager.AddView(new(0, 0, 128, 128));
        var view = host.ViewManager.Views[0];
        using var widget = new TestWidget(host, layer);
        Assert.True(widget.HitTest(view, new(69, 101)));
        Assert.True(widget.HitTest(view, new(5, 5)));
        Assert.False(widget.HitTest(view, new(15, 15)));

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerCapture_RetainsSelectedInstance_AndDoesNotClickAnotherCopy(bool drag)
    {
        using var host = new TestRenderSurfaceHost();
        var layer = host.Scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = layer.WrapVertically = true;
        host.ViewManager.AddView(new(0, 0, 128, 128));
        using var widget = new TestWidget(host, layer);
        widget.IsDragEnabled = drag;
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Register(widget);
        object? Call(string method, params object?[] args) => typeof(WidgetInputRouter)
            .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(router, args);
        var hit = Call("HitTest", new Point(69, 101));
        PointF captured = PointF.Empty;
        int clicks = 0;
        widget.PointerMove += args => { Assert.Same(widget, args.Widget); captured = args.WrappedOffsetWorldPx; };
        widget.PointerClick += _ => clicks++;
        Call("ProcessMouseDown", hit, new Point(69, 101), Gondwana.Input.Mouse.MouseButton.Left, 0L);
        Call("ProcessMouseMove", new Point(69, 101), new Point(5, 5), 1L);
        Assert.Equal(new PointF(64, 96), captured);
        Assert.Equal(drag ? new System.Numerics.Vector2(-62, -94) : new System.Numerics.Vector2(2, 2), widget.GetPosition());
        Call("ProcessMouseUp", new Point(5, 5), Gondwana.Input.Mouse.MouseButton.Left, 2L);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void FogAndCollisionDebug_AreTranslatedWithTileInstances()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        layer.WrapHorizontally = layer.WrapVertically = true;
        layer.ShowCollisionBoxes = true;
        layer[0, 0]!.EnableFog = true;
        layer[0, 0]!.CollisionsEnabled = true;
        var camera = new Gondwana.Rendering.Views.Camera(scene);
        var view = new Gondwana.Rendering.Views.View(camera, new Gondwana.Rendering.Views.Viewport { TargetRectPx = new(0, 0, 64, 64) });
        using var buffer = new BitmapBackbuffer(64, 64);
        buffer.FogPaint.Color = SKColors.Red;
        buffer.DrawDrawables(view, layer.GetDrawablesInWorldRect(new(0, 0, 64, 64)), new(0, 0, 64, 64));
        using var image = buffer.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(40, 40));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(8, 8));
        Assert.True(bitmap.GetPixel(32, 40).Green > 0);
    }

    private sealed class TestWidget : DraggableWidgetBase
    {
        internal TestWidget(RenderSurfaceHostBase host, SceneLayer layer)
            : base(host, DirectDrawingMode.SceneLayer, new(2, 2))
        {
            Add(new DirectRectangle(Color.White, host, layer, new(2, 2, 8, 8)).SetFilled(true));
        }
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(128, 128)
    {
        public override void Present(SKImage image, SKRectI source, SKRect destination) { }
    }
}
