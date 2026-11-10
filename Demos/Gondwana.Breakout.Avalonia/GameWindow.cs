using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Gondwana.Avalonia.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Breakout;

internal sealed class GameWindow : Window
{
    private readonly AvaloniaGpuRenderSurfaceControl _surface;
    private BreakoutGameHost? _host;

    internal GameWindow()
    {
        Title = "Gondwana: Breakout";
        Width = 1280;
        Height = 750; // Window chrome consumes some height; correct the client on open.
        MinWidth = 660;
        MinHeight = 440;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Gondwana.Engine.Instance.Configuration.RenderScale = 1.5f;

        _surface = new AvaloniaGpuRenderSurfaceControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Content = _surface;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Match the intended 1280 x 720 client area independently of title-bar metrics.
        Width += 1280 - _surface.Bounds.Width;
        Height += 720 - _surface.Bounds.Height;

        // Allow the corrected layout to reach the GPU adapter before initialization.
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _host = new BreakoutGameHost(_surface);
                _host.Initialize(logLevel: LogLevel.Warning);
                _surface.Focus();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Breakout Avalonia failed: {ex}");
                Close();
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnClosed(EventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnClosed(e);
    }
}
