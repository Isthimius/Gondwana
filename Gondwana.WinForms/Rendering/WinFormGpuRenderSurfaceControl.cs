using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceControl : UserControl
{
    private readonly SKGLControl _glControl;
    private WinFormGpuRenderSurfaceAdapter? _adapter;

    /// <summary>
    /// Gets the GPU render surface adapter used by this control.
    /// </summary>
    public WinFormGpuRenderSurfaceAdapter Adapter => _adapter!;

    /// <summary>
    /// Gets the render surface host used for displaying game content.
    /// </summary>
    public RenderSurfaceHost<GpuBackbuffer> Host { get; private set; } = null!;

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

        // Ensure the adapter re-reads size whenever THIS wrapper changes size
        SizeChanged += (_, __) => _adapter?.RefreshDestinationSize();

        // Fire once after this control is realized
        HandleCreated += (_, __) => _adapter?.RefreshDestinationSize();
        if (IsHandleCreated)
            BeginInvoke((Action)(() => _adapter?.RefreshDestinationSize()));
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (IsHandleCreated)
            BeginInvoke((Action)(() => _adapter?.RefreshDestinationSize()));
    }

    private void InitializeBackbuffer()
    {
        _adapter = new WinFormGpuRenderSurfaceAdapter(_glControl);
        Host = new RenderSurfaceHost<GpuBackbuffer>(_adapter);

        var gpuBackbuffer = (GpuBackbuffer)Host.Backbuffer;

        // Called once from the GL thread when the GRContext becomes available for the first time.
        _adapter.GrContextFirstAvailable += (grContext) =>
        {
            gpuBackbuffer.Initialize(grContext, _adapter.Width, _adapter.Height);
        };

        // Called from the GL thread whenever the control is resized and the GRContext is ready.
        _adapter.ResizeRequested += (grContext, w, h) =>
        {
            gpuBackbuffer.Initialize(grContext, w, h);
        };

        // Register the host so the adapter drives all rendering on the GL thread (Option A).
        _adapter.SetHost(Host);
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
            Host?.Dispose();
            _adapter?.Dispose();
        }

        base.Dispose(disposing);
    }
}