using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a Windows Forms control that hosts GPU-accelerated Gondwana rendering.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormGpuRenderSurfaceControl"/> class.
    /// </summary>
    public WinFormGpuRenderSurfaceControl()
    {
        // Do not Dock=Fill: WinForms will otherwise physically resize SKGLControl to 0×0
        // during minimize/layout. OpenTK forwards that size to its native GLFW child. Keep the
        // drawable at least 1×1 and let this wrapper report the real presentation size instead.
        _glControl = new SKGLControl();
        Controls.Add(_glControl);
        UpdateGlControlBounds();

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

        // Keep the native GL drawable valid even when this wrapper is temporarily 0×0. The
        // adapter separately observes this wrapper's real size and suspends presentation at zero.
        SizeChanged += (_, __) => UpdateGlControlBounds();

        // Fire once after this control is realized
        HandleCreated += (_, __) => _adapter?.RefreshDestinationSize();
        if (IsHandleCreated)
            BeginInvoke((Action)(() => _adapter?.RefreshDestinationSize()));
    }

    /// <summary>
    /// Refreshes the render surface size when the control's parent changes.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (IsHandleCreated)
            BeginInvoke((Action)(() => _adapter?.RefreshDestinationSize()));
    }

    private void UpdateGlControlBounds()
    {
        var rect = DisplayRectangle;
        _glControl.SetBounds(
            rect.X,
            rect.Y,
            Math.Max(1, rect.Width),
            Math.Max(1, rect.Height));
    }

    private void InitializeBackbuffer()
    {
        _adapter = new WinFormGpuRenderSurfaceAdapter(_glControl, this);
        Host = new RenderSurfaceHost<GpuBackbuffer>(_adapter);

        // Register the host so the adapter drives all rendering on the GL thread.
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