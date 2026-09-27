using System;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Hosts the Spot demo on Gondwana's WinForms GPU runtime.
/// Game-specific responsibilities are split across focused partial-class files.
/// </summary>
internal sealed partial class SpotGameHost : WinFormsGpuGameHost
{
    private const int PersistentMenuHeight = 32;
    private const int ScoreToggleKey = 9;

    private Scene ActiveScene => Scene
        ?? throw new InvalidOperationException("Spot scene has not been created.");

    private RenderSurfaceHostBase SurfaceHost => RenderSurface.Host;
    private int SurfaceWidth => RenderSurface.Width;
    private int SurfaceHeight => RenderSurface.Height;

    internal SpotGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

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

    partial void ConfigurePlatformKeyboardInput(Gondwana.Input.Keyboard.KeyboardEventPoller keyboard)
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

        static void RegisterRange(Gondwana.Input.Keyboard.KeyboardEventPoller poller, int first, int last)
        {
            for (int key = first; key <= last; key++)
                poller.StartMonitoringKey(key);
        }
    }

    partial void PersistGameState()
    {
        Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }
}
