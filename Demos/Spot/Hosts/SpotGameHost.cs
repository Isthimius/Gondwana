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
}
