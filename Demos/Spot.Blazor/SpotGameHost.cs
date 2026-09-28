using Gondwana.Assets;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
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

        bool msaaLogged = false;
        Gondwana.Engine.Instance.CPSCalculated += args =>
        {
            if (msaaLogged ||
                args.GpuFps is not > 0 ||
                RenderSurface.Host.Backbuffer is not GpuBackbuffer gpuBackbuffer)
            {
                return;
            }

            msaaLogged = true;
            Engine.Logger.LogInformation(
                "Spot.Blazor MSAA requested {MsaaSampleCount} | actual {ActualMsaaSampleCount} | max {MaxSupportedMsaaSampleCount}",
                gpuBackbuffer.MsaaSampleCount,
                gpuBackbuffer.ActualMsaaSampleCount,
                gpuBackbuffer.MaxSupportedMsaaSampleCount);
        };

        return _runtime.CreateInitialScene();
    }

    protected override void OnSceneGraphCreated() => _runtime.OnSceneGraphCreated();

    protected override void OnMouseAdapterInitialized() => _runtime.OnMouseAdapterInitialized();

    protected override void OnKeyboardAdapterInitialized() => _runtime.OnKeyboardAdapterInitialized();

    protected override void UnhookEvents() => _runtime.UnhookEvents();

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
