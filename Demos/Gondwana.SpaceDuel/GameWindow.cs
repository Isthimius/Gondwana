using System.IO;
using Gondwana.Widgets.Overlays;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SpaceDuel;

internal sealed class GameWindow : Form
{
    private const int GpuTargetFps = 0;
    private const int GpuMsaaSampleCount = 4;

    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new();
    private SpaceDuelGameHost? _gameHost;

    internal GameWindow()
    {
        Text = "Gondwana: Space Duel";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.FromPoint(Cursor.Position).Bounds;
        WindowState = FormWindowState.Normal;
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
        _gameHost = new SpaceDuelGameHost(_renderSurface);

        // Match Spot's GPU startup configuration. Subscribing before Initialize()
        // ensures these values are applied as the engine finishes initialization.
        _gameHost.Engine.InitializationComplete += () =>
        {
            _gameHost.Engine.Configuration.TargetFPS = GpuTargetFps;
            _gameHost.Engine.Configuration.VSync = false;
            _gameHost.Engine.Configuration.MsaaSampleCount = GpuMsaaSampleCount;
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            ShowStartupSplashAndInitialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"The space duel could not start.\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Close();
        }
    }

    private void ShowStartupSplashAndInitialize()
    {
        if (_gameHost is null)
            throw new InvalidOperationException(
                "Game host was not initialized before startup splash initialization.");

        Enabled = false;

        try
        {
            _gameHost.Initialize(logLevel: LogLevel.Warning);

            // The form is already borderless and monitor-sized before initialization.
            // Explicitly refresh the GL destination once more so the GPU backbuffer
            // is synchronized with the actual full-screen client area.
            _renderSurface.Adapter.RefreshDestinationSize();

            string imagePath = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "gondwana-logo-text.png");

            using var imageStream = File.OpenRead(imagePath);
            var host = _renderSurface.Host;
            var view = host.ViewManager.Views[0];

            var splash = SplashScreen.TryCreate(
                imageStream: imageStream,
                host: host,
                view: view,
                onSplashCompleted: _gameHost.BeginPostSplashStartup);

            if (splash is null)
                _gameHost.BeginPostSplashStartup();
        }
        finally
        {
            Enabled = true;
            Activate();
            _renderSurface.Focus();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _gameHost?.Dispose();
        _gameHost = null;
        base.OnFormClosed(e);
    }
}
