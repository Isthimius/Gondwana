using Gondwana.Configuration;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerGameHost(WinFormGpuRenderSurfaceControl surface, string scenePath)
    : WinFormsGpuGameHost(surface)
{
    internal ViewerCameraController? Camera { get; private set; }

    protected override Scene CreateInitialScene() => new ViewerSceneLoader().Load(scenePath);

    protected override void OnSceneBound()
    {
        RenderSurface.Host.ViewManager.ConfigureSingleFullView();
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = RectangleF.Empty;
        view.Camera.SnapTo(PointF.Empty);
        Camera = new ViewerCameraController(view);
    }

    protected override void ConfigureGamepads() { }

    protected override void OnDisposed()
    {
        // GameHostBase disposes only an initialized Engine. Also cover failure
        // during content loading, before its engine-initialized flag was set.
        if (!Engine.IsDisposed)
            Engine.Dispose();
        (Engine.Input.KeyboardEventPoller?.Adapter as IDisposable)?.Dispose();
    }

    // Viewer startup must not load an unrelated game config/state from the cwd.
    internal sealed class ViewerConfiguration : IEngineConfigurationStore
    {
        public EngineConfiguration Configuration { get; } = new();
        public bool AutoSave { get; set; }
        public void Save() { }
        public void Dispose() { }
    }
}
