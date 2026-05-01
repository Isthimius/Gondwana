using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Gondwana.Avalonia.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// Single-view host for SpotAvalonia on browser/WASM targets.
/// Used as the <c>MainView</c> of <see cref="Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime"/>.
/// </summary>
internal sealed class GameView : UserControl
{
    private readonly AvaloniaBitmapRenderSurfaceControl _renderSurface = new();
    private SpotAvaloniaGameHost? _host;

    internal GameView()
    {
        _renderSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment = VerticalAlignment.Stretch;

        Content = _renderSurface;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_host != null)
            return;

        _host = new SpotAvaloniaGameHost(_renderSurface);

        // Subscribe before Initialize() so the handler fires during initialization.
        _host.Engine.InitializationComplete += () =>
        {
            _host.Engine.Configuration.TargetFPS = 0;
            _host.StartDefaultGame();
        };

        _host.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnDetachedFromVisualTree(e);
    }
}
