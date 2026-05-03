using System;
using System.Drawing;
using System.Windows.Forms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace MyGame;

internal sealed class GameWindow : Form
{
//#if (UseGpuBackbuffer)
    // OpenGL-accelerated render surface provided by Gondwana.WinForms.
    private readonly WinFormGpuRenderSurfaceControl _renderSurface = new();
//#else
    // SkiaSharp-backed render surface provided by Gondwana.WinForms.
    private readonly WinFormBitmapRenderSurfaceControl _renderSurface = new();
//#endif
    private MyGameHost? _host;

    internal GameWindow()
    {
        this.Text             = "MyGame";
        this.ClientSize       = new Size(640, 640);
        this.FormBorderStyle  = FormBorderStyle.FixedSingle;
        this.StartPosition    = FormStartPosition.CenterScreen;
        this.MinimizeBox      = false;
        this.MaximizeBox      = false;

        _renderSurface.Dock = DockStyle.Fill;
        Controls.Add(_renderSurface);
    }

    // Create the host once the form and all controls exist.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new MyGameHost(_renderSurface);
    }

    // Initialize AFTER the form is visible so SynchronizationContext is available,
    // which the engine requires to marshal callbacks back to the UI thread.
    // Tip: change LogLevel.Warning to LogLevel.Debug to see per-frame engine output.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _host!.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnFormClosed(e);
    }
}
