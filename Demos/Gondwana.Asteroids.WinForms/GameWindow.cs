using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Asteroids;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface;
    private AsteroidsGameHost? _host;

    internal GameWindow()
    {
        Text = "Gondwana: Asteroids";
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(660, 440);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        _surface = new WinFormGpuRenderSurfaceControl { Dock = DockStyle.Fill };
        Controls.Add(_surface);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new AsteroidsGameHost(_surface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            _host!.Initialize(logLevel: LogLevel.Warning);
            _surface.Host.RequestLogicalResolution(AsteroidsGame.Width, AsteroidsGame.Height);
            _surface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Asteroids startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnFormClosed(e);
    }
}
