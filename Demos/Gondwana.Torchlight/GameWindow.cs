using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Torchlight;

internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new();
    private TorchlightGameHost? _host;

    internal GameWindow()
    {
        Text = "Gondwana: Torchlight Dungeon";
        ClientSize = new Size(960, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimumSize = new Size(760, 520);
        KeyPreview = true;

        _renderSurface.Dock = DockStyle.Fill;
        Controls.Add(_renderSurface);

        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
                Close();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new TorchlightGameHost(_renderSurface);
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
            MessageBox.Show(this, $"Torchlight could not start.\n\n{ex.Message}",
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
