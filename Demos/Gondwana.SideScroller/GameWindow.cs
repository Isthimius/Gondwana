using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SideScroller;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new() { Dock = DockStyle.Fill };
    private SideScrollerGameHost? _gameHost;

    internal GameWindow()
    {
        Text = "Gondwana: Azure Strike";
        ClientSize = new Size(1280, 720);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Controls.Add(_renderSurface);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _gameHost = new SideScrollerGameHost(_renderSurface);
            _gameHost.Engine.InitializationComplete += () =>
            {
                _gameHost.Engine.Configuration.TargetFPS = 0;
                _gameHost.Engine.Configuration.VSync = true;
                _gameHost.Engine.Configuration.MsaaSampleCount = 4;
            };
            _gameHost.Initialize(logLevel: LogLevel.Warning);
            _gameHost.StartGame();
            _renderSurface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _gameHost?.Dispose();
        base.OnFormClosed(e);
    }
}
