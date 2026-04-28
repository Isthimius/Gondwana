using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceControl : UserControl
{
    private readonly SKGLControl _glControl;
    private WinFormGpuRenderSurfaceAdapter? _renderAdapter;
    public RenderSurfaceHost<GpuBackbuffer> RenderSurfaceHost { get; private set; } = null!;

    public RenderSurfaceHost<GpuBackbuffer> Host { get; }

    public WinFormGpuRenderSurfaceControl()
    {
        _glControl = new SKGLControl { Dock = DockStyle.Fill };
        Controls.Add(_glControl);

        // Forward mouse events from the inner GL control to this outer control so that a
        // WinFormsMouseAdapter attached to this control sees them.  The inner SKGLControl
        // fills the entire client area and therefore receives all mouse input; without this
        // forwarding the outer control's mouse events never fire.
        _glControl.MouseDown  += (_, e) => OnMouseDown(e);
        _glControl.MouseUp    += (_, e) => OnMouseUp(e);
        _glControl.MouseMove  += (_, e) => OnMouseMove(e);
        _glControl.MouseClick += (_, e) => OnMouseClick(e);
        _glControl.MouseWheel += (_, e) => OnMouseWheel(e);
        _glControl.MouseEnter += (_, e) => OnMouseEnter(e);
        _glControl.MouseLeave += (_, e) => OnMouseLeave(e);

        this.Load += (_, _) => InitializeBackbuffer();
    }

    private void InitializeBackbuffer()
    {
        _renderAdapter = new WinFormGpuRenderSurfaceAdapter(_glControl);
        RenderSurfaceHost = new RenderSurfaceHost<GpuBackbuffer>(_renderAdapter);

        var gpuBackbuffer = (GpuBackbuffer)RenderSurfaceHost.Backbuffer;

        // Called once from the GL thread when the GRContext becomes available for the first time.
        _renderAdapter.GrContextFirstAvailable += (grContext) =>
        {
            gpuBackbuffer.Initialize(grContext, _renderAdapter.Width, _renderAdapter.Height);
        };

        // Called from the GL thread whenever the control is resized and the GRContext is ready.
        _renderAdapter.ResizeRequested += (grContext, w, h) =>
        {
            gpuBackbuffer.Initialize(grContext, w, h);
        };

        // Register the host so the adapter drives all rendering on the GL thread (Option A).
        _renderAdapter.SetHost(RenderSurfaceHost);
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
            _renderAdapter?.Dispose();
        }

        base.Dispose(disposing);
    }
}