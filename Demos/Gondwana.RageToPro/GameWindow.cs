using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.RageToPro;

internal sealed class GameWindow : Form
{
    private readonly WinFormBitmapRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private RageToProGameHost? _host;

    internal GameWindow()
    {
        Text = "Rage to Pro";
        ClientSize = new Size(1280, 720);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        KeyPreview = true;
        Controls.Add(_surface);
        KeyDown += (_, args) => { if (args.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new RageToProGameHost(_surface);
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
            MessageBox.Show(this, $"Rage to Pro could not start.\n\n{ex.Message}", "Startup Error");
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
