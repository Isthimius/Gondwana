using Avalonia.Controls;
using Avalonia.Layout;
using Gondwana.Avalonia.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// The main game window for SpotAvalonia on desktop targets.
/// </summary>
internal sealed class GameWindow : Window
{
    private readonly AvaloniaBitmapRenderSurfaceControl _renderSurface = new();
    private SpotAvaloniaGameHost? _host;

    internal GameWindow()
    {
        Title = "Spot (Avalonia)";
        Width = 769;
        Height = 769;
        CanResize = false;

        _renderSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment = VerticalAlignment.Stretch;

        Content = _renderSurface;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _host = new SpotAvaloniaGameHost(_renderSurface);

        // Subscribe before Initialize() so the handler fires during initialization.
        _host.Engine.InitializationComplete += () =>
        {
            _host.Engine.Configuration.TargetFPS = 0;
            _host.StartDefaultGame();
        };

        _host.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnClosed(EventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnClosed(e);
    }
}
