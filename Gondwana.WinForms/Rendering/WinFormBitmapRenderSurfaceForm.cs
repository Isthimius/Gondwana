using Gondwana.Rendering;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

public partial class WinFormBitmapRenderSurfaceForm : Form
{
    public WinFormBitmapRenderSurfaceForm()
    {
        InitializeComponent();
        InitializeRendering();
    }

    public RenderSurfaceHost<BitmapBackbuffer>? RenderSurfaceHost { get; private set; }

    private void InitializeRendering()
    {
        // Create and dock the SKControl (acts as our display canvas)
        var skControl = new SKControl
        {
            Dock = DockStyle.Fill
        };
        this.Controls.Add(skControl);

        // Create the render adapter for the SKControl
        var renderAdapter = new WinFormBitmapRenderSurfaceAdapter(skControl);

        // Create the surface and hook the adapter
        RenderSurfaceHost = new RenderSurfaceHost<BitmapBackbuffer>(renderAdapter);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        RenderSurfaceHost?.Dispose();
    }
}
