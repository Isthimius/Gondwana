using System.Diagnostics;
using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Tests.Drawing.Direct;
using Gondwana.Video;
using Gondwana.Video.Widgets;
using SkiaSharp;

namespace Gondwana.Tests.Widgets;

[Collection("Effects rendering")]
public sealed class VideoWidgetRenderingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RegisteredVideoRendersMovesAndHidesThroughNormalPipeline(bool gpu, bool world)
    {
        if (gpu) Render<GpuBackbuffer>(world);
        else Render<BitmapBackbuffer>(world);
    }

    private static void Render<T>(bool world) where T : BackbufferBase
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4, 16, 16);
        using var host = new RenderSurfaceHost<T>(new Adapter());
        using var buffer = host.Backbuffer;
        host.Bind(scene, limitCameraToWorldBoundPx: false);
        var view = Assert.Single(host.ViewManager.Views);
        var player = new FakeVideoPlayer();
        var source = VideoSource.FromUri(new Uri("file:///test.mp4"));
        using var widget = world
            ? new VideoWidget(host, layer, new Rectangle(2, 2, 8, 8), source, player)
            : new VideoWidget(host, view, new Rectangle(2, 2, 8, 8), source, player);
        widget.Show();
        player.Emit([0, 0, 255, 0], 1, 1, 4);
        widget.Video.Update(Stopwatch.GetTimestamp());

        SKColor Pixel(int x, int y)
        {
            host.RenderToBackbuffer(Stopwatch.GetTimestamp());
            buffer.EndFrame();
            using var image = buffer.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            var result = bitmap.GetPixel(x, y);
            buffer.BeginFrame();
            return result;
        }

        Assert.Equal(SKColors.Red, Pixel(4, 4));
        widget.SetPosition(20, 20);
        Assert.Equal(SKColors.Black, Pixel(4, 4));
        Assert.Equal(SKColors.Red, Pixel(22, 22));
        widget.Hide();
        Assert.Equal(SKColors.Black, Pixel(22, 22));
        Assert.True(widget.IsPlaying);
        widget.Show();
        Assert.Equal(SKColors.Red, Pixel(22, 22));
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(64, 64)
    {
        public override void Present(SKImage image, SKRectI source, SKRect destination) { }
    }
}
