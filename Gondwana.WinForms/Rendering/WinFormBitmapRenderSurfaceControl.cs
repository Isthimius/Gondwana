using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a Windows Forms control that hosts bitmap-based Gondwana rendering.
/// </summary>
public partial class WinFormBitmapRenderSurfaceControl : UserControl
{
    private readonly SKControl _skControl;

    /// <summary>
    /// Gets the render surface adapter used by this control.
    /// </summary>
    public WinFormBitmapRenderSurfaceAdapter Adapter { get; }

    /// <summary>
    /// Gets the render surface host used for displaying game content.
    /// </summary>
    public RenderSurfaceHost<BitmapBackbuffer> Host { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormBitmapRenderSurfaceControl"/> class.
    /// </summary>
    public WinFormBitmapRenderSurfaceControl()
    {
        _skControl = new SKControl { Dock = DockStyle.Fill };
        Controls.Add(_skControl);

        // Forward mouse events
        _skControl.MouseDown += (s, e) => OnMouseDown(e);
        _skControl.MouseUp += (s, e) => OnMouseUp(e);
        _skControl.MouseMove += (s, e) => OnMouseMove(e);
        _skControl.MouseClick += (s, e) => OnMouseClick(e);
        _skControl.MouseWheel += (s, e) => OnMouseWheel(e);
        _skControl.MouseEnter += (s, e) => OnMouseEnter(e);
        _skControl.MouseLeave += (s, e) => OnMouseLeave(e);

        // Create the render adapter for the SKControl
        Adapter = new WinFormBitmapRenderSurfaceAdapter(_skControl);

        // Create the surface and hook the adapter
        Host = new RenderSurfaceHost<BitmapBackbuffer>(Adapter);

        // Ensure the adapter re-reads size whenever THIS wrapper changes size
        SizeChanged += (_, __) => Adapter.RefreshDestinationSize();

        // Fire once after this control is realized
        HandleCreated += (_, __) => Adapter.RefreshDestinationSize();
        if (IsHandleCreated)
            BeginInvoke((Action)Adapter.RefreshDestinationSize);
    }

    /// <summary>
    /// Refreshes the render surface size when the control's parent changes.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (IsHandleCreated)
            BeginInvoke((Action)Adapter.RefreshDestinationSize);
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
        }

        base.Dispose(disposing);
    }
}