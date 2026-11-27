using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Adventure;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface;
    private AdventureGameHost? _host;

    internal GameWindow()
    {
        Text = "Gondwana: Adventure";
        ClientSize = new Size(AdventureGame.Width, AdventureGame.Height);
        MinimumSize = new Size(480, 410);
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
        _host = new AdventureGameHost(_surface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            _host!.Initialize(logLevel: LogLevel.Warning);
            _surface.Host.RequestLogicalResolution(AdventureGame.Width, AdventureGame.Height);
            _surface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Adventure startup error",
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
