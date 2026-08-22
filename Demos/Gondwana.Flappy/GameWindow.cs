using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Flappy;

internal sealed class GameWindow : Form
{
    private static readonly Size GameSize = new(960, 640);

    private readonly WinFormBitmapRenderSurfaceControl _renderSurface = new();
    private FlappyGameHost? _gameHost;

    internal GameWindow()
    {
        Text = "Gondwana: Flappy";
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
        _gameHost = new FlappyGameHost(_renderSurface);
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
                $"The flappy demo could not start.\n\n{ex.Message}",
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
