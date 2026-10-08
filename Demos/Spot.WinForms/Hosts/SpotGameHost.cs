using Gondwana.Assets;
using Gondwana.Diagnostics;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Hosts the Spot demo on Gondwana's WinForms GPU runtime and delegates game behavior
/// to the platform-neutral runtime in Spot.Shared.
/// </summary>
internal sealed partial class SpotGameHost : WinFormsGpuGameHost
{
    private readonly SpotGameRuntime _runtime;
    private readonly AssetsFile _assets = LoadAssetPackage();
    private IDisposable? _profilerRequest;
    private double _lastProfilerLogSeconds;

    private static AssetsFile LoadAssetPackage()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "spot.gaf"));
        return AssetsFile.Load(stream, register: false);
    }

    /// <inheritdoc/>
    protected override void OnDisposed()
    {
        _assets.Dispose();
        _font?.Dispose();
        base.OnDisposed();
    }

    internal SpotGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
        _runtime = new SpotGameRuntime(
            renderSurface.Host,
            scoreToggleKey: 9,
            widgetInputRouterAccessor: () => WidgetInputRouter,
            configurePlatformKeyboardInput: ConfigurePlatformKeyboardInput,
            persistGameState: PersistGameState);
    }

    internal NewGameOptions? LastNewGameOptions => _runtime.LastNewGameOptions;

    internal void BeginPostSplashStartup() => _runtime.BeginPostSplashStartup();

    internal void OpenNewGameDialog(NewGameOptions? options = null)
        => _runtime.OpenNewGameDialog(options);

    internal void SetMusicEnabled(bool enabled) => _runtime.SetMusicEnabled(enabled);

    internal void SetSoundEffectsEnabled(bool enabled) => _runtime.SetSoundEffectsEnabled(enabled);

    internal void SetJiggleEnabled(bool enabled) => _runtime.SetJiggleEnabled(enabled);

    internal void SetCloudsEnabled(bool enabled) => _runtime.SetCloudsEnabled(enabled);

    /// <inheritdoc/>
    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(LogLevel.Information);

        _profilerRequest = Engine.Profiler.Start();
        Engine.AfterBackgroundTasksExecute += LogRuntimeProfilerSample;

        return _runtime.CreateInitialScene();
    }

    private void LogRuntimeProfilerSample()
    {
        var snapshot = Engine.Profiler.GetLatestSnapshot();
        if (snapshot is null ||
            snapshot.EndedSeconds - _lastProfilerLogSeconds < 1.5d)
        {
            return;
        }

        _lastProfilerLogSeconds = snapshot.EndedSeconds;

        var engineSource = snapshot.Sources.FirstOrDefault(source => source.Backend == "Core");
        var renderSource = snapshot.Sources.FirstOrDefault(
            source => source.Id == RenderSurface.Host.Telemetry?.Id);

        double? cps = GetRate(engineSource, "cycle.cpu.ms", snapshot.ElapsedSeconds);
        double? engineFps = GetRate(engineSource, "foreground.cpu.ms", snapshot.ElapsedSeconds);
        double? gpuFps = GetRate(renderSource, "presentation.count", snapshot.ElapsedSeconds);

        if (RenderSurface.Host.Backbuffer is GpuBackbuffer gpuBackbuffer)
        {
            Engine.Logger.LogInformation(
                "CPS {Cps} | engine FPS {EngineFps} | GPU FPS {GpuFps} | " +
                "MSAA requested {MsaaSampleCount} | MSAA actual {ActualMsaaSampleCount} | " +
                "MSAA max {MaxSupportedMsaaSampleCount}",
                FormatRate(cps),
                FormatRate(engineFps),
                FormatRate(gpuFps),
                gpuBackbuffer.MsaaSampleCount,
                gpuBackbuffer.ActualMsaaSampleCount,
                gpuBackbuffer.MaxSupportedMsaaSampleCount);

            return;
        }

        Engine.Logger.LogInformation(
            "CPS {Cps} | engine FPS {EngineFps}",
            FormatRate(cps),
            FormatRate(engineFps));
    }

    private static double? GetRate(
        TelemetrySourceSnapshot? source,
        string metricKey,
        double elapsedSeconds)
    {
        return elapsedSeconds > 0d &&
            source?.Metrics.TryGetValue(metricKey, out var metric) == true &&
            metric.Availability == TelemetryAvailability.Available
                ? metric.Count / elapsedSeconds
                : null;
    }

    private static string FormatRate(double? rate) => rate?.ToString("0.0") ?? "n/a";

    /// <inheritdoc/>
    protected override void OnSceneGraphCreated() => _runtime.OnSceneGraphCreated();

    /// <inheritdoc/>
    protected override void OnMouseAdapterInitialized() => _runtime.OnMouseAdapterInitialized();

    /// <inheritdoc/>
    protected override void OnKeyboardAdapterInitialized() => _runtime.OnKeyboardAdapterInitialized();

    /// <inheritdoc/>
    protected override void UnhookEvents()
    {
        Engine.AfterBackgroundTasksExecute -= LogRuntimeProfilerSample;
        _profilerRequest?.Dispose();
        _profilerRequest = null;
        _runtime.UnhookEvents();
    }

    /// <inheritdoc/>
    protected override void CreateDirectDrawings()
    {
        // Deliberately empty: startup presentation is created in BeginPostSplashStartup()
        // so it does not appear beneath the Gondwana splash.
    }

    /// <inheritdoc/>
    protected override void OnEngineStarted()
    {
        // Deliberately empty: startup music begins in BeginPostSplashStartup()
        // after the Gondwana splash has fully faded out.
    }

    private static void ConfigurePlatformKeyboardInput(KeyboardEventPoller keyboard)
    {
        RegisterRange(keyboard, 65, 90);  // A-Z
        RegisterRange(keyboard, 48, 57);  // 0-9
        RegisterRange(keyboard, 96, 105); // numpad 0-9

        foreach (int key in new[]
                 {
                     8, 13, 32, 35, 36, 37, 39, 46,
                     106, 107, 109, 110, 111,
                     186, 187, 188, 189, 190, 191, 192,
                     219, 220, 221, 222
                 })
        {
            keyboard.StartMonitoringKey(key);
        }

        static void RegisterRange(KeyboardEventPoller poller, int first, int last)
        {
            for (int key = first; key <= last; key++)
                poller.StartMonitoringKey(key);
        }
    }

    private static void PersistGameState()
    {
        Gondwana.Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }
}
