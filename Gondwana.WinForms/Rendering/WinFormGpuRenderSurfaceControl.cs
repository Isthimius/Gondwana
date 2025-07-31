using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceControl : UserControl
{
    private readonly SKGLControl _glControl;
    public RenderSurfaceHost RenderSurfaceHost { get; }

    public WinFormGpuRenderSurfaceControl()
    {
        _glControl = new SKGLControl { Dock = DockStyle.Fill };
        Controls.Add(_glControl);

        var renderAdapter = new WinFormGpuRenderSurfaceAdapter(_glControl);
        RenderSurfaceHost = new RenderSurfaceHost(renderAdapter);

        this.Load += (_, _) => InitializeBackbuffer();
    }

    private void InitializeBackbuffer()
    {
        // Create the Backbuffer
        var screenBounds = Screen.FromControl(this).Bounds;
        var buffer = new GpuBackbuffer(screenBounds.Width, screenBounds.Height);

        // Bind the buffer to the surface
        RenderSurfaceHost.Bind(buffer);
    }

    /// <summary> 
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
            RenderSurfaceHost?.Dispose();
        }

        base.Dispose(disposing);
    }
}
