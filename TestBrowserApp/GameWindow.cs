using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace TestBrowserApp;

/// <summary>
/// Main application window for desktop targets (Windows, macOS, Linux).
/// On browser/WASM the single-view equivalent <see cref="GameView"/> is used instead.
/// </summary>
internal sealed class GameWindow : Window
{
    private readonly GameRenderSurface _renderSurface = new();
    private TestBrowserAppHost? _host;

    internal GameWindow()
    {
        this.Title     = "TestBrowserApp";
        this.Width     = 640;
        this.Height    = 640;
        this.CanResize = false;

        _renderSurface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch;

        Content = _renderSurface;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _host = new TestBrowserAppHost(_renderSurface);
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
