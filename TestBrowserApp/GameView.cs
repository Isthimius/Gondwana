using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace TestBrowserApp;

/// <summary>
/// Single-view host used on browser/WASM targets (replaces <c>GameWindow</c>).
/// <para>
/// Avalonia on WASM uses <see cref="Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime"/>
/// which supplies one root <see cref="Control"/> instead of a desktop <c>Window</c>.
/// This <see cref="UserControl"/> fulfils that role.
/// </para>
/// </summary>
internal sealed class GameView : UserControl
{
    private readonly GameRenderSurface _renderSurface = new();
    private TestBrowserAppHost? _host;

    internal GameView()
    {
        this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        this.VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch;

        _renderSurface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch;

        Content = _renderSurface;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _host = new TestBrowserAppHost(_renderSurface);
        // Tip: change LogLevel.Warning to LogLevel.Debug to see per-frame engine output.
        _host.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnDetachedFromVisualTree(e);
    }
}
