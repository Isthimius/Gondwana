using System.Drawing;
using Gondwana.Drawing;
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
    [Fact]
    public void ProfilerObservesBuildAndReplayWithoutChangingMailboxOrLegacySubscribers()
    {
        var profiler = Engine.Instance.Profiler;
        profiler.Reset();
        using var scene = new Scene();
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var buffer = host.Backbuffer;
        host.Bind(scene, false);
        int legacy = 0;
        host.GpuRenderFrameDiagnosticsCalculated += _ => legacy++;
        using (profiler.Start())
        {
            host.ProduceRenderFrameSnapshot(1);
            using var image = host.GlRenderAndSnapshot();
        }
        var source = profiler.GetLatestSnapshot()!.Sources.Single(s => s.Id == host.Telemetry!.Id);
        Assert.Equal(1, source.Metrics["build.cpu.ms"].Count);
        Assert.Equal(1, source.Metrics["replay.cpu.ms"].Count);
        Assert.Equal(Gondwana.Diagnostics.TelemetryAvailability.NotApplicable, source.Metrics["gate.wait.cpu.ms"].Availability);
        var counters = host.FrameMailbox.Counters;
        profiler.Reset();
        Assert.Equal(counters, host.FrameMailbox.Counters);
        host.ProduceRenderFrameSnapshot(2);
        Assert.Equal(2, legacy);
    }

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
    public void BulkGridRecording_MatchesLegacyDrawing_WithZoomCameraAndOverhang()
    {
        var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.CornflowerBlue);

        using var sheet = new Tilesheet("bulk-grid-parity", bitmap);
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        sheet.DefaultRegion.Overhang = new Spacing(2, 2, 2, 2);

        using var scene = new Scene();
        var layer = scene.AddLayer(6, 4, 16, 16);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 6; x++)
                layer[x, y]!.CurrentFrame = sheet.GetFrame(0, 0);

        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);

        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.SnapZoom(1.5f);
        view.Camera.SnapTo(new PointF(8f, 6f));

        host.RenderToBackbuffer(1);
        using var expectedImage = backbuffer.Snapshot();
        using var expected = SKBitmap.FromImage(expectedImage);

        backbuffer.Canvas.Clear(SKColors.Transparent);
        host.ProduceRenderFrameSnapshot(2);
        using var actualImage = host.GlRenderAndSnapshot();
        using var actual = SKBitmap.FromImage(actualImage!);

        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    [Fact]
    public void FractionalZoomAtlasTiles_DoNotExposeBackgroundSeams()
    {
        using var bitmap = CreateOpaqueAtlas();

        using var sheet = new Tilesheet("fractional-zoom-seam-test", bitmap);
        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var scene = new Scene();
        var layer = scene.AddLayer(40, 30, 16, 16);
        PopulateOpaqueAtlasGrid(layer, sheet);

        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);

        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.SnapZoom(0.694f);
        view.Camera.SnapTo(new PointF(-13.2f, -7.6f));

        backbuffer.ClearColor = SKColors.Black;
        host.ProduceRenderFrameSnapshot(1);

        using var image = host.GlRenderAndSnapshot();
        using var actual = SKBitmap.FromImage(image!);

        AssertGridInteriorContainsNoBlackPixels(actual, layer, view);
    }

    [Fact]
    public void FractionalZoomAtlasTiles_WithTransformedException_DoNotExposeBackgroundSeams()
    {
        using var bitmap = CreateOpaqueAtlas();

        using var sheet = new Tilesheet("fractional-zoom-seam-mixed-test", bitmap);
        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var scene = new Scene();
        var layer = scene.AddLayer(40, 30, 16, 16);
        PopulateOpaqueAtlasGrid(layer, sheet);

        // Force the materialized fixed-grid path used by real scenes that contain
        // a handful of transformed tiles, while keeping the visual result identical.
        layer[20, 15]!.Transform = TileTransform.Rotate90;

        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);

        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.SnapZoom(0.694f);
        view.Camera.SnapTo(new PointF(-13.2f, -7.6f));

        backbuffer.ClearColor = SKColors.Black;
        host.ProduceRenderFrameSnapshot(1);

        using var image = host.GlRenderAndSnapshot();
        using var actual = SKBitmap.FromImage(image!);

        AssertGridInteriorContainsNoBlackPixels(actual, layer, view);
    }

    private static SKBitmap CreateOpaqueAtlas()
    {
        var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();

        SKColor[] colors =
        [
            SKColors.Red,
            SKColors.Green,
            SKColors.Blue,
            SKColors.Yellow
        ];

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                paint.Color = colors[y * 2 + x];
                canvas.DrawRect(
                    x * 16,
                    y * 16,
                    16,
                    16,
                    paint);
            }
        }

        return bitmap;
    }

    private static void PopulateOpaqueAtlasGrid(
        SceneLayer layer,
        Tilesheet sheet)
    {
        Frame[] frames =
        [
            sheet.GetFrame(0, 0),
            sheet.GetFrame(1, 0),
            sheet.GetFrame(0, 1),
            sheet.GetFrame(1, 1)
        ];

        for (int y = 0; y < layer.GridRowCount; y++)
            for (int x = 0; x < layer.GridColumnCount; x++)
                layer[x, y]!.CurrentFrame = frames[(x + y) & 3];
    }

    private static void AssertGridInteriorContainsNoBlackPixels(
        SKBitmap bitmap,
        SceneLayer layer,
        Gondwana.Rendering.Views.View view)
    {
        RectangleF first = layer[0, 0]!.GetDrawLocationScreen(view);
        RectangleF last =
            layer[layer.GridColumnCount - 1, layer.GridRowCount - 1]!
                .GetDrawLocationScreen(view);

        int left = Math.Max(
            0,
            (int)MathF.Round(first.Left, MidpointRounding.AwayFromZero) + 1);
        int top = Math.Max(
            0,
            (int)MathF.Round(first.Top, MidpointRounding.AwayFromZero) + 1);
        int right = Math.Min(
            bitmap.Width - 1,
            (int)MathF.Round(last.Right, MidpointRounding.AwayFromZero) - 1);
        int bottom = Math.Min(
            bitmap.Height - 1,
            (int)MathF.Round(last.Bottom, MidpointRounding.AwayFromZero) - 1);

        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                Assert.NotEqual(SKColors.Black, bitmap.GetPixel(x, y));
    }

    [Fact]
    public void EmptyGridCells_AreCulledUnlessPostDrawGeometryIsNeeded()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(8, 1, 16, 16);
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var backbuffer = host.Backbuffer;
        host.Bind(scene, false);

        GpuRenderFrameDiagnostics? diagnostics = null;
        host.GpuRenderFrameDiagnosticsCalculated += value => diagnostics = value;

        host.ProduceRenderFrameSnapshot(1);
        Assert.NotNull(diagnostics);
        Assert.Equal(0, diagnostics.TileCount);

        layer.ShowGridLines = true;
        host.ProduceRenderFrameSnapshot(2);
        Assert.Equal(8, diagnostics.TileCount);
    }

    [Fact]
    public void FixedGridSnapshotEligibility_InvalidatesOnZOrderAndFogChanges()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 1, 16, 16);
        var tile = layer[0, 0]!;

        Assert.True(layer.IsFixedGridSnapshotFastPathEligible);

        tile.ZOrder = 1;
        Assert.False(layer.IsFixedGridSnapshotFastPathEligible);

        tile.ZOrder = 0;
        Assert.True(layer.IsFixedGridSnapshotFastPathEligible);

        tile.EnableFog = true;
        Assert.False(layer.IsFixedGridSnapshotFastPathEligible);

        tile.EnableFog = false;
        Assert.True(layer.IsFixedGridSnapshotFastPathEligible);
    }

    [Fact]
    public void DirectTileArrayExposure_DisablesPersistentFixedGridFastPath()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);

        Assert.True(layer.IsFixedGridSnapshotFastPathEligible);

        _ = layer.SceneLayerTileArray;

        Assert.False(layer.IsFixedGridSnapshotFastPathEligible);
    }

    [Fact]
    public void OrthogonalFastOrder_FallsBackWhenFixedTileZOrdersDiffer()
    {
        var bitmap = new SKBitmap(16, 16);
        using var sheet = new Tilesheet("sort-order-test", bitmap);
        sheet.DefaultRegion.TileSize = new Size(16, 16);

        using var scene = new Scene();
        var layer = scene.AddLayer(2, 1, 16, 16);
        var first = layer[0, 0]!;
        var second = layer[1, 0]!;
        first.CurrentFrame = sheet.GetFrame(0, 0);
        second.CurrentFrame = sheet.GetFrame(0, 0);

        ((Tile)first).ZOrder = 10;
        ((Tile)second).ZOrder = 0;

        var drawables = layer.GetDrawablesInWorldRect(new Rectangle(0, 0, 32, 16));

        Assert.Equal(2, drawables.Count);
        Assert.Same(second, drawables[0]);
        Assert.Same(first, drawables[1]);
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
