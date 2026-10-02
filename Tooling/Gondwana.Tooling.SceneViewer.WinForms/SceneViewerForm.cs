using Gondwana.WinForms.Rendering;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerForm : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private readonly string _scenePath;
    private SceneViewerGameHost? _host;

    internal SceneViewerForm(string scenePath)
    {
        _scenePath = scenePath;
        Text = $"Gondwana Scene Viewer — {Path.GetFileName(scenePath)}";
        ClientSize = new Size(1024, 768);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_surface);
        _surface.MouseWheel += (_, e) => Dispatch(camera => camera.Zoom(e.Delta));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _host = new SceneViewerGameHost(_surface, _scenePath);
            _host.CloseRequested += OnCloseRequested;
            _host.InitializeWithConfigurationStore(new SceneViewerGameHost.ViewerConfiguration());
            _surface.Focus();
        }
        catch (Exception ex)
        {
            Program.ReportError(ex);
            Close();
        }
    }

    private void OnCloseRequested()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        BeginInvoke(Close);
    }

    private void Dispatch(Action<ViewerCameraController> action)
    {
        if (_host?.Camera is { } camera)
            _host.Engine.EngineDispatcher.Post(() => action(camera));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_host is not null)
            {
                _host.CloseRequested -= OnCloseRequested;
                _host.Dispose();
                _host = null;
            }

            // Explicitly release these: the current control's generated component
            // container may be null, so its Dispose guard can skip host/adapter cleanup.
            _surface.Adapter?.Dispose();
            _surface.Host?.Backbuffer?.Dispose();
            _surface.Host?.Dispose();
        }

        base.Dispose(disposing);
    }
}
