using Avalonia.Controls;
using Gondwana.Avalonia.Rendering;
using Microsoft.Extensions.Logging;

namespace MyGame;

internal sealed class GameWindow : Window
{
    // Gondwana.Avalonia bitmap-based render surface — works on all Avalonia desktop targets.
    private readonly AvaloniaBitmapRenderSurfaceControl _renderSurface = new();
    private MyGameHost? _host;

    internal GameWindow()
    {
        this.Title     = "MyGame";
        this.Width     = 640;
        this.Height    = 640;
        this.CanResize = false;

        _renderSurface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch;

        Content = _renderSurface;
    }

    // Create the host once the window is fully loaded.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _host = new MyGameHost(_renderSurface);
        // Tip: change LogLevel.Warning to LogLevel.Debug to see per-frame engine output.
        _host.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnClosed(EventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnClosed(e);
    }
}
