using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGpuGameHost : WinFormsGpuGameHost
{
    internal SpotGpuGameHost(WinFormGpuRenderSurfaceControl renderSurface)
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
