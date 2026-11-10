using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Breakout;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface;
    private BreakoutGameHost? _host;

    internal GameWindow()
    {
        Text = "Gondwana: Breakout";
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(660, 440);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        // 1280 x 720 presentation at 1.5x gives a 1920 x 1080 logical buffer.
        Gondwana.Engine.Instance.Configuration.RenderScale = 1.5f;
        _surface = new WinFormGpuRenderSurfaceControl { Dock = DockStyle.Fill };
        Controls.Add(_surface);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new BreakoutGameHost(_surface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _host!.Initialize(logLevel: LogLevel.Warning);
            _surface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Breakout startup error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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
