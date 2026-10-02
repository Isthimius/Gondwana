using System.Diagnostics;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerForm : Form, IMessageFilter
{
    private readonly WinFormGpuRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly HashSet<Keys> _keys = [];
    private readonly string _scenePath;
    private SceneViewerGameHost? _host;
    private long _lastTick;

    internal SceneViewerForm(string scenePath)
    {
        _scenePath = scenePath;
        Text = $"Gondwana Scene Viewer — {Path.GetFileName(scenePath)}";
        ClientSize = new Size(1024, 768);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_surface);
        _surface.MouseWheel += (_, e) => Dispatch(camera => camera.Zoom(e.Delta));
        _timer.Tick += UpdateCamera;
        Deactivate += (_, _) => _keys.Clear();
        Application.AddMessageFilter(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _host = new SceneViewerGameHost(_surface, _scenePath);
            _host.InitializeWithConfigurationStore(new SceneViewerGameHost.ViewerConfiguration());
            _surface.Focus();
            _lastTick = Stopwatch.GetTimestamp();
            _timer.Start();
        }
        catch (Exception ex)
        {
            Program.ReportError(ex);
            Close();
        }
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (!ContainsFocus)
            return false;
        if (m.Msg is not (0x100 or 0x101 or 0x104 or 0x105))
            return false;
        var key = (Keys)(m.WParam.ToInt32() & 0xffff);
        if (key is not (Keys.W or Keys.A or Keys.S or Keys.D or Keys.Up or Keys.Down or
            Keys.Left or Keys.Right or Keys.ShiftKey or Keys.Home or Keys.Escape))
            return false;
        bool down = m.Msg is 0x100 or 0x104;
        if (!down)
            _keys.Remove(key);
        else if (_keys.Add(key))
        {
            if (key == Keys.Escape)
                BeginInvoke(Close);
            else if (key == Keys.Home)
                Dispatch(camera => camera.Reset());
        }
        return true;
    }

    private void UpdateCamera(object? sender, EventArgs e)
    {
        var tick = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTick, tick).TotalSeconds;
        _lastTick = tick;
        bool north = _keys.Contains(Keys.W) || _keys.Contains(Keys.Up);
        bool south = _keys.Contains(Keys.S) || _keys.Contains(Keys.Down);
        bool west = _keys.Contains(Keys.A) || _keys.Contains(Keys.Left);
        bool east = _keys.Contains(Keys.D) || _keys.Contains(Keys.Right);
        bool fast = _keys.Contains(Keys.ShiftKey);
        Dispatch(camera => camera.Move(north, south, west, east, fast, elapsed));
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
            Application.RemoveMessageFilter(this);
            _timer.Dispose();
            _host?.Dispose();
            _host = null;
            // Explicitly release these: the current control's generated component
            // container may be null, so its Dispose guard can skip host/adapter cleanup.
            _surface.Adapter?.Dispose();
            _surface.Host?.Backbuffer?.Dispose();
            _surface.Host?.Dispose();
        }
        base.Dispose(disposing);
    }
}
