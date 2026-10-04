using System.Reflection;
using Gondwana.Drawing.Direct;
using Gondwana.WinForms.Rendering;
using Xunit.Abstractions;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

/// <summary>Opt-in, process-isolated Scene Viewer run using the actual Engine and platform paint loop.</summary>
/// <param name="output">The xUnit output sink used to report Scene Viewer dogfood diagnostics.</param>
public sealed class SceneViewerSnapshotDogfoodTests(ITestOutputHelper output)
{
    private sealed class DogfoodFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DogfoodFactAttribute"/>.
        /// </summary>
        public DogfoodFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GONDWANA_VIEWER_DOGFOOD") != "1")
                Skip = "Run alone with GONDWANA_VIEWER_DOGFOOD=1 and GONDWANA_VIEWER_SCENE set to island.gscn.";
        }
    }

    /// <summary>
    /// Verifies that island running and paused.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [DogfoodFact]
    public async Task IslandRunningAndPaused()
    {
        var scene = Environment.GetEnvironmentVariable("GONDWANA_VIEWER_SCENE")!;
        Assert.True(File.Exists(scene));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Run(scene); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    private void Run(string scene)
    {
        using var form = new Form()
        {
            ClientSize = new Size(1024, 768),
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000)
        };
        using var surface = new WinFormGpuRenderSurfaceControl { Dock = DockStyle.Fill };
        form.Controls.Add(surface);
        SceneViewerGameHost? gameHost = null;
        Action? takeSample = null;
        using var timer = new System.Threading.Timer(_ => takeSample?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);
        string running = "", paused = "", stalled = "";
        bool captured = false;
        int phase = 0;
        Exception? failure = null;
        form.Shown += (_, _) =>
        {
            SceneViewerGameHost host;
            try
            {
                host = gameHost = new SceneViewerGameHost(surface, scene);
                host.InitializeWithConfigurationStore(new SceneViewerGameHost.ViewerConfiguration());
            }
            catch (Exception exception)
            {
                failure = exception;
                form.Close();
                return;
            }
            Engine.Instance.EngineDispatcher.Post(() =>
            {
                surface.Host.ViewManager.Views[0].Viewport.Zoom = .125f;
                typeof(SceneViewerGameHost).GetMethod("ToggleDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null);
            });
            surface.Adapter.FrameDiagnosticsCalculated += _ =>
            {
                if (Volatile.Read(ref phase) == 2) Thread.Sleep(100); // deliberate slow consumer
                var capturePath = Environment.GetEnvironmentVariable("GONDWANA_GPU_CAPTURE");
                if (!captured && Volatile.Read(ref phase) == 1 && capturePath is not null)
                {
                    using var image = surface.Host.GlSnapshotCurrentFrame();
                    using var encoded = image!.Encode(global::SkiaSharp.SKEncodedImageFormat.Png, 100);
                    using var file = File.Create(capturePath);
                    encoded.SaveTo(file);
                    captured = true;
                }
            };
            takeSample = () =>
            {
                // Capture the F3 text on its owner thread before changing phase.
                Engine.Instance.EngineDispatcher.Post(() =>
                {
                    try
                    {
                        var text = (TextBlock)typeof(SceneViewerGameHost)
                            .GetField("_diagnosticsText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                        if (phase == 0)
                        {
                            running = text.Text;
                            typeof(SceneViewerGameHost).GetMethod("ToggleAnimations", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null);
                            Volatile.Write(ref phase, 1);
                        }
                        else if (phase == 1)
                        {
                            paused = text.Text;
                            Volatile.Write(ref phase, 2);
                        }
                        else
                        {
                            stalled = text.Text;
                            form.BeginInvoke(() => form.Close());
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        form.BeginInvoke(() => form.Close());
                    }
                });
            };
            timer.Change(6000, 6000);
        };
        try { Application.Run(form); }
        finally { gameHost?.Dispose(); }
        if (failure is not null) throw failure;
        output.WriteLine("RUNNING\n" + running);
        output.WriteLine("PAUSED\n" + paused);
        output.WriteLine("STALLED GL (100 ms/callback)\n" + stalled);
        Assert.Contains("running", running);
        Assert.Contains("PAUSED", paused);
        Assert.Contains("Snapshot build", paused);
    }
}
