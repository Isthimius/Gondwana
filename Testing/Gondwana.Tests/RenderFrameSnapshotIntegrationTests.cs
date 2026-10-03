using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Effects;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Effects rendering")]
public sealed class RenderFrameSnapshotIntegrationTests
{
    public RenderFrameSnapshotIntegrationTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
    }

    [Fact]
    public void ReplayMatchesLiveComposition_AndSurvivesSceneAndDrawingDisposal()
    {
        using var scene = new Scene();
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        var layer = scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = true;
        layer.WrapVertically = true;
        host.Bind(scene, false);
        var view = Assert.Single(host.ViewManager.Views);
        view.Camera.SnapTo(new(-32, -32));
        using var drawing = new DirectRectangle(Color.Red, host, layer, new(2, 2, 8, 8)).SetFilled(true);
        using var overlay = new DirectRectangle(Color.Blue, host, view, new(0, 0, 4, 4)).SetFilled(true);
        host.Effects.Run(layer, new FadeOutEffect(1));
        host.Effects.Advance(0.5f);
        // DirectRectangle lazily initializes stroke join/cap on its first draw.
        host.RenderToBackbuffer(0);
        host.RenderToBackbuffer(1);
        using var reference = backbuffer.Snapshot();
        using var expected = SKBitmap.FromImage(reference);
        host.ProduceRenderFrameSnapshot(1);
        drawing.Dispose();
        overlay.Dispose();
        scene.Dispose();
        backbuffer.Canvas.Clear(SKColors.Green);
        using var image = host.GlRenderAndSnapshot();
        using var actual = SKBitmap.FromImage(image!);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal(0, host.FrameMailbox.Counters.InUse);
    }

    [Fact]
    public void AtlasBatchedGridTiles_MatchLegacyDrawing_AndReportBatching()
    {
        var bitmap = new SKBitmap(32, 16);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Red })
        {
            canvas.DrawRect(new SKRect(0, 0, 16, 16), paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(new SKRect(16, 0, 32, 16), paint);
        }

        using var sheet = new Tilesheet("atlas-batch-test", bitmap);
        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var scene = new Scene();
        var layer = scene.AddLayer(8, 1, 16, 16);
        for (int x = 0; x < 8; x++)
            layer[x, 0]!.CurrentFrame = sheet.GetFrame(x % 2, 0);

        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);

        host.RenderToBackbuffer(1);
        using var expectedImage = backbuffer.Snapshot();
        using var expected = SKBitmap.FromImage(expectedImage);

        GpuRenderFrameDiagnostics? diagnostics = null;
        host.GpuRenderFrameDiagnosticsCalculated += value => diagnostics = value;

        backbuffer.Canvas.Clear(SKColors.Transparent);
        host.ProduceRenderFrameSnapshot(2);

        using var actualImage = host.GlRenderAndSnapshot();
        using var actual = SKBitmap.FromImage(actualImage!);

        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.AtlasBatchCount > 0);
        Assert.Equal(8, diagnostics.AtlasBatchedTileCount);
    }

    [Fact]
    public async Task ReplayDoesNotAcquireSimulationGate_OrInvokeLiveCallbacks()
    {
        using var scene = new Scene();
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);
        int calls = 0;
        host.RenderBackbufferPostScene += canvas =>
        {
            calls++;
            canvas.Clear(SKColors.Red);
        };
        host.ProduceRenderFrameSnapshot(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var blocker = Task.Run(() =>
        {
            lock (RenderStateSynchronization.SyncRoot)
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(20));
            }
        });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try
        {
            await Task.Run(() =>
            {
                using var image = host.GlRenderAndSnapshot();
                using var pixels = SKBitmap.FromImage(image!);
                Assert.Equal(SKColors.Red, pixels.GetPixel(1, 1));
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, calls);
        }
        finally { release.Set(); await blocker; }
    }

    [Fact]
    public void FailedRecordingReleasesBuildingSlot()
    {
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var buffer = host.Backbuffer;
        host.RenderBackbufferBegin += () => throw new InvalidOperationException("test");
        Assert.Throws<InvalidOperationException>(() => host.ProduceRenderFrameSnapshot(1));
        Assert.Equal(0, host.FrameMailbox.Counters.InUse);
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(128, 128)
    {
        public override void Present(SKImage image, SKRectI source, SKRect destination) { }
    }
}
