using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.TheGreatPlop;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new();
    private GreatPlopGameHost? _host;

    internal GameWindow()
    {
        Text = "The Great Plop";
        ClientSize = new Size(1280, 720);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        KeyPreview = true;
        _renderSurface.Dock = DockStyle.Fill;
        Controls.Add(_renderSurface);
        KeyDown += (_, args) => { if (args.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new GreatPlopGameHost(_renderSurface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _host!.Initialize(logLevel: LogLevel.Warning);
            _renderSurface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The pasture refused to load.\n\n{ex.Message}",
                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
