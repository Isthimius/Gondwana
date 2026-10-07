using Gondwana.Assets;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Diagnostics;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Demos.Spot;

/// <summary>Hosts the Spot demo on Gondwana's Blazor WebGL runtime.</summary>
internal sealed partial class SpotGameHost : BlazorGpuGameHost
{
    private readonly AssetsFile _assets;
    private readonly SpotGameRuntime _runtime;
    private IDisposable? _profilerRequest;
    private bool _msaaLogged;

    internal SpotGameHost(
        BlazorGpuRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime,
        AssetsFile assets)
        : base(renderSurface, jsRuntime)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _runtime = new SpotGameRuntime(
            renderSurface.Host,
            scoreToggleKey: (int)BlazorKey.Tab,
            widgetInputRouterAccessor: () => WidgetInputRouter,
            configurePlatformKeyboardInput: ConfigurePlatformKeyboardInput,
            configureNewGameDialogForPlatform: ConfigureNewGameDialogForPlatform);
    }

    internal NewGameOptions? LastNewGameOptions => _runtime.LastNewGameOptions;

    internal void BeginPostSplashStartup() => _runtime.BeginPostSplashStartup();

    internal void OpenNewGameDialog(NewGameOptions? options = null)
        => _runtime.OpenNewGameDialog(options);

    internal void SetMusicEnabled(bool enabled) => _runtime.SetMusicEnabled(enabled);

    internal void SetSoundEffectsEnabled(bool enabled) => _runtime.SetSoundEffectsEnabled(enabled);

    internal void SetJiggleEnabled(bool enabled) => _runtime.SetJiggleEnabled(enabled);

    internal void SetCloudsEnabled(bool enabled) => _runtime.SetCloudsEnabled(enabled);

    protected override void OnConfigurePlatform()
    {
        Engine.UseBrowserAudio();
    }

    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(Microsoft.Extensions.Logging.LogLevel.Information);

        _profilerRequest = Engine.Profiler.Start();
        Engine.AfterBackgroundTasksExecute += LogMsaaAfterFirstPresentation;

        return _runtime.CreateInitialScene();
    }

    private void LogMsaaAfterFirstPresentation()
    {
        if (_msaaLogged)
            return;

        var snapshot = Engine.Profiler.GetLatestSnapshot();
        var renderSource = snapshot?.Sources.FirstOrDefault(
            source => source.Id == RenderSurface.Host.Telemetry?.Id);

        if (snapshot is null ||
            GetRate(renderSource, "presentation.count", snapshot.ElapsedSeconds) is not > 0d ||
            RenderSurface.Host.Backbuffer is not GpuBackbuffer gpuBackbuffer)
        {
            return;
        }

        _msaaLogged = true;
        Engine.Logger.LogInformation(
            "Spot.Blazor MSAA requested {MsaaSampleCount} | actual {ActualMsaaSampleCount} | max {MaxSupportedMsaaSampleCount}",
            gpuBackbuffer.MsaaSampleCount,
            gpuBackbuffer.ActualMsaaSampleCount,
            gpuBackbuffer.MaxSupportedMsaaSampleCount);

        Engine.AfterBackgroundTasksExecute -= LogMsaaAfterFirstPresentation;
        _profilerRequest?.Dispose();
        _profilerRequest = null;
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

    protected override void OnSceneGraphCreated() => _runtime.OnSceneGraphCreated();

    protected override void OnMouseAdapterInitialized() => _runtime.OnMouseAdapterInitialized();

    protected override void OnKeyboardAdapterInitialized() => _runtime.OnKeyboardAdapterInitialized();

    protected override void UnhookEvents()
    {
        Engine.AfterBackgroundTasksExecute -= LogMsaaAfterFirstPresentation;
        _profilerRequest?.Dispose();
        _profilerRequest = null;
        _runtime.UnhookEvents();
    }

    protected override void CreateDirectDrawings()
    {
        // Startup presentation is created after the splash completes.
    }

    protected override void OnEngineStarted()
    {
        // Music starts after the splash completes.
    }

    protected override void OnBlazorDisposed()
    {
        _assets.Dispose();
    }

    private static void ConfigureNewGameDialogForPlatform(NewGameDialog dialog)
    {
        dialog.ConfigureTextInput(textBox => BlazorTextBoxInput.Configure(textBox));
    }

    private static void ConfigurePlatformKeyboardInput(KeyboardEventPoller keyboard)
    {
        BlazorTextBoxInput.StartMonitoring(keyboard);
    }
}
