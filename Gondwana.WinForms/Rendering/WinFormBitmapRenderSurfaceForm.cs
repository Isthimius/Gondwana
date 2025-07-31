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

    public RenderSurfaceHost? RenderSurfaceHost { get; private set; }

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
        RenderSurfaceHost = new RenderSurfaceHost(renderAdapter);

        // Create the Backbuffer
        var screenBounds = Screen.FromControl(this).Bounds;
        var buffer = new BitmapBackbuffer(screenBounds.Width, screenBounds.Height);

        // Bind the buffer to the surface
        RenderSurfaceHost.Bind(buffer);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        RenderSurfaceHost?.Dispose();
    }
}
