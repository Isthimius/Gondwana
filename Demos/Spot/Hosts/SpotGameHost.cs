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

    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(LogLevel.Information);

        Gondwana.Engine.Instance.CPSCalculated += args =>
        {
            if (RenderSurface.Host.Backbuffer is GpuBackbuffer gpuBackbuffer)
            {
                string gpuFps = args.GpuFps.HasValue
                    ? args.GpuFps.Value.ToString("0.0")
                    : "n/a";

                Engine.Logger.LogInformation(
                    "CPS {Cps:0.0} | engine FPS {EngineFps:0.0} | GPU FPS {GpuFps} | " +
                    "MSAA requested {MsaaSampleCount} | MSAA actual {ActualMsaaSampleCount} | " +
                    "MSAA max {MaxSupportedMsaaSampleCount}",
                    args.GrossCPS,
                    args.NetCPS,
                    gpuFps,
                    gpuBackbuffer.MsaaSampleCount,
                    gpuBackbuffer.ActualMsaaSampleCount,
                    gpuBackbuffer.MaxSupportedMsaaSampleCount);

                return;
            }

            Engine.Logger.LogInformation("{CyclesPerSecond}", args);
        };

        return _runtime.CreateInitialScene();
    }

    protected override void OnSceneGraphCreated() => _runtime.OnSceneGraphCreated();

    protected override void OnMouseAdapterInitialized() => _runtime.OnMouseAdapterInitialized();

    protected override void OnKeyboardAdapterInitialized() => _runtime.OnKeyboardAdapterInitialized();

    protected override void UnhookEvents() => _runtime.UnhookEvents();

    protected override void CreateDirectDrawings()
    {
        // Deliberately empty: startup presentation is created in BeginPostSplashStartup()
        // so it does not appear beneath the Gondwana splash.
    }

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
