using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurfaceForm : Form
{
    public WinFormGpuRenderSurfaceForm()
    {
        InitializeComponent();
        InitializeRendering();
    }

    public RenderSurfaceHost<GpuBackbuffer>? RenderSurfaceHost { get; private set; }

    private void InitializeRendering()
    {
        // Create and dock the GPU-backed SKGLControl
        var skGlControl = new SKGLControl
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(skGlControl);

        // Create the GPU-capable render adapter
        var renderAdapter = new WinFormGpuRenderSurfaceAdapter(skGlControl);

        // Create the surface host with the adapter
        RenderSurfaceHost = new RenderSurfaceHost<GpuBackbuffer>(renderAdapter);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        RenderSurfaceHost?.Dispose();
    }
}
