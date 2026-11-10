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
        // GPU adapter dimensions are physical pixels, while Avalonia window bounds
        // are DIPs. Account for display scaling (for example 150% on a 4K screen)
        // so the initial 1280 x 720 presentation establishes a 1920 x 1080 buffer.
        double scaling = RenderScaling > 0d ? RenderScaling : 1d;
        // Window minimums are Avalonia DIPs, not physical pixels. Keep the
        // intended minimum presentation size at high DPI so the 1280x720
        // initial client area is not clamped at 200% scaling or above.
        MinWidth = 660d / scaling;
        MinHeight = 440d / scaling;
        Width += 1280d / scaling - _surface.Bounds.Width;
        Height += 720d / scaling - _surface.Bounds.Height;

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
