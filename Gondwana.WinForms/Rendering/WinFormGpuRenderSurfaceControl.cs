using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceControl : UserControl
{
    private readonly SKGLControl _glControl;
    public RenderSurfaceHost<GpuBackbuffer> RenderSurfaceHost { get; private set; }

    public WinFormGpuRenderSurfaceControl()
    {
        _glControl = new SKGLControl { Dock = DockStyle.Fill };
        Controls.Add(_glControl);

        this.Load += (_, _) => InitializeBackbuffer();
    }

    private void InitializeBackbuffer()
    {
        var renderAdapter = new WinFormGpuRenderSurfaceAdapter(_glControl);
        RenderSurfaceHost = new RenderSurfaceHost<GpuBackbuffer>(renderAdapter);
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