using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormGpuRenderSurface : Form
{
    public WinFormGpuRenderSurface()
    {
        InitializeComponent();
        InitializeRendering();
    }

    public RenderSurfaceHost? RenderSurfaceHost { get; private set; }

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
        RenderSurfaceHost = new RenderSurfaceHost(renderAdapter);

        // GPU Backbuffer requires OpenGL context, which SKGLControl has now provided
        var buffer = new GpuBackbuffer(skGlControl.Width, skGlControl.Height);

        // Bind buffer to surface host
        RenderSurfaceHost.Bind(buffer);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        RenderSurfaceHost?.Dispose();
    }
}
