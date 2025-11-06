using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormBitmapRenderSurfaceControl : UserControl
{
    private readonly SKControl _skControl;

    public WinFormBitmapRenderSurfaceAdapter Adapter { get; }

    public RenderSurfaceHost<BitmapBackbuffer> Host { get; }

    public WinFormBitmapRenderSurfaceControl()
    {
        _skControl = new SKControl { Dock = DockStyle.Fill };
        Controls.Add(_skControl);

        // Forward mouse events
        _skControl.MouseDown += (s, e) => OnMouseDown(e);
        _skControl.MouseUp += (s, e) => OnMouseUp(e);
        _skControl.MouseMove += (s, e) => OnMouseMove(e);
        _skControl.MouseClick += (s, e) => OnMouseClick(e);
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