using System.Diagnostics;
using System.Reflection;
using Gondwana.Drawing.Coordinates;
using Gondwana.WinForms.Rendering;
using Xunit.Abstractions;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

/// <summary>Opt-in comparable baseline/revised native workload; reflection keeps it compilable on master.</summary>
/// <param name="output">Test output for measured results, without performance assertions.</param>
public sealed class RuntimeTelemetryOverheadTests(ITestOutputHelper output)
{
    /// <summary>Measures actual simulation work and added telemetry cost without native rendering.</summary>
    [BenchmarkFact]
    public void MeasureEngineLoop()
    {
        string mode = Environment.GetEnvironmentVariable("GONDWANA_TELEMETRY_MODE") ?? "disabled";
        var engine = (Engine)Activator.CreateInstance(typeof(Engine), nonPublic: true)!;
        typeof(Engine).Assembly
            .GetType("Gondwana.Configuration.EngineConfiguration")?
            .GetProperty("LegacyCpsSamplingTime", BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(engine.Configuration, 0d);
        typeof(Engine).Assembly
            .GetType("Gondwana.Configuration.EngineConfiguration")?
            .GetProperty("SamplingTimeForCPS")?
            .SetValue(engine.Configuration, 0d);
        var cycle = (Action<long, double, bool, long, double, bool>)typeof(Engine)
            .GetMethod("RunSimulationCycle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Action<long, double, bool, long, double, bool>), engine);
        IDisposable? request = null;
        if (mode == "collecting")
        {
            dynamic profiler = typeof(Engine).GetProperty("Profiler")!.GetValue(engine)!;
            request = profiler.Start();
        }
        try
        {
            for (int i = 0; i < 100000; i++) cycle(i, .001, false, i, .001, false);
            for (int run = 0; run < 3; run++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long tick = Stopwatch.GetTimestamp();
                for (int i = 0; i < 1000000; i++) cycle(i, .001, false, i, .001, false);
                double ns = (Stopwatch.GetTimestamp() - tick) * 1000d / Stopwatch.Frequency;
                output.WriteLine($"ENGINE mode={mode} run={run} ns_per_cycle={ns:F2} bytes_per_cycle={(GC.GetAllocatedBytesForCurrentThread() - bytes) / 1000000d:F4}");
            }
        }
        finally { request?.Dispose(); GC.SuppressFinalize(engine); }
    }

    private sealed class BenchmarkFactAttribute : FactAttribute
    {
        public BenchmarkFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GONDWANA_TELEMETRY_BENCH") != "1")
                Skip = "Opt-in Release native benchmark; run separately from functional tests.";
        }
    }

    /// <summary>Measures native stress-scene work after warmup with collection disabled, collecting, or displayed.</summary>
    [BenchmarkFact]
    public async Task MeasureNativeViewer()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Run(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(90));
    }

    private void Run()
    {
        string mode = Environment.GetEnvironmentVariable("GONDWANA_TELEMETRY_MODE") ?? "disabled";
        int tiles = int.Parse(Environment.GetEnvironmentVariable("GONDWANA_TELEMETRY_TILES") ?? "1024");
        int warmupMilliseconds = int.Parse(Environment.GetEnvironmentVariable("GONDWANA_TELEMETRY_WARMUP_MS") ?? "3000");
        using var form = new Form
        {
            ClientSize = new Size(1024, 768),
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000)
        };
        using var surface = new WinFormGpuRenderSurfaceControl { Dock = DockStyle.Fill };
        form.Controls.Add(surface);
        SceneViewerGameHost? host = null;
        IDisposable? request = null;
        Exception? failure = null;
        long cycles = 0, frames = 0;
        Engine.Instance.AfterBackgroundTasksExecute += () => Interlocked.Increment(ref cycles);
        Engine.Instance.AfterFrameRender += () => Interlocked.Increment(ref frames);
        using var process = Process.GetCurrentProcess();
        Action? sample = null;
        using var timer = new System.Threading.Timer(_ => sample?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);
        int phase = 0;
        long retainedAtWarmup = 0;
        long allocation = 0, ticks = 0, lastCycles = 0, lastFrames = 0;
        TimeSpan cpu = default;
        form.Shown += (_, _) =>
        {
            try
            {
                host = new SceneViewerGameHost(surface, null, new(tiles, CoordinateSystemTypes.Orthogonal));
                host.InitializeWithConfigurationStore(new SceneViewerGameHost.ViewerConfiguration());
                Engine.Instance.EngineDispatcher.Post(() =>
                {
                    try
                    {
                        if (mode == "collecting")
                        {
                            object profiler = typeof(Engine).GetProperty("Profiler")!.GetValue(Engine.Instance)!;
                            request = (IDisposable)profiler.GetType().GetMethod("Start")!.Invoke(profiler, null)!;
                        }
                        if (mode == "display")
                            typeof(SceneViewerGameHost).GetMethod("ToggleDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null);
                    }
                    catch (Exception error) { failure = error; form.BeginInvoke(() => form.Close()); }
                });
                timer.Change(warmupMilliseconds, Timeout.Infinite);
            }
            catch (Exception error) { failure = error; form.Close(); }
        };
        sample = () =>
        {
            try
            {
                if (phase == 0) retainedAtWarmup = GC.GetTotalMemory(true);
                long now = Stopwatch.GetTimestamp();
                long allocated = GC.GetTotalAllocatedBytes(false);
                var currentCpu = process.TotalProcessorTime;
                if (phase > 0)
                {
                    double seconds = (now - ticks) / (double)Stopwatch.Frequency;
                    output.WriteLine($"BENCH mode={mode} tiles={tiles} run={phase} seconds={seconds:F3} cpu_ms={(currentCpu - cpu).TotalMilliseconds:F1} alloc_Bps={(allocated - allocation) / seconds:F0} heap_bytes={GC.GetTotalMemory(false)} cycles_ps={(cycles - lastCycles) / seconds:F0} foreground_ps={(frames - lastFrames) / seconds:F1}");
                    if (mode != "disabled")
                    {
                        dynamic profiler = typeof(Engine).GetProperty("Profiler")!.GetValue(Engine.Instance)!;
                        dynamic snapshot = profiler.GetLatestSnapshot();
                        Assert.NotNull((object?)snapshot);
                        foreach (dynamic source in snapshot.Sources)
                            foreach (string metric in new[] { "cycle.cpu.ms", "background.cpu.ms", "build.cpu.ms", "replay.cpu.ms" })
                                if (source.Metrics.ContainsKey(metric))
                                    output.WriteLine($"LAST_WINDOW source={source.Id} metric={metric} mean_ms={source.Metrics[metric].Mean} count={source.Metrics[metric].Count}");
                    }
                    if (warmupMilliseconds >= 30000)
                        output.WriteLine($"RETAINED_FULL_HISTORY run={phase} bytes={GC.GetTotalMemory(true)}");
                }
                if (++phase == 4)
                {
                    output.WriteLine($"RETAINED warmup_bytes={retainedAtWarmup} end_bytes={GC.GetTotalMemory(true)}");
                    form.BeginInvoke(() => form.Close());
                    return;
                }
                ticks = now; allocation = allocated; cpu = currentCpu; lastCycles = cycles; lastFrames = frames;
                timer.Change(5000, Timeout.Infinite);
            }
            catch (Exception error)
            {
                failure = error;
                try
                {
                    if (!form.IsDisposed && form.IsHandleCreated)
                        form.BeginInvoke(() => form.Close());
                }
                catch (Exception closeError)
                {
                    failure = new AggregateException(error, closeError);
                }
            }
        };
        try { Application.Run(form); }
        finally { request?.Dispose(); host?.Dispose(); }
        if (failure is not null) throw failure;
    }
}
