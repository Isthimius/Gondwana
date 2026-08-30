using System.Drawing;
using System.Windows.Forms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.ZeldaPrototype;

internal sealed class GameWindow : Form
{
    internal static readonly Size GameSize = new(960, 640);

    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new();
    private ZeldaGameHost? _gameHost;

    internal GameWindow()
    {
        Text = "Gondwana: The Greenward Key";
        ClientSize = GameSize;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
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
        _gameHost = new ZeldaGameHost(_renderSurface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            _gameHost!.Initialize(logLevel: LogLevel.Warning);
            _renderSurface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"The prototype could not start.\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _gameHost?.Dispose();
        _gameHost = null;
        base.OnFormClosed(e);
    }
}
