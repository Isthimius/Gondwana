using System.Diagnostics;
using System.Reflection;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;
using Xunit.Abstractions;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

/// <summary>Opt-in hardware check: GONDWANA_GPU_TESTS=1 dotnet test --filter DesktopGpuSnapshotTests.</summary>
public sealed class DesktopGpuSnapshotTests(ITestOutputHelper output)
{
    private sealed class HardwareFactAttribute : FactAttribute
    {
        public HardwareFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GONDWANA_GPU_TESTS") != "1")
                Skip = "Requires an interactive Windows desktop with an OpenGL driver; set GONDWANA_GPU_TESTS=1.";
        }
    }

    [HardwareFact]
    public async Task NativeReplaySurvivesMsaaResizeAndContextReplacement()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Run(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    private void Run()
    {
        using var control = new OpenTK.GLControl { Width = 128, Height = 128 };
        _ = control.Handle;
        control.MakeCurrent();
        using var gl = GRGlInterface.Create();
        using var context = GRContext.CreateGl(gl);
        Assert.NotNull(context);
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new Adapter());
        using var buffer = (GpuBackbuffer)host.Backbuffer;
        using var scene = new Scene();
        host.Bind(scene, false);
        host.RenderBackbufferPostScene += canvas => canvas.Clear(SKColors.Red);
        buffer.EnsureInitialized(context);
        Produce(host);
        AssertRed(host);

        Produce(host);
        buffer.MsaaSampleCount = 4;
        Assert.True(buffer.EnsureInitialized(context));
        AssertRed(host);

        // The retained recording is independent of the context used to replay it.
        Produce(host);
        using var replacement = GRContext.CreateGl(gl);
        Assert.True(buffer.EnsureInitialized(replacement));
        AssertRed(host);

        Produce(host);
        typeof(GpuBackbuffer).GetMethod("RequestResize", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(buffer, [64, 64]);
        Assert.True(buffer.EnsureInitialized(replacement));
        using (var rejected = host.GlRenderAndSnapshot())
        using (var pixels = SKBitmap.FromImage(rejected!))
            Assert.Equal(buffer.ClearColor, pixels.GetPixel(1, 1));
        Produce(host);
        AssertRed(host);
        output.WriteLine($"Native GL replay passed; MSAA actual/max {buffer.ActualMsaaSampleCount}/{buffer.MaxSupportedMsaaSampleCount}.");
        // Dispose GPU resources before their current context.
        buffer.Dispose();
    }

    private static void Produce(RenderSurfaceHost<GpuBackbuffer> host) =>
        typeof(RenderSurfaceHost<GpuBackbuffer>)
            .GetMethod("ProduceRenderFrameSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(host, [Stopwatch.GetTimestamp()]);

    private static void AssertRed(RenderSurfaceHost<GpuBackbuffer> host)
    {
        using var image = host.GlRenderAndSnapshot();
        using var pixels = SKBitmap.FromImage(image!);
        Assert.Equal(SKColors.Red, pixels.GetPixel(1, 1));
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(128, 128)
    {
        public override void Present(SKImage image, SKRectI source, SKRect destination) { }
    }
}
