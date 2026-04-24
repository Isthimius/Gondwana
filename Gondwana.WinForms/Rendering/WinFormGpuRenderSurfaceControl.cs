using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceControl : UserControl
{
    private readonly SKGLControl _glControl;
    public RenderSurfaceHost<GpuBackbuffer> RenderSurfaceHost { get; private set; } = null!;

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

        var gpuBackbuffer = (GpuBackbuffer)RenderSurfaceHost.Backbuffer;

        // Called once from the GL thread when the GRContext becomes available for the first time.
        renderAdapter.GrContextFirstAvailable += (grContext) =>
        {
            gpuBackbuffer.Initialize(grContext, renderAdapter.Width, renderAdapter.Height);
        };

        // Called from the GL thread whenever the control is resized and the GRContext is ready.
        renderAdapter.ResizeRequested += (grContext, w, h) =>
        {
            gpuBackbuffer.Initialize(grContext, w, h);
        };
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